using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Actions;
using DroidDeck.Hubs;
using DroidDeck.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Integrations
{
    /// <summary>Uma tecla da superficie, como o Companion mandou desenhar.</summary>
    public class CompanionKeyState
    {
        public int Key { get; set; }

        /// <summary>PNG em base64.</summary>
        public string? Image { get; set; }
        public string? Color { get; set; }
        public string? TextColor { get; set; }
        public string? Text { get; set; }
        public bool Pressed { get; set; }
    }

    /// <summary>
    /// Os celulares como superficie do Bitfocus Companion, pela Satellite API (TCP 16622): o PC
    /// aparece no Companion como um aparelho com a grade escolhida, recebe o desenho de cada tecla
    /// e repassa os toques. Todos os celulares mostram a mesma superficie.
    /// </summary>
    public class CompanionIntegration : AccountIntegration
    {
        public const string Event = "ReceiveCompanionState";
        public const string KeysEvent = "ReceiveCompanionKeys";
        public const int DefaultPort = 16622;
        public const int DefaultColumns = 8, DefaultRows = 4;
        public const int BitmapSize = 144;

        /// <summary>Limite de uma linha na Satellite API.</summary>
        private const int MaxLine = 2 * 1024 * 1024;

        /// <summary>O Companion derruba a conexao depois de 5 s sem nada.</summary>
        private static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SilenceLimit = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan CapsWait = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan FlushEvery = TimeSpan.FromMilliseconds(40);

        private readonly ConcurrentDictionary<int, CompanionKeyState> _keys = new();
        private readonly ConcurrentDictionary<int, byte> _dirty = new();
        private readonly ConcurrentDictionary<string, HashSet<int>> _pressedBy = new();
        private volatile bool _clearPending;
        private TcpLineClient? _client;
        private CancellationTokenSource? _reconnect;
        private volatile bool _connected;
        private string? _error;
        private string? _version;
        private int _added;

        public CompanionIntegration(ILogger<CompanionIntegration> logger, IHubContext<DeckHub> hub, StreamDeckConfigService config)
            : base(logger, hub, config) { }

        public override string Name => "companion";
        public override string Title => "Companion";
        public override string StateEvent => Event;

        private string? Address => Get("address");
        private int Columns => int.TryParse(Get("columns"), out var v) ? v : DefaultColumns;
        private int Rows => int.TryParse(Get("rows"), out var v) ? v : DefaultRows;

        public override IntegrationStatus GetStatus() => new()
        {
            Connected = _connected,
            Configured = Address != null,
            Account = _connected ? (_version != null ? $"Companion {_version}" : Address) : null,
            Message = _connected ? null : _error,
            Fields = new()
            {
                ["address"] = Address ?? "127.0.0.1",
                ["columns"] = Columns.ToString(CultureInfo.InvariantCulture),
                ["rows"] = Rows.ToString(CultureInfo.InvariantCulture),
            },
        };

        public override object GetState() => new { connected = _connected, columns = Columns, rows = Rows };

        /// <summary>Todas as teclas (ao abrir a tela; depois chegam so as que mudam).</summary>
        public object Snapshot() => new
        {
            connected = _connected,
            columns = Columns,
            rows = Rows,
            keys = _keys.Values.OrderBy(k => k.Key).ToList(),
        };

        public override async Task<IntegrationStatus> ConfigureAsync(Dictionary<string, string> fields)
        {
            var (host, port) = TcpLineClient.ParseAddress(fields.GetValueOrDefault("address"), "127.0.0.1", DefaultPort);
            var columns = ParseSize(fields.GetValueOrDefault("columns"), DefaultColumns, 1, 32);
            var rows = ParseSize(fields.GetValueOrDefault("rows"), DefaultRows, 1, 16);
            Set(("address", port == DefaultPort ? host : $"{host}:{port}"),
                ("columns", columns.ToString(CultureInfo.InvariantCulture)),
                ("rows", rows.ToString(CultureInfo.InvariantCulture)));
            _error = null;
            Restart();
            for (var i = 0; i < 40 && !_connected && _error == null; i++) await Task.Delay(150);
            return GetStatus();
        }

        private static int ParseSize(string? value, int fallback, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v < min || v > max)
                throw new InvalidOperationException("Tamanho da grade inválido.");
            return v;
        }

        public override Task LogoutAsync()
        {
            // O id do aparelho fica: voltar a conectar reencontra a mesma superficie no Companion.
            Clear("device");
            Restart();
            return Task.CompletedTask;
        }

        private void Restart()
        {
            _reconnect?.Cancel();
            _client?.Dispose();
        }

        private string DeviceId
        {
            get
            {
                if (Get("device") is { } id) return id;
                id = "droiddeck-" + Guid.NewGuid().ToString("N")[..12];
                Set(("device", id));
                return id;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            _ = FlushLoopAsync(stoppingToken);
            var delay = TimeSpan.FromSeconds(2);
            while (!stoppingToken.IsCancellationRequested)
            {
                using var reconnect = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _reconnect = reconnect;
                if (Address == null)
                {
                    await Wait(TimeSpan.FromDays(1), reconnect.Token);
                    continue;
                }
                try
                {
                    await RunAsync(reconnect.Token);
                    delay = TimeSpan.FromSeconds(2);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    if (!reconnect.IsCancellationRequested)
                    {
                        _error = ex is SocketException or IOException or OperationCanceledException or TimeoutException
                            ? "Não foi possível conectar ao Companion. Confira se ele está aberto e o endereço."
                            : ex.Message;
                        Logger.LogInformation("Companion: {Error}", ex.Message);
                    }
                }
                _client = null;
                ClearKeys();
                if (_connected)
                {
                    _connected = false;
                    await BroadcastAsync();
                }
                if (!reconnect.IsCancellationRequested)
                {
                    await Wait(delay, reconnect.Token);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            var (host, port) = TcpLineClient.ParseAddress(Address, "127.0.0.1", DefaultPort);
            using var client = await TcpLineClient.ConnectAsync(host, port, TimeSpan.FromSeconds(5), ct);
            _client = client;
            _added = 0;
            using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var lastSeen = DateTime.UtcNow;

            var ping = Task.Run(async () =>
            {
                while (!session.IsCancellationRequested)
                {
                    await Task.Delay(PingEvery, session.Token);
                    if (DateTime.UtcNow - lastSeen > SilenceLimit)
                    {
                        client.Dispose();
                        return;
                    }
                    await client.WriteAsync("PING droiddeck\n", session.Token);
                }
            }, CancellationToken.None);

            try
            {
                while (true)
                {
                    var line = await client.ReadLineAsync(MaxLine, session.Token) ?? throw new EndOfStreamException();
                    lastSeen = DateTime.UtcNow;
                    await HandleLineAsync(client, line, session.Token);
                }
            }
            finally
            {
                session.Cancel();
                try { await ping; }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
            }
        }

        private async Task HandleLineAsync(TcpLineClient client, string line, CancellationToken ct)
        {
            var (command, body) = SatelliteCodec.Split(line);
            switch (command.ToUpperInvariant())
            {
                case "BEGIN":
                    _version = SatelliteCodec.Parse(body).GetValueOrDefault("CompanionVersion");
                    // Versoes antigas nao mandam CAPS.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(CapsWait, ct);
                            await AddDeviceAsync(client, png: false, ct);
                        }
                        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
                    }, CancellationToken.None);
                    break;
                case "CAPS":
                {
                    var formats = SatelliteCodec.Parse(body).GetValueOrDefault("BITMAP_FORMATS") ?? "";
                    await AddDeviceAsync(client, formats.Split(',').Contains("png"), ct);
                    break;
                }
                case "ADD-DEVICE":
                {
                    var p = SatelliteCodec.Parse(body);
                    if (p.ContainsKey("ERROR"))
                        throw new InvalidOperationException(p.GetValueOrDefault("MESSAGE") ?? "O Companion recusou a superfície.");
                    if (p.ContainsKey("OK"))
                    {
                        _connected = true;
                        _error = null;
                        await BroadcastAsync();
                    }
                    break;
                }
                case "KEY-STATE":
                    ApplyKeyState(SatelliteCodec.Parse(body));
                    break;
                case "KEYS-CLEAR":
                    ClearKeys();
                    break;
                case "PING":
                    await client.WriteAsync("PONG " + body + "\n", ct);
                    break;
                case "ERROR":
                    Logger.LogInformation("Companion: {Error}", SatelliteCodec.Parse(body).GetValueOrDefault("MESSAGE"));
                    break;
            }
        }

        private Task AddDeviceAsync(TcpLineClient client, bool png, CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _added, 1) == 1) return Task.CompletedTask;
            return client.WriteAsync(BuildAddDevice(DeviceId, Columns, Rows, png), ct);
        }

        public static string BuildAddDevice(string deviceId, int columns, int rows, bool png)
        {
            var args = new List<(string key, object value)>
            {
                ("PRODUCT_NAME", "DroidDeck"),
                ("KEYS_TOTAL", columns * rows),
                ("KEYS_PER_ROW", columns),
                ("BITMAPS", BitmapSize),
                ("COLORS", "hex"),
                ("TEXT", true),
                ("BRIGHTNESS", false),
            };
            if (png) args.Add(("BITMAP_FORMAT", "png"));
            return SatelliteCodec.Format("ADD-DEVICE", deviceId, args.ToArray());
        }

        public void ApplyKeyState(Dictionary<string, string?> p)
        {
            if (!int.TryParse(p.GetValueOrDefault("KEY"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var key)) return;
            var state = _keys.TryGetValue(key, out var old) ? old : null;
            var next = new CompanionKeyState
            {
                Key = key,
                Image = p.TryGetValue("BITMAP", out var bitmap) ? ImageFrom(bitmap) : state?.Image,
                Color = p.GetValueOrDefault("COLOR") ?? state?.Color,
                TextColor = p.GetValueOrDefault("TEXTCOLOR") ?? state?.TextColor,
                Text = p.TryGetValue("TEXT", out var text) ? DecodeText(text) : state?.Text,
                Pressed = p.TryGetValue("PRESSED", out var pressed) ? !SatelliteCodec.IsFalsey(pressed) : state?.Pressed ?? false,
            };
            _keys[key] = next;
            _dirty[key] = 0;
        }

        private static string? DecodeText(string? base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(base64)); }
            catch (FormatException) { return null; }
        }

        /// <summary>BITMAP: data URL (formato negociado) ou RGB cru (Companion antigo), que vira PNG aqui.</summary>
        public static string? ImageFrom(string? bitmap)
        {
            if (string.IsNullOrEmpty(bitmap)) return null;
            if (bitmap.StartsWith("data:", StringComparison.Ordinal))
            {
                var comma = bitmap.IndexOf(',');
                return comma < 0 ? null : bitmap[(comma + 1)..];
            }
            try { return RgbToPng(Convert.FromBase64String(bitmap)); }
            catch (FormatException) { return null; }
        }

        /// <summary>RGB cru de uma tecla quadrada para PNG em base64. Null se o tamanho nao bater.</summary>
        public static string? RgbToPng(byte[] rgb)
        {
            var side = (int)Math.Round(Math.Sqrt(rgb.Length / 3.0));
            if (side <= 0 || side * side * 3 != rgb.Length) return null;
            using var image = new Bitmap(side, side, PixelFormat.Format24bppRgb);
            var data = image.LockBits(new Rectangle(0, 0, side, side), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                var row = new byte[side * 3];
                for (var y = 0; y < side; y++)
                {
                    // O Bitmap guarda BGR.
                    for (var x = 0; x < side; x++)
                    {
                        var i = (y * side + x) * 3;
                        row[x * 3] = rgb[i + 2];
                        row[x * 3 + 1] = rgb[i + 1];
                        row[x * 3 + 2] = rgb[i];
                    }
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
                }
            }
            finally
            {
                image.UnlockBits(data);
            }
            using var output = new MemoryStream();
            image.Save(output, ImageFormat.Png);
            return Convert.ToBase64String(output.ToArray());
        }

        private void ClearKeys()
        {
            _keys.Clear();
            _dirty.Clear();
            _clearPending = true;
        }

        /// <summary>Junta as teclas que mudaram (uma troca de pagina muda todas de uma vez).</summary>
        private async Task FlushLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(FlushEvery, ct);
                    if (_dirty.IsEmpty && !_clearPending) continue;
                    var clear = _clearPending;
                    _clearPending = false;
                    var batch = new List<CompanionKeyState>();
                    foreach (var key in _dirty.Keys.ToList())
                    {
                        _dirty.TryRemove(key, out _);
                        if (_keys.TryGetValue(key, out var state)) batch.Add(state);
                    }
                    if (DeckHub.ConnectedCount > 0)
                        await Hub.Clients.All.SendAsync(KeysEvent, new { clear, keys = batch }, ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Companion: falha ao enviar teclas");
                }
            }
        }

        /// <summary>Encostou ou soltou o dedo numa tecla (o dono e a conexao do celular).</summary>
        public async Task SetKeyAsync(string owner, int key, bool pressed)
        {
            var client = _client;
            if (!_connected || client == null || key < 0 || key >= Columns * Rows) return;
            var held = _pressedBy.GetOrAdd(owner, _ => new HashSet<int>());
            lock (held)
            {
                if (pressed) held.Add(key);
                else held.Remove(key);
            }
            await client.WriteAsync(SatelliteCodec.Format("KEY-PRESS", DeviceId, ("KEY", key), ("PRESSED", pressed)));
        }

        /// <summary>Celular caiu com o dedo numa tecla: solta, senao ela fica apertada no Companion.</summary>
        public void ReleaseOwner(string owner)
        {
            if (!_pressedBy.TryRemove(owner, out var held)) return;
            int[] keys;
            lock (held) keys = held.ToArray();
            foreach (var key in keys)
                _ = SafeReleaseAsync(owner, key);
        }

        private async Task SafeReleaseAsync(string owner, int key)
        {
            try { await SetKeyAsync(owner, key, false); }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { }
        }
    }

    /// <summary>Acao "companion": abre a superficie (a tela e do app).</summary>
    public class CompanionProvider : IStatefulActionProvider
    {
        private readonly CompanionIntegration _companion;

        public CompanionProvider(CompanionIntegration companion) => _companion = companion;

        public string Type => "companion";
        public string StateEvent => CompanionIntegration.Event;
        public object GetState() => _companion.GetState();

        public ActionSchema Describe() => new()
        {
            Type = Type,
            Title = "Companion",
            Icon = "grid_view",
            Category = "Estúdio",
            Settings = "companion",
            StateEvent = StateEvent,
            Description = "Abre a superfície do Bitfocus Companion: as teclas e páginas configuradas lá, em tela cheia.",
        };

        public Task ExecuteAsync(ActionContext context) => Task.CompletedTask;
    }
}
