using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using DroidDeck.Services.Tuya;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// Plugin Tuya/Smart Life (cobre as marcas que sao rebrand da Tuya: Nova Digital, Positivo,
    /// RSmart, Elgin...). Mesmo formato do ObsService: config em %LocalAppData%\DroidDeck,
    /// estado espelhado ao vivo pelo SignalR, e um watchdog cuidando da conexao.
    ///
    /// O estado vem por PUSH (MQTT), nunca por polling: o tier gratuito da Tuya permite ~26 mil
    /// chamadas de API por mes (~0,6/min), o que torna polling impossivel; as mensagens de push
    /// tem cota separada de 68 mil/mes.
    /// </summary>
    public class TuyaService : IAsyncDisposable
    {
        private readonly ILogger<TuyaService> _logger;
        private readonly IHubContext<DeckHub> _hub;
        private readonly HttpClient _http;

        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private readonly ConcurrentDictionary<string, TuyaDevice> _devices = new();

        private TuyaConfig _config = new();
        private List<TuyaHome> _homes = new();
        private TuyaApiClient? _api;
        private TuyaDeviceRepository? _repo;
        private TuyaMqttClient? _mqtt;

        // Pareamento em andamento (o QR expira em 1-2 min, entao isso e efemero de proposito).
        private (string Token, string UserCode, DateTime Expires)? _pendingPairing;

        public bool Connected => _api != null;
        public bool PushConnected => _mqtt?.Connected ?? false;

        public TuyaService(ILogger<TuyaService> logger, IHubContext<DeckHub> hub, IHttpClientFactory httpFactory)
        {
            _logger = logger;
            _hub = hub;
            _http = httpFactory.CreateClient("tuya");
            _config = LoadConfig();
        }

        // ---- Config em %LocalAppData%\DroidDeck\tuya.json ----

        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroidDeck");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "tuya.json");
            }
        }

        public TuyaConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<TuyaConfig>(File.ReadAllText(ConfigPath)) ?? new TuyaConfig();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Tuya: config ilegivel ({Msg}); comecando do zero", ex.Message);
            }
            return new TuyaConfig();
        }

        public bool HasSavedSession() => _config.HasSession;

        private void SaveConfig()
        {
            try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_config)); }
            catch (Exception ex) { _logger.LogError("Tuya: falha ao salvar config: {Msg}", ex.Message); }
        }

        // ---- Pareamento por QR ----

        /// <summary>Passo 1: gera o QR que o usuario escaneia com o app Smart Life / Tuya Smart.</summary>
        public async Task<object> StartPairingAsync(string userCode)
        {
            var auth = new TuyaAuth(_http, _logger);
            var (token, payload) = await auth.RequestQrCodeAsync(userCode, _config.ClientId, _config.Schema);

            _pendingPairing = (token, userCode, DateTime.UtcNow.AddMinutes(2));

            // O QR ja vai renderizado: o app Flutter nao tem lib de geracao de QR (so de
            // leitura), e o backend ja carrega o QRCoder para o pareamento do proprio DroidDeck.
            var png = Convert.ToBase64String(Lib.PairingInfo.BuildQrPng(payload));

            return new { qrPayload = payload, qrPng = png, expiresInSeconds = 120 };
        }

        /// <summary>
        /// Passo 2: o configurador chama isso em laco ate virar true. Devolve false enquanto
        /// o usuario nao escaneou -- nao e erro.
        /// </summary>
        public async Task<bool> PollPairingAsync()
        {
            if (_pendingPairing is not { } pending) return false;

            if (DateTime.UtcNow > pending.Expires)
            {
                _pendingPairing = null;
                throw new InvalidOperationException("QR expirado; gere outro.");
            }

            var auth = new TuyaAuth(_http, _logger);
            var token = await auth.TryCompleteLoginAsync(pending.Token, pending.UserCode, _config.ClientId);
            if (token == null) return false;

            _pendingPairing = null;
            _config.UserCode = pending.UserCode;
            _config.Token = token;
            SaveConfig();

            _logger.LogInformation("Tuya: pareado (uid {Uid}, endpoint {Endpoint})", token.Uid, token.Endpoint);

            await ConnectAsync();
            return true;
        }

        // ---- Conexao ----

        public async Task ConnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                if (!_config.HasSession)
                    throw new InvalidOperationException("Tuya nao pareado.");

                var api = new TuyaApiClient(_http, _logger, _config.Token!, _config.ClientId);
                api.TokenRefreshed += token =>
                {
                    // O refresh token gira a cada renovacao; perder isso obriga o usuario a
                    // parear de novo, entao a sessao e regravada na hora.
                    _config.Token = token;
                    SaveConfig();
                };

                _api = api;
                _repo = new TuyaDeviceRepository(api, _logger);

                await RefreshDevicesInternalAsync();
                await StartPushAsync();
                await BroadcastStateAsync();
            }
            finally
            {
                _connectLock.Release();
            }
        }

        /// <summary>
        /// Reconstroi o cache de dispositivos. CARO: ~4 chamadas por aparelho. Chamar so a
        /// pedido do usuario ou no startup, nunca periodicamente.
        /// </summary>
        public async Task RefreshDevicesAsync()
        {
            if (_repo == null) throw new InvalidOperationException("Tuya nao conectado.");
            await RefreshDevicesInternalAsync();
            await StartPushAsync();
            await BroadcastStateAsync();
        }

        private async Task RefreshDevicesInternalAsync()
        {
            _homes = await _repo!.QueryHomesAsync();

            _devices.Clear();
            foreach (var home in _homes)
            {
                foreach (var device in await _repo.QueryDevicesAsync(home.Id))
                    _devices[device.Id] = device;
            }

            _logger.LogInformation("Tuya: {N} dispositivo(s) em {H} casa(s)", _devices.Count, _homes.Count);
        }

        private async Task StartPushAsync()
        {
            if (_api == null) return;

            if (_mqtt != null)
            {
                await _mqtt.DisposeAsync();
                _mqtt = null;
            }

            var mqtt = new TuyaMqttClient(_api, _logger);
            mqtt.UpdateDevices(_homes, _devices.Values.ToList());
            mqtt.StatusReported += OnStatusReported;
            mqtt.ConnectionChanged += connected => { _ = BroadcastStateAsync(); };
            mqtt.Start();
            _mqtt = mqtt;
        }

        private void OnStatusReported(TuyaStatusReport report)
        {
            if (!_devices.TryGetValue(report.DeviceId, out var device)) return;

            foreach (var change in report.Changes)
                device.Status[change.Key] = change.Value;

            // Empurra so o que mudou: o app usa isso para repintar o botao (ActiveColor).
            _ = _hub.Clients.All.SendAsync("ReceiveTuyaDeviceState", new
            {
                deviceId = report.DeviceId,
                status = report.Changes,
            });
        }

        // ---- Acoes ----

        public IReadOnlyCollection<TuyaDevice> Devices => _devices.Values.ToList();

        public TuyaDevice? GetDevice(string deviceId)
            => _devices.TryGetValue(deviceId, out var d) ? d : null;

        public async Task SendCommandAsync(string deviceId, string code, object? value)
        {
            if (_repo == null) throw new InvalidOperationException("Tuya nao conectado.");
            await _repo.SendCommandAsync(deviceId, code, value);
        }

        /// <summary>
        /// Inverte um DP booleano usando o ultimo estado conhecido (que o push mantem fresco).
        /// E a acao mais util no deck: um botao que liga e desliga.
        /// </summary>
        public async Task ToggleAsync(string deviceId, string code)
        {
            var device = GetDevice(deviceId)
                ?? throw new InvalidOperationException($"Dispositivo {deviceId} desconhecido.");

            var current = device.Status.TryGetValue(code, out var v) && v is bool b && b;
            await SendCommandAsync(deviceId, code, !current);
        }

        public object GetStatePayload() => new
        {
            connected = Connected,
            push = PushConnected,
            paired = _config.HasSession,
            userCode = _config.UserCode,
            devices = _devices.Values.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                category = d.Category,
                productName = d.ProductName,
                online = d.Online,
                icon = d.Icon,
                status = d.Status,
                functions = d.Functions.ToDictionary(f => f.Key, f => new { type = f.Value.Type, values = f.Value.Values }),
            }),
        };

        private async Task BroadcastStateAsync()
        {
            try { await _hub.Clients.All.SendAsync("ReceiveTuyaState", GetStatePayload()); }
            catch (Exception ex) { _logger.LogDebug("Tuya: broadcast falhou: {Msg}", ex.Message); }
        }

        public async ValueTask DisposeAsync()
        {
            if (_mqtt != null) await _mqtt.DisposeAsync();
            _connectLock.Dispose();
        }
    }
}
