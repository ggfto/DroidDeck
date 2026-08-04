using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DroidDeck.Services
{
    /// <summary>
    /// Configuração da soundboard, persistida em %LocalAppData%\DroidDeck\soundboard.json
    /// (mesma convenção do Discord/OBS).
    /// </summary>
    public class SoundboardConfig
    {
        /// <summary>Id (MMDevice.ID) do dispositivo de saída "cabo" — o que vai pro Discord/OBS
        /// (ex.: VB-Cable). Vazio = usa o dispositivo de saída padrão do Windows.</summary>
        public string? CableDeviceId { get; set; }

        /// <summary>Id do dispositivo de "monitor" — pra você ouvir (fone/alto-falante).</summary>
        public string? MonitorDeviceId { get; set; }

        /// <summary>Se true, o som toca também no MonitorDeviceId além do cabo.</summary>
        public bool MonitorEnabled { get; set; } = false;

        /// <summary>Volume 0..100.</summary>
        public int Volume { get; set; } = 100;
    }

    /// <summary>
    /// Resultado de busca no MyInstants (proxiado pelo backend).
    /// </summary>
    public class SoundResult
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Mp3 { get; set; } = "";
    }

    /// <summary>
    /// Soundboard: busca sons no MyInstants (via wrapper JSON não-oficial), baixa/cacheia o MP3
    /// e reproduz no PC num dispositivo de saída escolhido (cabo virtual pro Discord/OBS + monitor
    /// opcional). Toca um som por vez — apertar outro corta o anterior. O roteamento do dispositivo
    /// de saída pro Discord/OBS é responsabilidade do usuário (VB-Cable / Voicemeeter).
    ///
    /// O Discord RPC deste projeto NÃO injeta áudio; por isso a soundboard sai por um dispositivo
    /// de áudio, não pelo Discord.
    /// </summary>
    public class SoundboardService
    {
        private const string ApiBase = "https://myinstants-api.vercel.app";

        private readonly ILogger<SoundboardService> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IHubContext<DeckHub> _hub;

        // Playbacks ativos (cabo + monitor). Protegido por _lock.
        private readonly object _lock = new();
        private readonly List<ActivePlayback> _active = new();

        public string? NowPlayingTitle { get; private set; }

        private sealed class ActivePlayback
        {
            public required IWavePlayer Output { get; init; }
            public required WaveStream Reader { get; init; }

            /// <summary>
            /// Device de saída explícito (null = dispositivo padrão do Windows). O WasapiOut
            /// NÃO descarta o MMDevice que recebe no construtor, então a posse fica aqui e
            /// ele é liberado junto com o Output — senão vaza um proxy COM por som tocado.
            /// </summary>
            public MMDevice? Device { get; init; }
        }

        public SoundboardService(
            ILogger<SoundboardService> logger,
            IHttpClientFactory httpFactory,
            IHubContext<DeckHub> hub)
        {
            _logger = logger;
            _httpFactory = httpFactory;
            _hub = hub;
        }

        // ---- Config em %LocalAppData%\DroidDeck\soundboard.json ----
        private static string BaseDir
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroidDeck");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string ConfigPath => Path.Combine(BaseDir, "soundboard.json");

        private static string CacheDir
        {
            get
            {
                var dir = Path.Combine(BaseDir, "SoundCache");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public SoundboardConfig LoadConfig()
        {
            try { if (File.Exists(ConfigPath)) return JsonSerializer.Deserialize<SoundboardConfig>(File.ReadAllText(ConfigPath)) ?? new SoundboardConfig(); }
            catch { }
            return new SoundboardConfig();
        }

        public void SaveConfig(SoundboardConfig c)
        {
            try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(c)); } catch { }
        }

        // ---- Busca (proxy do MyInstants) ----
        public async Task<List<SoundResult>> SearchAsync(string query)
        {
            return await FetchAsync($"{ApiBase}/search?q={Uri.EscapeDataString(query ?? "")}");
        }

        public async Task<List<SoundResult>> TrendingAsync()
        {
            return await FetchAsync($"{ApiBase}/trending?q=us");
        }

        private async Task<List<SoundResult>> FetchAsync(string url)
        {
            var results = new List<SoundResult>();
            try
            {
                var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var mp3 = GetStr(item, "mp3");
                        if (string.IsNullOrWhiteSpace(mp3)) continue;
                        results.Add(new SoundResult
                        {
                            Id = GetStr(item, "id"),
                            Title = GetStr(item, "title"),
                            Mp3 = mp3,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao buscar sons no MyInstants ({Url})", url);
            }
            return results;
        }

        private static string GetStr(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

        // ---- Cache do MP3 ----
        // SEGURANÇA: o id/url compõe um caminho de arquivo. Deriva-se um nome seguro (só
        // alfanumérico) do id quando válido; senão, um hash da url. Nunca usa a string crua.
        private static bool IsSafeId(string? id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 128) return false;
            foreach (var c in id)
                if (!(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')) return false;
            return true;
        }

        private static string CacheFileFor(string id, string url)
        {
            var name = IsSafeId(id) ? id : Math.Abs(url.GetHashCode()).ToString();
            return Path.Combine(CacheDir, name + ".mp3");
        }

        private async Task<string> EnsureCachedAsync(string id, string url)
        {
            var path = CacheFileFor(id, url);
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            var bytes = await http.GetByteArrayAsync(url);
            var tmp = path + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
            return path;
        }

        // ---- Playback ----
        public async Task PlayAsync(string id, string url, string? title)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("PlayAsync sem url (id={Id})", id);
                return;
            }

            string path;
            try { path = await EnsureCachedAsync(id, url); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao baixar/cachear o som {Url}", url);
                return;
            }

            // Para o que estiver tocando (1 som por vez).
            StopAll(broadcast: false);

            var cfg = LoadConfig();
            float vol = Math.Clamp(cfg.Volume, 0, 100) / 100f;

            var targets = new List<MMDevice?>();
            targets.Add(ResolveDevice(cfg.CableDeviceId)); // null => dispositivo padrão
            if (cfg.MonitorEnabled && !string.IsNullOrWhiteSpace(cfg.MonitorDeviceId))
            {
                var monitor = ResolveDevice(cfg.MonitorDeviceId);
                if (monitor != null) targets.Add(monitor);
            }

            lock (_lock)
            {
                foreach (var device in targets)
                {
                    ActivePlayback? playback = null;
                    try
                    {
                        var reader = new MediaFoundationReader(path);
                        var sample = new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = vol };
                        // WasapiOut em shared mode reamostra automaticamente pro mix format do device.
                        IWavePlayer output = device != null
                            ? new WasapiOut(device, AudioClientShareMode.Shared, false, 200)
                            : new WasapiOut(AudioClientShareMode.Shared, 200);
                        output.Init(sample);
                        // A posse do device passa pro ActivePlayback a partir daqui.
                        playback = new ActivePlayback { Output = output, Reader = reader, Device = device };
                        output.PlaybackStopped += (_, __) => OnPlaybackStopped(playback);
                        output.Play();
                        _active.Add(playback);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao iniciar playback no device {Device}", device?.FriendlyName ?? "(padrão)");
                        // Falhou antes de virar ActivePlayback: ninguém mais liberaria o device.
                        if (playback == null) { try { device?.Dispose(); } catch { } }
                    }
                }
                NowPlayingTitle = _active.Count > 0 ? (title ?? "") : null;
            }

            await BroadcastAsync();
        }

        private MMDevice? ResolveDevice(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            try
            {
                using var en = new MMDeviceEnumerator();
                return en.GetDevice(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dispositivo de saída {Id} não encontrado; usando padrão", id);
                return null;
            }
        }

        private void OnPlaybackStopped(ActivePlayback pb)
        {
            bool becameEmpty = false;
            lock (_lock)
            {
                if (_active.Remove(pb))
                {
                    try { pb.Output.Dispose(); } catch { }
                    try { pb.Reader.Dispose(); } catch { }
                    try { pb.Device?.Dispose(); } catch { }
                }
                if (_active.Count == 0)
                {
                    NowPlayingTitle = null;
                    becameEmpty = true;
                }
            }
            if (becameEmpty) _ = BroadcastAsync();
        }

        public void StopAll(bool broadcast = true)
        {
            List<ActivePlayback> toStop;
            lock (_lock)
            {
                toStop = _active.ToList();
                _active.Clear();
                NowPlayingTitle = null;
            }
            foreach (var pb in toStop)
            {
                // PlaybackStopped ainda dispara, mas o item já saiu de _active — o handler vira no-op.
                try { pb.Output.Stop(); } catch { }
                try { pb.Output.Dispose(); } catch { }
                try { pb.Reader.Dispose(); } catch { }
                try { pb.Device?.Dispose(); } catch { }
            }
            if (broadcast) _ = BroadcastAsync();
        }

        // ---- Dispositivos de saída (pro app escolher cabo/monitor) ----
        public List<object> GetOutputDevices()
        {
            var list = new List<object>();
            try
            {
                using var en = new MMDeviceEnumerator();
                foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    // Só precisamos de duas strings; o MMDevice é COM e morre aqui.
                    using (d)
                    {
                        list.Add(new { id = d.ID, name = d.FriendlyName });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enumerar dispositivos de saída");
            }
            return list;
        }

        public object GetStatePayload() => new
        {
            playing = NowPlayingTitle != null,
            title = NowPlayingTitle,
        };

        private async Task BroadcastAsync()
        {
            try { await _hub.Clients.All.SendAsync("ReceivePlaybackState", GetStatePayload()); }
            catch { }
        }
    }
}
