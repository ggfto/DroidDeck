using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Lib;
using DroidDeck.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// Automacao: um acontecimento no PC aperta um botao do deck, sem tocar no celular.
    ///
    ///   schedule  Time "HH:mm" + Days (como na condicao "weekday"; vazio = todo dia)
    ///   app_start App abriu (processo, sem .exe)
    ///   app_exit  App fechou
    ///   resume    PC voltou da suspensao/hibernacao
    ///   startup   DroidDeck iniciou (logo depois de ligar o PC, com o autostart)
    ///   tally_on  Target (alvo do tally) entrou no ar
    ///   tally_off Target saiu do ar. Perder a conexao nao conta: nao se sabe se saiu
    ///
    /// O gatilho aperta um botao salvo (como o celular faria): o que ele pode fazer ja foi
    /// decidido quando o botao foi criado (ver ActionPolicy), entao gatilho nao abre brecha.
    /// Guardado em %AppData%\DroidDeck\triggers.json.
    /// </summary>
    public class TriggerService : BackgroundService
    {
        public class Trigger
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N");
            public string? Name { get; set; }
            public bool Enabled { get; set; } = true;
            public string Event { get; set; } = "schedule";
            public string? Time { get; set; }
            public string? Days { get; set; }
            public string? App { get; set; }

            /// <summary>Alvo do tally e o nome dele quando foi escolhido (o id de uma entrada do vMix nao diz nada).</summary>
            public string? Target { get; set; }
            public string? TargetLabel { get; set; }
            public string? ProfileId { get; set; }
            public string? ButtonId { get; set; }
            /// <summary>Condicao extra (mesma da multi-acao), ex.: so se o OBS estiver aberto.</summary>
            public StepCondition? If { get; set; }
        }

        public static readonly string[] Events = { "schedule", "app_start", "app_exit", "resume", "startup", "tally_on", "tally_off" };

        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(2);
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        private readonly StreamDeckConfigService _config;
        private readonly ButtonToggleService _toggles;
        private readonly ActionExecutorService _executor;
        private readonly Conditions _conditions;
        private readonly ILogger<TriggerService> _logger;
        private readonly object _lock = new();
        private List<Trigger> _triggers;

        /// <summary>Relogio (os testes trocam).</summary>
        public Func<DateTime> Now { get; set; } = () => DateTime.Now;

        /// <summary>Processos abertos (os testes trocam).</summary>
        public Func<HashSet<string>> RunningProcesses { get; set; } = DefaultRunningProcesses;

        public TriggerService(StreamDeckConfigService config, ButtonToggleService toggles,
            ActionExecutorService executor, Conditions conditions, ILogger<TriggerService> logger,
            TallyService? tally = null)
        {
            _config = config;
            _toggles = toggles;
            _executor = executor;
            _conditions = conditions;
            _logger = logger;
            _triggers = Load();
            if (tally != null)
            {
                tally.WatchRequested = () => GetAll().Any(t => t.Enabled && t.Event is "tally_on" or "tally_off");
                // Na hora, sem esperar a volta de 2 s: a luz de "no ar" acende junto com o tally.
                tally.TargetChanged += (target, previous) => _ = SafeTallyChangedAsync(target.Id, previous, target.State);
            }
        }

        private async Task SafeTallyChangedAsync(string id, string previous, string current)
        {
            try { await OnTallyChangedAsync(id, previous, current); }
            catch (Exception ex) { _logger.LogWarning("Gatilhos: {Message}", ex.Message); }
        }

        /// <summary>Um alvo do tally mudou de estado. Programa para preview tambem e sair do ar.</summary>
        public async Task OnTallyChangedAsync(string id, string previous, string current)
        {
            var wasOn = previous == TallyTarget.Program;
            var isOn = current == TallyTarget.Program;
            if (wasOn == isOn) return;
            var eventName = isOn ? "tally_on" : "tally_off";
            await FireAsync(t => t.Event == eventName && t.Target == id);
        }

        private static string FilePath => Path.Combine(AppPaths.RoamingDir, "triggers.json");

        public List<Trigger> GetAll()
        {
            lock (_lock) return _triggers.ToList();
        }

        public void SaveAll(List<Trigger> triggers)
        {
            foreach (var t in triggers)
            {
                if (string.IsNullOrWhiteSpace(t.Id)) t.Id = Guid.NewGuid().ToString("N");
                if (!Events.Contains(t.Event)) t.Event = "schedule";
            }
            lock (_lock) _triggers = triggers.ToList();
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(triggers, Json)); }
            catch (Exception ex) { _logger.LogWarning("Gatilhos: nao deu para salvar ({Message})", ex.Message); }
        }

        private static List<Trigger> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<List<Trigger>>(File.ReadAllText(FilePath), Json) ?? new();
            }
            catch { }
            return new();
        }

        // ---- Laco ----

        private HashSet<string>? _lastProcesses;
        private DateTime _lastTick;
        private string? _lastMinute;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Da tempo de o resto subir (OBS, Discord) antes do gatilho de inicio.
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { return; }
            await FireAsync(t => t.Event == "startup");

            _lastTick = Now();
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await TickAsync(); }
                catch (Exception ex) { _logger.LogDebug("Gatilhos: {Message}", ex.Message); }
                try { await Task.Delay(Tick, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>Uma volta: horario, apps que abriram/fecharam e volta da suspensao.</summary>
        public async Task TickAsync()
        {
            var now = Now();

            // Um intervalo bem maior que o normal entre duas voltas = o PC estava dormindo.
            if (_lastTick != default && now - _lastTick > TimeSpan.FromSeconds(30))
                await FireAsync(t => t.Event == "resume");
            _lastTick = now;

            var minute = now.ToString("HH:mm");
            if (minute != _lastMinute)
            {
                _lastMinute = minute;
                await FireAsync(t => t.Event == "schedule" && t.Time?.Trim() == minute &&
                                     (string.IsNullOrWhiteSpace(t.Days) || Conditions.Weekday(t.Days, now)));
            }

            if (GetAll().Any(t => t.Enabled && t.Event is "app_start" or "app_exit"))
            {
                var current = RunningProcesses();
                if (_lastProcesses != null)
                {
                    var started = current.Except(_lastProcesses).ToHashSet();
                    var exited = _lastProcesses.Except(current).ToHashSet();
                    if (started.Count > 0)
                        await FireAsync(t => t.Event == "app_start" && started.Contains(Normalize(t.App)));
                    if (exited.Count > 0)
                        await FireAsync(t => t.Event == "app_exit" && exited.Contains(Normalize(t.App)));
                }
                _lastProcesses = current;
            }
            else
            {
                _lastProcesses = null;
            }
        }

        private async Task FireAsync(Func<Trigger, bool> match)
        {
            foreach (var t in GetAll().Where(t => t.Enabled && match(t)))
            {
                if (!_conditions.Evaluate(t.If)) continue;
                _logger.LogInformation("Gatilho '{Name}' ({Event})", t.Name ?? t.Id, t.Event);
                await RunAsync(t);
            }
        }

        /// <summary>Aperta o botao do gatilho. False se o botao nao existe mais.</summary>
        public async Task<bool> RunAsync(Trigger t)
        {
            var button = _config.GetProfile(t.ProfileId ?? "")?.Buttons?.Find(b => b.Id == t.ButtonId);
            if (button == null)
            {
                _logger.LogWarning("Gatilho '{Name}': o botao nao existe mais", t.Name ?? t.Id);
                return false;
            }
            var action = button.Toggle ? _toggles.Press(button) : button.Action;
            if (action != null) await _executor.ExecuteActionAsync(action, button.Id);
            return true;
        }

        private static string Normalize(string? app)
        {
            app = (app ?? "").Trim().ToLowerInvariant();
            return app.EndsWith(".exe") ? app[..^4] : app;
        }

        private static HashSet<string> DefaultRunningProcesses()
        {
            var set = new HashSet<string>();
            foreach (var p in Process.GetProcesses())
            {
                try { set.Add(p.ProcessName.ToLowerInvariant()); }
                catch { }
                finally { p.Dispose(); }
            }
            return set;
        }
    }
}
