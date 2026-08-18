using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;

namespace DroidDeck.Services.Tuya
{
    /// <summary>Um relato de mudanca de estado vindo do push.</summary>
    public class TuyaStatusReport
    {
        public string DeviceId { get; set; } = "";
        public Dictionary<string, object?> Changes { get; set; } = new();
    }

    /// <summary>
    /// Push de estado da Tuya por MQTT. E o que permite o botao do deck refletir o estado real
    /// do aparelho sem polling -- e polling aqui e inviavel: o tier gratuito da 26k chamadas/mes
    /// (~0,6/min), enquanto as mensagens de push tem cota propria de 68k/mes.
    ///
    /// As credenciais do broker expiram (expireTime, tipicamente 2h) e sao reemitidas a cada
    /// reconexao; por isso o auto-reconnect do cliente MQTT fica DESLIGADO e a reconexao e
    /// nossa -- reconectar com credencial velha so gera rejeicao em laco.
    /// </summary>
    public class TuyaMqttClient : IAsyncDisposable
    {
        private const int ProtocolDeviceReport = 4;

        private readonly TuyaApiClient _api;
        private readonly ILogger _logger;

        private IMqttClient? _client;
        private CancellationTokenSource? _cts;
        private Task? _loop;

        /// <summary>devId -> (dpId -> code), para traduzir relatos de aparelho com suporte local.</summary>
        private Dictionary<string, Dictionary<string, string>> _dpMaps = new();
        private List<string> _ownerIds = new();
        private Dictionary<string, bool> _supportLocal = new();

        public event Action<TuyaStatusReport>? StatusReported;
        public event Action<bool>? ConnectionChanged;

        public bool Connected => _client?.IsConnected ?? false;

        public TuyaMqttClient(TuyaApiClient api, ILogger logger)
        {
            _api = api;
            _logger = logger;
        }

        public void UpdateDevices(IEnumerable<TuyaHome> homes, IEnumerable<TuyaDevice> devices)
        {
            _ownerIds = new List<string>();
            foreach (var h in homes) _ownerIds.Add(h.Id);

            _dpMaps = new Dictionary<string, Dictionary<string, string>>();
            _supportLocal = new Dictionary<string, bool>();
            foreach (var d in devices)
            {
                _dpMaps[d.Id] = new Dictionary<string, string>(d.DpIdToCode);
                _supportLocal[d.Id] = d.SupportLocal;
            }
        }

