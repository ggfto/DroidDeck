using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    public class DiscordConfig
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }

    /// <summary>
    /// Cliente Discord RPC (IPC via named pipe). Controla mute/deafen do próprio
    /// usuário e reflete o estado ao vivo. Requer um app no Discord Developer Portal
    /// (Client ID + Secret) e redirect http://localhost:4787/discord. Funciona p/ o dono do app.
    /// </summary>
    public class DiscordRpcService
    {
        private const string RedirectUri = "http://localhost:4787/discord";
        private static readonly string[] Scopes = { "rpc", "rpc.voice.read", "rpc.voice.write" };

        private readonly ILogger<DiscordRpcService> _logger;
        private readonly IHubContext<DeckHub> _hub;

        private NamedPipeClientStream? _pipe;
        private CancellationTokenSource? _readCts;
        private TaskCompletionSource<bool>? _readyTcs;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly SemaphoreSlim _connectLock = new(1, 1); // serializa watchdog + Conectar manual

        public bool Connected { get; private set; }
        public bool SelfMute { get; private set; }
        public bool SelfDeaf { get; private set; }
        public string? VoiceChannelId { get; private set; }
        public string? VoiceChannelName { get; private set; }
        public int InputVolume { get; private set; }
        public int OutputVolume { get; private set; }
        public string VoiceMode { get; private set; } = ""; // PUSH_TO_TALK | VOICE_ACTIVITY
        private volatile List<object> _participants = new();
        private readonly ConcurrentDictionary<string, bool> _userMute = new();

        /// <summary>True quando há Client ID + Secret salvos (app Discord configurado).</summary>
        public bool Configured
        {
            get
            {
                var c = LoadConfig();
                return !string.IsNullOrWhiteSpace(c.ClientId) &&
                       !string.IsNullOrWhiteSpace(c.ClientSecret);
            }
        }

        /// <summary>Estado completo do Discord enviado aos clientes (SignalR/REST).</summary>
        public object GetStatePayload() => new
        {
            configured = Configured,
            connected = Connected,
            mute = SelfMute,
            deaf = SelfDeaf,
            channelId = VoiceChannelId,
            channelName = VoiceChannelName,
            inputVolume = InputVolume,
            outputVolume = OutputVolume,
            voiceMode = VoiceMode,
            participants = _participants,
        };

        public DiscordRpcService(ILogger<DiscordRpcService> logger, IHubContext<DeckHub> hub)
        {
            _logger = logger;
            _hub = hub;
        }

        // ---- Config persistida em %LocalAppData%\DroidDeck\discord.json ----
        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroidDeck");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "discord.json");
            }
        }

        public DiscordConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<DiscordConfig>(File.ReadAllText(ConfigPath)) ?? new DiscordConfig();
            }
            catch { }
            return new DiscordConfig();
        }

        public void SaveConfig(DiscordConfig c)
        {
            try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(c)); } catch { }
        }

        public void SetCredentials(string clientId, string clientSecret)
        {
            var cfg = LoadConfig();
            cfg.ClientId = clientId;
            cfg.ClientSecret = clientSecret;
            cfg.AccessToken = null;  // força reautorizar com as novas credenciais
            cfg.RefreshToken = null;
            SaveConfig(cfg);
        }

        // ---- Conexão + autenticação ----
        public async Task ConnectAsync(bool interactive = true)
        {
            await _connectLock.WaitAsync();
            try { await ConnectCoreAsync(interactive); }
            finally { _connectLock.Release(); }
        }

        private async Task ConnectCoreAsync(bool interactive)
        {
            var cfg = LoadConfig();
            if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
                throw new InvalidOperationException("Configure o Client ID e o Secret do Discord primeiro.");

            Disconnect();
            await ConnectPipeAsync();
            _readCts = new CancellationTokenSource();
            _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Task.Run(() => ReadLoopAsync(_readCts.Token));

            _logger.LogInformation("Discord: enviando handshake (client_id={Id})", cfg.ClientId);
            await WriteFrameAsync(0, new { v = 1, client_id = cfg.ClientId }); // handshake
            await Task.WhenAny(_readyTcs.Task, Task.Delay(10000));
            if (!_readyTcs.Task.IsCompletedSuccessfully)
                throw new Exception("Discord não respondeu ao handshake (READY).");

            bool authed = false;
            if (!string.IsNullOrEmpty(cfg.AccessToken))
            {
                try { await SendCommandAsync("AUTHENTICATE", new { access_token = cfg.AccessToken }); authed = true; }
                catch { authed = false; }
            }
            // Token expirado (Discord expira em ~7 dias)? Renova SEM popup usando o
            // refresh_token salvo. Isso faz a auto-conexão voltar a funcionar sozinha em
            // vez de exigir reautorização manual toda semana.
            if (!authed && !string.IsNullOrEmpty(cfg.RefreshToken))
            {
                try
                {
                    if (await RefreshAccessTokenAsync(cfg))
                    {
                        await SendCommandAsync("AUTHENTICATE", new { access_token = cfg.AccessToken });
                        authed = true;
                    }
                }
                catch (Exception ex) { _logger.LogDebug(ex, "Discord: refresh de token falhou"); }
            }
            if (!authed)
            {
                // Auto-conexão (startup) nunca abre popup; só reusa/renova o token salvo.
                if (!interactive)
                    throw new InvalidOperationException("Sem token válido para auto-conexão.");
                await AuthorizeAndGetTokenAsync(cfg); // salva access+refresh internamente
                await SendCommandAsync("AUTHENTICATE", new { access_token = cfg.AccessToken });
            }

            await SendCommandAsync("SUBSCRIBE", null, evt: "VOICE_SETTINGS_UPDATE");
            try { await SendCommandAsync("SUBSCRIBE", null, evt: "VOICE_CHANNEL_SELECT"); } catch { }
            var vs = await SendCommandAsync("GET_VOICE_SETTINGS", null);
            if (vs.TryGetProperty("data", out var data)) UpdateVoiceFromData(data);

            Connected = true;
            await RefreshVoiceChannelAsync(); // canal atual + participantes (também faz o broadcast)
            _logger.LogInformation("Discord RPC conectado (mute={Mute}, deaf={Deaf}, canal={Ch})",
                SelfMute, SelfDeaf, VoiceChannelName);
        }

        private async Task AuthorizeAndGetTokenAsync(DiscordConfig cfg)
        {
            // O usuário precisa APROVAR no cliente Discord (timeout maior).
            // No fluxo RPC o redirect_uri NÃO vai nos args (mas o app precisa ter um
            // Redirect URI cadastrado no portal; usamos o mesmo na troca de token).
            var resp = await SendCommandAsync("AUTHORIZE",
                new { client_id = cfg.ClientId, scopes = Scopes }, timeoutMs: 60000);
            var code = resp.GetProperty("data").GetProperty("code").GetString()!;

            await ExchangeTokenAsync(cfg, new Dictionary<string, string>
            {
                ["client_id"] = cfg.ClientId!,
                ["client_secret"] = cfg.ClientSecret!,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
            });
        }

        /// <summary>Renova o access_token com o refresh_token salvo (sem popup).</summary>
        private Task<bool> RefreshAccessTokenAsync(DiscordConfig cfg) =>
            ExchangeTokenAsync(cfg, new Dictionary<string, string>
            {
                ["client_id"] = cfg.ClientId!,
                ["client_secret"] = cfg.ClientSecret!,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = cfg.RefreshToken!,
            });

        /// <summary>
        /// Troca no endpoint OAuth2 do Discord (authorization_code ou refresh_token).
        /// Salva access_token E refresh_token em cfg (persistidos), pra próxima renovação
        /// não precisar de popup. Retorna true se veio um access_token válido.
        /// </summary>
        private async Task<bool> ExchangeTokenAsync(DiscordConfig cfg, Dictionary<string, string> form)
        {
            using var http = new HttpClient();
            var tokenResp = await http.PostAsync("https://discord.com/api/oauth2/token",
                new FormUrlEncodedContent(form));
            var body = await tokenResp.Content.ReadAsStringAsync();
            if (!tokenResp.IsSuccessStatusCode)
                throw new Exception($"Troca de token falhou ({(int)tokenResp.StatusCode}): {body}");

            var root = JsonDocument.Parse(body).RootElement;
            cfg.AccessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            if (root.TryGetProperty("refresh_token", out var rt)) cfg.RefreshToken = rt.GetString();
            SaveConfig(cfg);
            return !string.IsNullOrEmpty(cfg.AccessToken);
        }

        // ---- Comandos de voz ----
        public async Task SetMuteAsync(bool? mute)
        {
            EnsureConnected();
            var target = mute ?? !SelfMute;
            await SendCommandAsync("SET_VOICE_SETTINGS", new { mute = target });
            SelfMute = target;
            await BroadcastStateAsync();
        }

        public async Task SetDeafAsync(bool? deaf)
        {
            EnsureConnected();
            var target = deaf ?? !SelfDeaf;
            await SendCommandAsync("SET_VOICE_SETTINGS", new { deaf = target });
            SelfDeaf = target;
            await BroadcastStateAsync();
        }

        // ---- Servidores / canais de voz ----
        public async Task<object> GetGuildsAsync()
        {
            EnsureConnected();
            var r = await SendCommandAsync("GET_GUILDS", null);
            var list = new List<object>();
            if (r.TryGetProperty("data", out var d) && d.TryGetProperty("guilds", out var gs)
                && gs.ValueKind == JsonValueKind.Array)
                foreach (var g in gs.EnumerateArray())
                    list.Add(new
                    {
                        id = g.TryGetProperty("id", out var id) ? id.GetString() : null,
                        name = g.TryGetProperty("name", out var n) ? n.GetString() : null,
                    });
            return list;
        }

        public async Task<object> GetChannelsAsync(string guildId)
        {
            EnsureConnected();
            var r = await SendCommandAsync("GET_CHANNELS", new { guild_id = guildId });
            var list = new List<object>();
            if (r.TryGetProperty("data", out var d) && d.TryGetProperty("channels", out var cs)
                && cs.ValueKind == JsonValueKind.Array)
                foreach (var c in cs.EnumerateArray())
                {
                    // type 2 = GUILD_VOICE, 13 = STAGE_VOICE
                    int type = c.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Number
                        ? t.GetInt32() : -1;
                    if (type != 2 && type != 13) continue;
                    list.Add(new
                    {
                        id = c.TryGetProperty("id", out var id) ? id.GetString() : null,
                        name = c.TryGetProperty("name", out var n) ? n.GetString() : null,
                    });
                }
            return list;
        }

        /// <summary>Entra no canal (ou desconecta se channelId for nulo/vazio).</summary>
        public async Task SelectVoiceChannelAsync(string? channelId)
        {
            EnsureConnected();
            await SendCommandAsync("SELECT_VOICE_CHANNEL",
                new { channel_id = string.IsNullOrEmpty(channelId) ? null : channelId, force = true });
            await RefreshVoiceChannelAsync();
        }

        /// <summary>Lê o canal de voz atual e os participantes; atualiza estado + broadcast.</summary>
        public async Task RefreshVoiceChannelAsync()
        {
            try
            {
                var r = await SendCommandAsync("GET_SELECTED_VOICE_CHANNEL", null);
                if (r.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
                {
                    VoiceChannelId = d.TryGetProperty("id", out var id) ? id.GetString() : null;
                    VoiceChannelName = d.TryGetProperty("name", out var nm) ? nm.GetString() : null;
                    var parts = new List<object>();
                    if (d.TryGetProperty("voice_states", out var vss) && vss.ValueKind == JsonValueKind.Array)
                        foreach (var v in vss.EnumerateArray())
                        {
                            if (!v.TryGetProperty("user", out var u)) continue;
                            parts.Add(new
                            {
                                id = u.TryGetProperty("id", out var uid) ? uid.GetString() : null,
                                username = u.TryGetProperty("username", out var un) ? un.GetString() : "",
                                nick = v.TryGetProperty("nick", out var nk) ? nk.GetString() : null,
                            });
                        }
                    _participants = parts;
                }
                else
                {
                    VoiceChannelId = null;
                    VoiceChannelName = null;
                    _participants = new();
                }
            }
            catch
            {
                VoiceChannelId = null;
                VoiceChannelName = null;
                _participants = new();
            }
            await BroadcastStateAsync();
        }

        // ---- Volume de entrada (mic, 0-100) e saída (0-200) ----
        public async Task SetInputVolumeAsync(double volume)
        {
            EnsureConnected();
            volume = Math.Clamp(volume, 0, 100);
            var r = await SendCommandAsync("SET_VOICE_SETTINGS", new { input = new { volume } });
            if (r.TryGetProperty("data", out var d)) UpdateVoiceFromData(d);
        }

        public async Task SetOutputVolumeAsync(double volume)
        {
            EnsureConnected();
            volume = Math.Clamp(volume, 0, 200);
            var r = await SendCommandAsync("SET_VOICE_SETTINGS", new { output = new { volume } });
            if (r.TryGetProperty("data", out var d)) UpdateVoiceFromData(d);
        }

        public Task NudgeInputVolumeAsync(double delta) => SetInputVolumeAsync(InputVolume + delta);
        public Task NudgeOutputVolumeAsync(double delta) => SetOutputVolumeAsync(OutputVolume + delta);

        // ---- Modo de voz: PUSH_TO_TALK | VOICE_ACTIVITY ----
        public async Task SetVoiceModeAsync(string type)
        {
            EnsureConnected();
            var r = await SendCommandAsync("SET_VOICE_SETTINGS", new { mode = new { type } });
            if (r.TryGetProperty("data", out var d)) UpdateVoiceFromData(d);
        }

        public Task ToggleVoiceModeAsync() =>
            SetVoiceModeAsync(VoiceMode == "PUSH_TO_TALK" ? "VOICE_ACTIVITY" : "PUSH_TO_TALK");

        // ---- Por usuário (mute/volume local na call) ----
        public async Task SetUserVoiceAsync(string userId, bool? mute, double? volume)
        {
            EnsureConnected();
            var args = new Dictionary<string, object?> { ["user_id"] = userId };
            if (mute.HasValue) args["mute"] = mute.Value;
            if (volume.HasValue) args["volume"] = Math.Clamp(volume.Value, 0, 200);
            await SendCommandAsync("SET_USER_VOICE_SETTINGS", args);
            if (mute.HasValue) _userMute[userId] = mute.Value;
        }

        public Task ToggleUserMuteAsync(string userId) =>
            SetUserVoiceAsync(userId, !(_userMute.TryGetValue(userId, out var m) && m), null);

        private void EnsureConnected()
        {
            if (!Connected || _pipe is not { IsConnected: true })
                throw new InvalidOperationException("Discord não está conectado.");
        }

        // ---- IPC: pipe, framing, read loop ----
        private async Task ConnectPipeAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var p = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                        PipeDirection.InOut, PipeOptions.Asynchronous);
                    await p.ConnectAsync(1000);
                    _pipe = p;
                    return;
                }
                catch { /* tenta o próximo */ }
            }
            throw new IOException("Pipe do Discord não encontrado — o Discord está aberto?");
        }

        private Task WriteFrameAsync(int opcode, object payload)
            => WriteRawFrameAsync(opcode, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));

        private async Task WriteRawFrameAsync(int opcode, byte[] data)
        {
            var frame = new byte[8 + data.Length];
            BitConverter.GetBytes(opcode).CopyTo(frame, 0);      // little-endian (Windows)
            BitConverter.GetBytes(data.Length).CopyTo(frame, 4);
            data.CopyTo(frame, 8);
            await _writeLock.WaitAsync();
            try
            {
                await _pipe!.WriteAsync(frame);
                await _pipe!.FlushAsync();
            }
            finally { _writeLock.Release(); }
        }

        private async Task<JsonElement> SendCommandAsync(string cmd, object? args, string? evt = null, int timeoutMs = 10000)
        {
            var nonce = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[nonce] = tcs;

            var payload = new Dictionary<string, object?> { ["cmd"] = cmd, ["nonce"] = nonce };
            if (args != null) payload["args"] = args;
            if (evt != null) payload["evt"] = evt;
            await WriteFrameAsync(1, payload);

            using var cts = new CancellationTokenSource(timeoutMs);
            await using var reg = cts.Token.Register(() =>
            {
                if (_pending.TryRemove(nonce, out var t))
                    t.TrySetException(new TimeoutException($"Discord '{cmd}' expirou."));
            });
            return await tcs.Task;
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var header = new byte[8];
            try
            {
                while (!ct.IsCancellationRequested && _pipe is { IsConnected: true })
                {
                    await ReadExactAsync(header, 8, ct);
                    int opcode = BitConverter.ToInt32(header, 0);
                    int len = BitConverter.ToInt32(header, 4);
                    var buf = new byte[len];
                    await ReadExactAsync(buf, len, ct);

                    // Debug: frames recebidos (útil pra diagnosticar; fica fora do Info
                    // pra não spammar nem logar dados de usuário na operação normal).
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Discord IPC recv op={Op} len={Len}: {Body}",
                            opcode, len, len <= 600 ? Encoding.UTF8.GetString(buf) : "(payload grande)");

                    // Opcodes IPC do Discord: 0=HANDSHAKE, 1=FRAME, 2=CLOSE, 3=PING, 4=PONG.
                    // Antes tudo era tratado como FRAME — o PING nunca era respondido, então
                    // o Discord fechava conexões ociosas e o deck "parava de reagir".
                    switch (opcode)
                    {
                        case 3: // PING → devolver PONG com o MESMO payload (mantém a conexão viva)
                            await WriteRawFrameAsync(4, buf);
                            break;
                        case 2: // CLOSE → o Discord está encerrando; derruba limpo (dispara reconexão)
                            _logger.LogWarning("Discord RPC CLOSE: {Msg}", Encoding.UTF8.GetString(buf));
                            throw new IOException("Discord fechou a conexão (CLOSE).");
                        case 4: // PONG (resposta a um PING nosso, se houver) → ignora
                            break;
                        default: // 0=HANDSHAKE (resposta), 1=FRAME
                            HandleFrame(Encoding.UTF8.GetString(buf));
                            break;
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Discord RPC desconectado: {Msg}", ex.Message);
                Connected = false;
                await BroadcastStateAsync();
            }
        }

        private async Task ReadExactAsync(byte[] buf, int count, CancellationToken ct)
        {
            int read = 0;
            while (read < count)
            {
                int n = await _pipe!.ReadAsync(buf.AsMemory(read, count - read), ct);
                if (n <= 0) throw new IOException("Pipe fechado.");
                read += n;
            }
        }

        private void HandleFrame(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("nonce", out var nonceEl) &&
                    nonceEl.ValueKind == JsonValueKind.String &&
                    _pending.TryRemove(nonceEl.GetString()!, out var tcs))
                {
                    if (root.TryGetProperty("evt", out var e) && e.GetString() == "ERROR")
                    {
                        var msg = root.TryGetProperty("data", out var d) && d.TryGetProperty("message", out var m)
                            ? m.GetString() : "erro RPC";
                        tcs.TrySetException(new Exception($"Discord: {msg}"));
                    }
                    else tcs.TrySetResult(root.Clone());
                    return;
                }

                if (root.TryGetProperty("evt", out var evtEl))
                {
                    var evt = evtEl.GetString();
                    if (evt == "READY") _readyTcs?.TrySetResult(true);
                    else if (evt == "VOICE_SETTINGS_UPDATE" && root.TryGetProperty("data", out var vd))
                        UpdateVoiceFromData(vd);
                    else if (evt == "VOICE_CHANNEL_SELECT")
                        _ = RefreshVoiceChannelAsync();
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Discord: frame inválido"); }
        }

        private void UpdateVoiceFromData(JsonElement data)
        {
            if (data.TryGetProperty("mute", out var m) && (m.ValueKind == JsonValueKind.True || m.ValueKind == JsonValueKind.False))
                SelfMute = m.GetBoolean();
            if (data.TryGetProperty("deaf", out var d) && (d.ValueKind == JsonValueKind.True || d.ValueKind == JsonValueKind.False))
                SelfDeaf = d.GetBoolean();
            if (data.TryGetProperty("input", out var inp) && inp.TryGetProperty("volume", out var iv) && iv.ValueKind == JsonValueKind.Number)
                InputVolume = (int)Math.Round(iv.GetDouble());
            if (data.TryGetProperty("output", out var outp) && outp.TryGetProperty("volume", out var ov) && ov.ValueKind == JsonValueKind.Number)
                OutputVolume = (int)Math.Round(ov.GetDouble());
            if (data.TryGetProperty("mode", out var mode) && mode.TryGetProperty("type", out var mt) && mt.ValueKind == JsonValueKind.String)
                VoiceMode = mt.GetString() ?? "";
            _ = BroadcastStateAsync();
        }

        private async Task BroadcastStateAsync()
        {
            try
            {
                await _hub.Clients.All.SendAsync("ReceiveDiscordState", GetStatePayload());
            }
            catch { }
        }

        public void Disconnect()
        {
            try { _readCts?.Cancel(); } catch { }
            try { _pipe?.Dispose(); } catch { }
            _pipe = null;
            Connected = false;
            VoiceChannelId = null;
            VoiceChannelName = null;
            _participants = new();
            foreach (var kv in _pending) kv.Value.TrySetCanceled();
            _pending.Clear();
        }
    }
}
