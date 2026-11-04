using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using DroidDeck.Actions;
using DroidDeck.Hubs;
using DroidDeck.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Integrations
{
    public record VmixInput(string Key, int Number, string Title, bool Muted);

    /// <summary>O que o XML do vMix diz (programa, preview, gravando, entradas...).</summary>
    public class VmixSnapshot
    {
        public string? Version { get; init; }
        public List<VmixInput> Inputs { get; init; } = new();
        public int Active { get; init; }
        public int Preview { get; init; }
        public bool Recording { get; init; }
        public bool Streaming { get; init; }
        public bool External { get; init; }
        public bool MultiCorder { get; init; }
        public bool FadeToBlack { get; init; }

        /// <summary>Numero da entrada em cada overlay (o indice 0 e o overlay 1); 0 = vazio.</summary>
        public List<int> Overlays { get; init; } = new();

        public string? KeyOf(int number) => Inputs.FirstOrDefault(i => i.Number == number)?.Key;
    }

    /// <summary>
    /// vMix pela TCP API (porta fixa 8099, UTF-8, linhas com CRLF). Uma conexao so: o TALLY chega
    /// sozinho pela assinatura e o resto e pedido e resposta, um de cada vez, como a API exige.
    /// </summary>
    public class VmixIntegration : AccountIntegration
    {
        public const string Event = "ReceiveVmixState";
        public const int Port = 8099;
        public const int TallyOff = 0, TallyProgram = 1, TallyPreview = 2;

        private const int MaxLine = 64 * 1024;
        private const int MaxXml = 32 * 1024 * 1024;
        private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(2);

        private readonly SemaphoreSlim _requestLock = new(1, 1);
        private readonly SemaphoreSlim _poke = new(0, 1);
        private readonly object _pendingLock = new();
        private TaskCompletionSource<string>? _pending;
        private string? _pendingCommand;

        private TcpLineClient? _client;
        private CancellationTokenSource? _reconnect;
        private volatile VmixSnapshot? _snapshot;
        private volatile string _tally = "";
        private volatile bool _connected;
        private string? _error;
        private string? _lastState;

        public VmixIntegration(ILogger<VmixIntegration> logger, IHubContext<DeckHub> hub, StreamDeckConfigService config)
            : base(logger, hub, config) { }

        public override string Name => "vmix";
        public override string Title => "vMix";
        public override string StateEvent => Event;

        private string? Host => Get("host");

        public VmixSnapshot? Snapshot => _connected ? _snapshot : null;

        public override IntegrationStatus GetStatus() => new()
        {
            Connected = _connected,
            Configured = Host != null,
            Account = _connected && _snapshot?.Version is { } v ? $"vMix {v}" : null,
            Message = _connected ? null : _error,
            Fields = new() { ["host"] = Host ?? "127.0.0.1" },
        };

        public override object GetState()
        {
            var s = Snapshot;
            if (s == null) return new { connected = false };
            return new
            {
                connected = true,
                active = s.KeyOf(s.Active),
                preview = s.KeyOf(s.Preview),
                recording = s.Recording,
                streaming = s.Streaming,
                external = s.External,
                multiCorder = s.MultiCorder,
                fadeToBlack = s.FadeToBlack,
                overlays = s.Overlays.Select(s.KeyOf).ToList(),
                inputs = s.Inputs.Select(i => new { key = i.Key, number = i.Number, title = i.Title, muted = i.Muted }).ToList(),
            };
        }

        public override async Task<IntegrationStatus> ConfigureAsync(Dictionary<string, string> fields)
        {
            var (host, port) = TcpLineClient.ParseAddress(fields.GetValueOrDefault("host"), "127.0.0.1", Port);
            Set(("host", port == Port ? host : $"{host}:{port}"));
            _error = null;
            Restart();
            for (var i = 0; i < 40 && !_connected && _error == null; i++) await Task.Delay(150);
            return GetStatus();
        }

        public override Task LogoutAsync()
        {
            Clear();
            Restart();
            return Task.CompletedTask;
        }

        private void Restart()
        {
            _reconnect?.Cancel();
            _client?.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            var delay = TimeSpan.FromSeconds(2);
            while (!stoppingToken.IsCancellationRequested)
            {
                using var reconnect = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _reconnect = reconnect;
                if (Host == null)
                {
                    await Wait(TimeSpan.FromDays(1), reconnect.Token);
                    continue;
                }
                try
                {
                    await RunAsync(Host, reconnect.Token);
                    delay = TimeSpan.FromSeconds(2);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    if (!reconnect.IsCancellationRequested)
                    {
                        _error = ex is SocketException or IOException or OperationCanceledException or TimeoutException
                            ? "Não foi possível conectar ao vMix. Confira se ele está aberto e o endereço."
                            : ex.Message;
                        Logger.LogInformation("vMix: {Error}", ex.Message);
                    }
                }
                if (_connected)
                {
                    _connected = false;
                    _tally = "";
                    await PublishIfChanged();
                }
                if (!reconnect.IsCancellationRequested)
                {
                    await Wait(delay, reconnect.Token);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }
        }

        private async Task RunAsync(string host, CancellationToken ct)
        {
            var (name, port) = TcpLineClient.ParseAddress(host, "127.0.0.1", Port);
            using var client = await TcpLineClient.ConnectAsync(name, port, ReplyTimeout, ct);
            _client = client;
            using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var reader = Task.Run(async () =>
            {
                try { await ReadLoopAsync(client, session.Token); }
                finally
                {
                    FailPending();
                    session.Cancel();
                }
            }, CancellationToken.None);

            try
            {
                await client.WriteAsync("SUBSCRIBE TALLY\r\n", session.Token);
                while (true)
                {
                    ApplyXml(await RequestAsync(client, "XML", "XML\r\n", session.Token));
                    if (!_connected)
                    {
                        _connected = true;
                        _error = null;
                    }
                    await PublishIfChanged();
                    await _poke.WaitAsync(PollEvery, session.Token);
                }
            }
            finally
            {
                session.Cancel();
                client.Dispose();
                try { await reader; }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
            }
        }

        private async Task ReadLoopAsync(TcpLineClient client, CancellationToken ct)
        {
            while (true)
            {
                var line = await client.ReadLineAsync(MaxLine, ct) ?? throw new EndOfStreamException();
                var parts = line.Split(' ', 3);
                switch (parts[0].ToUpperInvariant())
                {
                    case "TALLY" when parts.Length >= 3 && parts[1] == "OK":
                        _tally = parts[2].Trim();
                        Poke();
                        break;
                    case "XML" when parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var length):
                        if (length is < 0 or > MaxXml) throw new InvalidDataException("Resposta longa demais.");
                        Complete("XML", Encoding.UTF8.GetString(await client.ReadBytesAsync(length, ct)));
                        break;
                    case "FUNCTION":
                        Complete("FUNCTION", line);
                        break;
                }
            }
        }

        private void Complete(string command, string value)
        {
            lock (_pendingLock)
            {
                if (_pendingCommand == command) _pending?.TrySetResult(value);
            }
        }

        private void FailPending()
        {
            lock (_pendingLock) _pending?.TrySetException(new IOException("O vMix fechou a conexão."));
        }

        private async Task<string> RequestAsync(TcpLineClient client, string command, string line, CancellationToken ct)
        {
            await _requestLock.WaitAsync(ct);
            try
            {
                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_pendingLock)
                {
                    _pending = tcs;
                    _pendingCommand = command;
                }
                await client.WriteAsync(line, ct);
                return await tcs.Task.WaitAsync(ReplyTimeout, ct);
            }
            finally
            {
                lock (_pendingLock)
                {
                    _pending = null;
                    _pendingCommand = null;
                }
                _requestLock.Release();
            }
        }

        private void Poke()
        {
            lock (_poke)
            {
                if (_poke.CurrentCount == 0) _poke.Release();
            }
        }

        private void ApplyXml(string xml) => _snapshot = ParseXml(xml);

        private async Task PublishIfChanged()
        {
            var state = JsonSerializer.Serialize(GetState());
            if (state == _lastState) return;
            _lastState = state;
            await BroadcastAsync();
        }

        /// <summary>Executa uma funcao do vMix (nome e query string, como na Web API).</summary>
        public async Task FunctionAsync(string function, string query)
        {
            var client = _client;
            if (!_connected || client == null) throw new InvalidOperationException("vMix desconectado.");
            var line = $"FUNCTION {function}{(query.Length > 0 ? " " + query : "")}\r\n";
            var reply = await RequestAsync(client, "FUNCTION", line, CancellationToken.None);
            var parts = reply.Split(' ', 3);
            if (parts.Length >= 2 && parts[1] == "ER")
                throw new InvalidOperationException(parts.Length >= 3 ? parts[2] : "O vMix recusou.");
            Poke();
        }

        /// <summary>Estado de tally de uma entrada, pelo numero dela (0 = apagado).</summary>
        public int TallyOf(int number)
        {
            var tally = _tally;
            return number >= 1 && number <= tally.Length && tally[number - 1] is >= '0' and <= '2' ? tally[number - 1] - '0' : TallyOff;
        }

        public override Task<List<FieldOption>> OptionsAsync(string list, IQueryCollection query)
        {
            if (list != "inputs") return base.OptionsAsync(list, query);
            var inputs = Snapshot?.Inputs ?? new List<VmixInput>();
            return Task.FromResult(inputs
                .Select(i => new FieldOption(i.Key, string.Create(CultureInfo.InvariantCulture, $"{i.Number}. {i.Title}")))
                .ToList());
        }

        /// <summary>Le o XML do vMix. Campo ausente (versao antiga) fica no padrao.</summary>
        public static VmixSnapshot ParseXml(string xml)
        {
            using var reader = XmlReader.Create(new StringReader(xml.TrimStart('﻿')),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("XML do vMix vazio.");

            var inputs = root.Element("inputs")?.Elements("input")
                .Select(i => new VmixInput(
                    (string?)i.Attribute("key") ?? "",
                    IntOf((string?)i.Attribute("number")),
                    (string?)i.Attribute("title") ?? "",
                    BoolOf((string?)i.Attribute("muted"))))
                .Where(i => i.Key.Length > 0 && i.Number > 0)
                .ToList() ?? new List<VmixInput>();

            return new VmixSnapshot
            {
                Version = (string?)root.Element("version"),
                Inputs = inputs,
                Active = IntOf((string?)root.Element("active")),
                Preview = IntOf((string?)root.Element("preview")),
                Recording = BoolOf((string?)root.Element("recording")),
                Streaming = BoolOf((string?)root.Element("streaming")),
                External = BoolOf((string?)root.Element("external")),
                MultiCorder = BoolOf((string?)root.Element("multiCorder")),
                FadeToBlack = BoolOf((string?)root.Element("fadeToBlack")),
                Overlays = root.Element("overlays")?.Elements("overlay")
                    .OrderBy(o => IntOf((string?)o.Attribute("number")))
                    .Select(o => IntOf(o.Value))
                    .ToList() ?? new List<int>(),
            };
        }

        private static int IntOf(string? value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static bool BoolOf(string? value) =>
            string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }
}
