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
using AnyDeck.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AnyDeck.Services
{
    public class DiscordConfig
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? AccessToken { get; set; }
    }

    /// <summary>
    /// Cliente Discord RPC (IPC via named pipe). Controla mute/deafen do próprio
    /// usuário e reflete o estado ao vivo. Requer um app no Discord Developer Portal
    /// (Client ID + Secret) e redirect http://localhost:5000/discord. Funciona p/ o dono do app.
    /// </summary>
    public class DiscordRpcService
    {
        private const string RedirectUri = "http://localhost:5000/discord";
        private static readonly string[] Scopes = { "rpc", "rpc.voice.read", "rpc.voice.write" };

        private readonly ILogger<DiscordRpcService> _logger;
        private readonly IHubContext<DeckHub> _hub;

        private NamedPipeClientStream? _pipe;
        private CancellationTokenSource? _readCts;
        private TaskCompletionSource<bool>? _readyTcs;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public bool Connected { get; private set; }
        public bool SelfMute { get; private set; }
        public bool SelfDeaf { get; private set; }

        public DiscordRpcService(ILogger<DiscordRpcService> logger, IHubContext<DeckHub> hub)
        {
            _logger = logger;
            _hub = hub;
        }

        // ---- Config persistida em %LocalAppData%\AnyDeck\discord.json ----
        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnyDeck");
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
            cfg.AccessToken = null; // força reautorizar com as novas credenciais
            SaveConfig(cfg);
        }

        // ---- Conexão + autenticação ----
        public async Task ConnectAsync()
        {
            var cfg = LoadConfig();
            if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
                throw new InvalidOperationException("Configure o Client ID e o Secret do Discord primeiro.");

            Disconnect();
            await ConnectPipeAsync();
            _readCts = new CancellationTokenSource();
            _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Task.Run(() => ReadLoopAsync(_readCts.Token));

            await WriteFrameAsync(0, new { v = 1, client_id = cfg.ClientId }); // handshake
            await Task.WhenAny(_readyTcs.Task, Task.Delay(5000));
            if (!_readyTcs.Task.IsCompletedSuccessfully)
                throw new Exception("Discord não respondeu ao handshake (READY).");

            bool authed = false;
            if (!string.IsNullOrEmpty(cfg.AccessToken))
            {
                try { await SendCommandAsync("AUTHENTICATE", new { access_token = cfg.AccessToken }); authed = true; }
                catch { authed = false; }
            }
            if (!authed)
            {
                var token = await AuthorizeAndGetTokenAsync(cfg);
                cfg.AccessToken = token;
                SaveConfig(cfg);
                await SendCommandAsync("AUTHENTICATE", new { access_token = token });
            }

            await SendCommandAsync("SUBSCRIBE", null, evt: "VOICE_SETTINGS_UPDATE");
            var vs = await SendCommandAsync("GET_VOICE_SETTINGS", null);
            if (vs.TryGetProperty("data", out var data)) UpdateVoiceFromData(data);

            Connected = true;
            await BroadcastStateAsync();
            _logger.LogInformation("Discord RPC conectado (mute={Mute}, deaf={Deaf})", SelfMute, SelfDeaf);
        }

        private async Task<string> AuthorizeAndGetTokenAsync(DiscordConfig cfg)
        {
            // O usuário precisa APROVAR no cliente Discord (timeout maior).
            // No fluxo RPC o redirect_uri NÃO vai nos args (mas o app precisa ter um
            // Redirect URI cadastrado no portal; usamos o mesmo na troca de token).
            var resp = await SendCommandAsync("AUTHORIZE",
                new { client_id = cfg.ClientId, scopes = Scopes }, timeoutMs: 60000);
            var code = resp.GetProperty("data").GetProperty("code").GetString()!;

            using var http = new HttpClient();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = cfg.ClientId!,
                ["client_secret"] = cfg.ClientSecret!,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
            });
            var tokenResp = await http.PostAsync("https://discord.com/api/oauth2/token", form);
            var body = await tokenResp.Content.ReadAsStringAsync();
            if (!tokenResp.IsSuccessStatusCode)
                throw new Exception($"Troca de token falhou ({(int)tokenResp.StatusCode}): {body}");
            return JsonDocument.Parse(body).RootElement.GetProperty("access_token").GetString()!;
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

        private async Task WriteFrameAsync(int opcode, object payload)
        {
            var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
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
                    int len = BitConverter.ToInt32(header, 4);
                    var buf = new byte[len];
                    await ReadExactAsync(buf, len, ct);
                    HandleFrame(Encoding.UTF8.GetString(buf));
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
            _ = BroadcastStateAsync();
        }

        private async Task BroadcastStateAsync()
        {
            try
            {
                await _hub.Clients.All.SendAsync("ReceiveDiscordState",
                    new { connected = Connected, mute = SelfMute, deaf = SelfDeaf });
            }
            catch { }
        }

        public void Disconnect()
        {
            try { _readCts?.Cancel(); } catch { }
            try { _pipe?.Dispose(); } catch { }
            _pipe = null;
            Connected = false;
            foreach (var kv in _pending) kv.Value.TrySetCanceled();
            _pending.Clear();
        }
    }
}