        public void Start()
        {
            if (_loop != null) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token));
        }

        private async Task RunAsync(CancellationToken ct)
        {
            var backoff = TimeSpan.FromSeconds(1);

            while (!ct.IsCancellationRequested)
            {
                long expireSeconds;
                try
                {
                    expireSeconds = await ConnectOnceAsync(ct);
                    backoff = TimeSpan.FromSeconds(1);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Tuya MQTT: falha ao conectar ({Msg}); nova tentativa em {S}s",
                        ex.Message, backoff.TotalSeconds);
                    try { await Task.Delay(backoff, ct); } catch { return; }
                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 60));
                    continue;
                }

                // Renova ~60s antes de expirar. Credencial nova exige conexao nova.
                var wait = TimeSpan.FromSeconds(Math.Max(expireSeconds - 60, 30));
                try { await Task.Delay(wait, ct); } catch { return; }
            }
        }

        private async Task<long> ConnectOnceAsync(CancellationToken ct)
        {
            var linkId = "droiddeck." + Guid.NewGuid();
            var cfg = await _api.PostAsync("/v1.0/m/life/ha/access/config", null,
                new Dictionary<string, object?> { ["linkId"] = linkId });

            var url = cfg.GetProperty("url").GetString() ?? "";
            var clientId = cfg.GetProperty("clientId").GetString() ?? "";
            var username = cfg.GetProperty("username").GetString() ?? "";
            var password = cfg.GetProperty("password").GetString() ?? "";
            var expire = cfg.TryGetProperty("expireTime", out var e) ? e.GetInt64() : 7200;

            var topic = cfg.GetProperty("topic");
            var ownerTopic = topic.GetProperty("ownerId").GetProperty("sub").GetString() ?? "";
            var devTopic = topic.GetProperty("devId").GetProperty("sub").GetString() ?? "";

            // url chega como "ssl://host:porta" (ou tcp://).
            var uri = new Uri(url.Replace("ssl://", "ssls://").Replace("tcp://", "tcps://"));
            var useTls = url.StartsWith("ssl://", StringComparison.OrdinalIgnoreCase);

            var old = _client;
            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            client.ApplicationMessageReceivedAsync += OnMessageAsync;
            client.DisconnectedAsync += _ =>
            {
                ConnectionChanged?.Invoke(false);
                return Task.CompletedTask;
            };

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithCredentials(username, password)
                .WithTcpServer(uri.Host, uri.Port)
                .WithCleanSession();

            if (useTls) builder = builder.WithTlsOptions(o => o.UseTls());

            await client.ConnectAsync(builder.Build(), ct);
            _client = client;

            var filters = new List<MqttTopicFilter>();
            foreach (var ownerId in _ownerIds)
                filters.Add(new MqttTopicFilterBuilder()
                    .WithTopic(ownerTopic.Replace("{ownerId}", ownerId)).Build());

            foreach (var kv in _dpMaps)
            {
                // /pen para aparelho com suporte local (relata por dpId), /sta para os demais.
                var suffix = _supportLocal.TryGetValue(kv.Key, out var sl) && sl ? "/pen" : "/sta";
                filters.Add(new MqttTopicFilterBuilder()
                    .WithTopic(devTopic.Replace("{devId}", kv.Key) + suffix).Build());
            }

            if (filters.Count > 0)
                await client.SubscribeAsync(BuildSubscribe(filters), ct);

            _logger.LogInformation("Tuya MQTT: conectado ({N} assinaturas, renova em {S}s)", filters.Count, expire);
            ConnectionChanged?.Invoke(true);

            if (old != null)
            {
                try { await old.DisconnectAsync(); } catch { }
                old.Dispose();
            }

            return expire;
        }

        private static MqttClientSubscribeOptions BuildSubscribe(List<MqttTopicFilter> filters)
        {
            var options = new MqttClientSubscribeOptions { TopicFilters = filters };
            return options;
        }

        private Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            try
            {
                var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var protocol = root.TryGetProperty("protocol", out var p) ? p.GetInt32() : 0;
                if (protocol != ProtocolDeviceReport) return Task.CompletedTask;
                if (!root.TryGetProperty("data", out var data)) return Task.CompletedTask;

                var devId = data.TryGetProperty("devId", out var d) ? d.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(devId)) return Task.CompletedTask;
                if (!data.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Array)
                    return Task.CompletedTask;

                var report = new TuyaStatusReport { DeviceId = devId };
                _dpMaps.TryGetValue(devId, out var dpMap);

                foreach (var item in status.EnumerateArray())
                {
                    if (!item.TryGetProperty("value", out var value)) continue;

                    string? code = null;
                    if (item.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                    {
                        code = c.GetString();
                    }
                    else if (item.TryGetProperty("dpId", out var dp) && dpMap != null)
                    {
                        // Relato local: chega o dpId numerico, traduzido pelo mapa da enumeracao.
                        dpMap.TryGetValue(dp.ToString(), out code);
                    }

                    if (!string.IsNullOrEmpty(code))
                        report.Changes[code!] = TuyaDeviceRepository.ToClr(value);
                }

                if (report.Changes.Count > 0) StatusReported?.Invoke(report);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Tuya MQTT: payload ilegivel: {Msg}", ex.Message);
            }

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            try { _cts?.Cancel(); } catch { }
            if (_client != null)
            {
                try { await _client.DisconnectAsync(); } catch { }
                _client.Dispose();
                _client = null;
            }
            _cts?.Dispose();
        }
    }
}
