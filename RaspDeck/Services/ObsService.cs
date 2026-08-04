using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    public class ObsConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 4455;
        public string? Password { get; set; }
    }

    /// <summary>
    /// Cliente obs-websocket v5 (embutido no OBS 28+). Conecta no OBS local, troca cenas,
    /// grava/transmite, câmera virtual, replay, muta fontes — e reflete o estado ao vivo.
    /// Mesma ideia do DiscordRpcService, mas sobre WebSocket em vez de named pipe.
    /// </summary>
    public class ObsService
    {
        private readonly ILogger<ObsService> _logger;
        private readonly IHubContext<DeckHub> _hub;

        private ClientWebSocket? _ws;
        private CancellationTokenSource? _readCts;
        private TaskCompletionSource<bool>? _identifiedTcs;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        public bool Connected { get; private set; }
        public string? CurrentScene { get; private set; }
        public bool Recording { get; private set; }
        public bool Streaming { get; private set; }
        public bool VirtualCam { get; private set; }
        public bool ReplayBuffer { get; private set; }
        private volatile List<string> _scenes = new();
        private volatile List<object> _audioInputs = new();

        public ObsService(ILogger<ObsService> logger, IHubContext<DeckHub> hub)
        {
            _logger = logger;
            _hub = hub;
        }

        // ---- Config em %LocalAppData%\DroidDeck\obs.json ----
        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroidDeck");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "obs.json");
            }
        }

        public ObsConfig LoadConfig()
        {
            try { if (File.Exists(ConfigPath)) return JsonSerializer.Deserialize<ObsConfig>(File.ReadAllText(ConfigPath)) ?? new ObsConfig(); }
            catch { }
            return new ObsConfig();
        }

        /// <summary>True se o usuário já configurou o OBS alguma vez (usado pelo auto-connect).</summary>
        public bool HasSavedConfig() => File.Exists(ConfigPath);

        public void SaveConfig(ObsConfig c)
        {
            try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(c)); } catch { }
        }

        public object GetStatePayload() => new
        {
            connected = Connected,
            currentScene = CurrentScene,
            recording = Recording,
            streaming = Streaming,
            virtualCam = VirtualCam,
            replayBuffer = ReplayBuffer,
            scenes = _scenes,
            audioInputs = _audioInputs,
        };

        // ---- Conexão + handshake (Hello -> Identify -> Identified) ----
        // Serializado por _connectLock: o watchdog (ObsAutoConnect) e o "Conectar"
        // manual podem disparar ao mesmo tempo, e dois connects concorrentes corromperiam o _ws.
        public async Task ConnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                var cfg = LoadConfig();
                Disconnect();

                _ws = new ClientWebSocket();
                var uri = new Uri($"ws://{cfg.Host}:{cfg.Port}");
                using (var connectCts = new CancellationTokenSource(5000))
                    await _ws.ConnectAsync(uri, connectCts.Token);

                // Mesmo padrão do DiscordRpcService: o CTS é descartado pelo próprio loop ao
                // terminar (último usuário do token). Antes cada reconexão vazava um CTS.
                var readCts = new CancellationTokenSource();
                _readCts = readCts;
                _identifiedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = Task.Run(async () =>
                {
                    try { await ReadLoopAsync(readCts.Token); }
                    finally { readCts.Dispose(); }
                });

                await Task.WhenAny(_identifiedTcs.Task, Task.Delay(6000));
                if (!_identifiedTcs.Task.IsCompletedSuccessfully)
                    throw new Exception("OBS não respondeu (handshake). O obs-websocket está ativo?");

                Connected = true;
                await RefreshStateAsync();
                _logger.LogInformation("OBS conectado (cena={Scene}, rec={Rec}, live={Live})", CurrentScene, Recording, Streaming);
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private async Task HandleHelloAsync(JsonElement d)
        {
            // Chamado como fire-and-forget do read loop. Se algo falhar (ex.: JSON de auth
            // sem challenge/salt), NAO deixa a excecao subir sem observador: sinaliza o TCS
            // pra ConnectAsync falhar na hora em vez de esperar o timeout de 6s.
            try
            {
                var cfg = LoadConfig();
                var identify = new Dictionary<string, object?> { ["rpcVersion"] = 1 };

                if (d.TryGetProperty("authentication", out var auth) && auth.ValueKind == JsonValueKind.Object
                    && auth.TryGetProperty("challenge", out var chEl) && auth.TryGetProperty("salt", out var saltEl))
                {
                    var challenge = chEl.GetString() ?? "";
                    var salt = saltEl.GetString() ?? "";
                    identify["authentication"] = ComputeAuth(cfg.Password ?? "", salt, challenge);
                }
                await SendAsync(new { op = 1, d = identify });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OBS: falha no handshake (Hello)");
                _identifiedTcs?.TrySetException(ex);
            }
        }

        private static string ComputeAuth(string password, string salt, string challenge)
        {
            using var sha = SHA256.Create();
            var secret = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password + salt)));
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(secret + challenge)));
        }

        // ---- Estado: lê tudo de uma vez (após conectar / sob demanda) ----
        public async Task RefreshStateAsync()
        {
            try
            {
                var sl = await RequestAsync("GetSceneList");
                if (sl.TryGetProperty("currentProgramSceneName", out var cs)) CurrentScene = cs.GetString();
                var scenes = new List<string>();
                if (sl.TryGetProperty("scenes", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var s in arr.EnumerateArray())
                        if (s.TryGetProperty("sceneName", out var n)) scenes.Add(n.GetString() ?? "");
                _scenes = scenes;

                Recording = await BoolReq("GetRecordStatus", "outputActive");
                Streaming = await BoolReq("GetStreamStatus", "outputActive");
                VirtualCam = await BoolReq("GetVirtualCamStatus", "outputActive");
                ReplayBuffer = await BoolReq("GetReplayBufferStatus", "outputActive");

                var il = await RequestAsync("GetInputList");
                var inputs = new List<object>();
                if (il.TryGetProperty("inputs", out var ia) && ia.ValueKind == JsonValueKind.Array)
                    foreach (var i in ia.EnumerateArray())
                    {
                        var kind = i.TryGetProperty("inputKind", out var k) ? k.GetString() ?? "" : "";
                        // só fontes com áudio (mic/desktop/captura)
                        if (kind.Contains("audio") || kind.Contains("wasapi") || kind.Contains("capture"))
                            inputs.Add(new { name = i.TryGetProperty("inputName", out var nn) ? nn.GetString() : null });
                    }
                _audioInputs = inputs;
            }
            catch (Exception ex) { _logger.LogWarning("OBS RefreshState: {Msg}", ex.Message); }
            await BroadcastAsync();
        }

        private async Task<bool> BoolReq(string req, string prop)
        {
            try { var r = await RequestAsync(req); return r.TryGetProperty(prop, out var v) && (v.ValueKind == JsonValueKind.True); }
            catch { return false; }
        }

        // ---- Ações ----
        public Task SetSceneAsync(string scene) => RequestVoid("SetCurrentProgramScene", new { sceneName = scene });
        public Task ToggleRecordAsync() => RequestVoid("ToggleRecord");
        public Task StartRecordAsync() => RequestVoid("StartRecord");
        public Task StopRecordAsync() => RequestVoid("StopRecord");
        public Task ToggleStreamAsync() => RequestVoid("ToggleStream");
        public Task ToggleVirtualCamAsync() => RequestVoid("ToggleVirtualCam");
        public Task ToggleReplayBufferAsync() => RequestVoid("ToggleReplayBuffer");
        public Task SaveReplayAsync() => RequestVoid("SaveReplayBuffer");
        public Task ToggleInputMuteAsync(string input) => RequestVoid("ToggleInputMute", new { inputName = input });

        public async Task<object> GetScenesAsync() { EnsureConnected(); await RefreshStateAsync(); return _scenes; }
        public async Task<object> GetAudioInputsAsync() { EnsureConnected(); await RefreshStateAsync(); return _audioInputs; }

        private void EnsureConnected()
        {
            if (!Connected || _ws is not { State: WebSocketState.Open })
                throw new InvalidOperationException("OBS não está conectado.");
        }

        // ---- WebSocket: request/resposta com requestId, leitura e eventos ----
        private async Task<JsonElement> RequestAsync(string type, object? data = null, int timeoutMs = 8000)
        {
            EnsureConnected();
            var id = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

            var req = new Dictionary<string, object?> { ["requestType"] = type, ["requestId"] = id };
            if (data != null) req["requestData"] = data;
            await SendAsync(new { op = 6, d = req });

            using var cts = new CancellationTokenSource(timeoutMs);
            await using var reg = cts.Token.Register(() => { if (_pending.TryRemove(id, out var t)) t.TrySetException(new TimeoutException($"OBS '{type}' expirou.")); });
            return await tcs.Task;
        }

        private async Task RequestVoid(string type, object? data = null) { await RequestAsync(type, data); }

        private async Task SendAsync(object payload)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            await _writeLock.WaitAsync();
            try { await _ws!.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
            finally { _writeLock.Release(); }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var buf = new byte[16384];
            var sb = new StringBuilder();
            try
            {
                while (!ct.IsCancellationRequested && _ws is { State: WebSocketState.Open })
                {
                    sb.Clear();
                    WebSocketReceiveResult res;
                    do
                    {
                        res = await _ws.ReceiveAsync(buf, ct);
                        if (res.MessageType == WebSocketMessageType.Close)
                            throw new IOException($"OBS fechou (code={(int?)_ws.CloseStatus}, motivo='{_ws.CloseStatusDescription}').");
                        sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
                    } while (!res.EndOfMessage);

                    HandleMessage(sb.ToString());
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("OBS desconectado: {Msg}", ex.Message);
                Connected = false;
                await BroadcastAsync();
            }
        }

        private void HandleMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("op", out var opEl)) return;
                int op = opEl.GetInt32();
                var d = root.TryGetProperty("d", out var dEl) ? dEl : default;

                switch (op)
                {
                    case 0: // Hello
                        _ = HandleHelloAsync(d.Clone());
                        break;
                    case 2: // Identified
                        _identifiedTcs?.TrySetResult(true);
                        break;
                    case 5: // Event
                        HandleEvent(d);
                        break;
                    case 7: // RequestResponse
                        if (d.TryGetProperty("requestId", out var rid) && _pending.TryRemove(rid.GetString()!, out var tcs))
                        {
                            bool ok = d.TryGetProperty("requestStatus", out var st) && st.TryGetProperty("result", out var rs) && rs.ValueKind == JsonValueKind.True;
                            if (ok) tcs.TrySetResult(d.TryGetProperty("responseData", out var rd) ? rd.Clone() : default);
                            else tcs.TrySetException(new Exception($"OBS recusou o pedido."));
                        }
                        break;
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "OBS: mensagem inválida"); }
        }

        private void HandleEvent(JsonElement d)
        {
            if (!d.TryGetProperty("eventType", out var etEl)) return;
            var evt = etEl.GetString();
            var data = d.TryGetProperty("eventData", out var ed) ? ed : default;

            switch (evt)
            {
                case "CurrentProgramSceneChanged":
                    if (data.TryGetProperty("sceneName", out var sn)) CurrentScene = sn.GetString();
                    break;
                case "RecordStateChanged":
                    if (data.TryGetProperty("outputActive", out var ra)) Recording = ra.GetBoolean();
                    break;
                case "StreamStateChanged":
                    if (data.TryGetProperty("outputActive", out var sa)) Streaming = sa.GetBoolean();
                    break;
                case "VirtualcamStateChanged":
                    if (data.TryGetProperty("outputActive", out var va)) VirtualCam = va.GetBoolean();
                    break;
                case "ReplayBufferStateChanged":
                    if (data.TryGetProperty("outputActive", out var pa)) ReplayBuffer = pa.GetBoolean();
                    break;
                case "SceneListChanged":
                    _ = RefreshStateAsync();
                    return;
                default:
                    return;
            }
            _ = BroadcastAsync();
        }

        private async Task BroadcastAsync()
        {
            try { await _hub.Clients.All.SendAsync("ReceiveObsState", GetStatePayload()); } catch { }
        }

        public void Disconnect()
        {
            try { _readCts?.Cancel(); } catch { }
            try { _ws?.Dispose(); } catch { }
            _ws = null;
            Connected = false;
            CurrentScene = null;
            Recording = Streaming = VirtualCam = ReplayBuffer = false;
            foreach (var kv in _pending) kv.Value.TrySetCanceled();
            _pending.Clear();
        }
    }
}
