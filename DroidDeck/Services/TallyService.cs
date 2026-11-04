using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Actions;
using DroidDeck.Hubs;
using DroidDeck.Integrations;
using DroidDeck.Lib;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace DroidDeck.Services
{
    /// <summary>Um ponto que a tela de tally pode acompanhar: uma cena, uma entrada, "no ar"...</summary>
    public record TallyTarget(string Id, string Source, string Label, string State)
    {
        public const string Program = "program", Preview = "preview", Idle = "idle";
    }

    /// <summary>
    /// Tally e ON AIR: junta o estado do OBS, vMix, mAirList, RadioDJ, Voicemeeter e de um sinal
    /// manual numa lista de alvos com "program", "preview" ou "idle". Cada celular escolhe o seu
    /// alvo. Alvo que some da lista (programa desconectado) aparece como "sem sinal" no app: tela
    /// de tally nunca pode dizer "fora do ar" so porque perdeu a conexao.
    /// </summary>
    public class TallyService : BackgroundService
    {
        public const string Event = "ReceiveTallyState";
        public const string ManualId = "manual/onair";

        private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan VoicemeeterEvery = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan VoicemeeterRetry = TimeSpan.FromSeconds(5);
        private const int MaxStrips = 8;

        private readonly ObsService _obs;
        private readonly VmixIntegration _vmix;
        private readonly MairlistIntegration _mairlist;
        private readonly RadioDjIntegration _radioDj;
        private readonly IVoicemeeterApi _voicemeeter;
        private readonly IHubContext<DeckHub> _hub;

        private volatile bool _manual;
        private volatile bool[] _stripsOpen = Array.Empty<bool>();
        private DateTime _nextVoicemeeter;
        private string _lastKey = "";

        public TallyService(ObsService obs, VmixIntegration vmix, MairlistIntegration mairlist,
            RadioDjIntegration radioDj, IVoicemeeterApi voicemeeter, IHubContext<DeckHub> hub)
        {
            _obs = obs;
            _vmix = vmix;
            _mairlist = mairlist;
            _radioDj = radioDj;
            _voicemeeter = voicemeeter;
            _hub = hub;
        }

        public bool Manual => _manual;

        public void SetManual(bool on)
        {
            _manual = on;
            _lastKey = "";
        }

        /// <summary>Todos os alvos agora, com o rotulo no idioma atual (L.Language).</summary>
        public List<TallyTarget> Targets()
        {
            var list = new List<TallyTarget>
            {
                new(ManualId, "manual", L.T("Sinal manual (ON AIR)"), _manual ? TallyTarget.Program : TallyTarget.Idle),
            };
            if (_obs.Connected)
                list.AddRange(ObsTargets(_obs.Scenes, _obs.CurrentScene, _obs.PreviewScene, _obs.Streaming, _obs.Recording));
            if (_vmix.Snapshot is { } vmix)
                list.AddRange(VmixTargets(vmix, _vmix.TallyOf));
            if (_mairlist.OnAir is { } onAir)
                list.Add(new("mairlist/onair", "mairlist", "mAirList: " + L.T("No ar"), onAir ? TallyTarget.Program : TallyTarget.Idle));
            if (_radioDj.Input is { } input)
                list.Add(new("radiodj/input", "radiodj", "RadioDJ: " + L.T("Entrada ao vivo"), input ? TallyTarget.Program : TallyTarget.Idle));
            var strips = _stripsOpen;
            for (var i = 0; i < strips.Length; i++)
                list.Add(new($"voicemeeter/strip:{i + 1}", "voicemeeter", $"Voicemeeter: {L.T("Microfone aberto")} {i + 1}",
                    strips[i] ? TallyTarget.Program : TallyTarget.Idle));
            return list;
        }

        public static IEnumerable<TallyTarget> ObsTargets(IEnumerable<string> scenes, string? program, string? preview, bool streaming, bool recording)
        {
            yield return new("obs/stream", "obs", "OBS: " + L.T("Transmissão"), streaming ? TallyTarget.Program : TallyTarget.Idle);
            yield return new("obs/record", "obs", "OBS: " + L.T("Gravação"), recording ? TallyTarget.Program : TallyTarget.Idle);
            foreach (var scene in scenes)
                yield return new("obs/scene:" + scene, "obs", "OBS: " + scene,
                    scene == program ? TallyTarget.Program : scene == preview ? TallyTarget.Preview : TallyTarget.Idle);
        }

        public static IEnumerable<TallyTarget> VmixTargets(VmixSnapshot vmix, Func<int, int> tallyOf)
        {
            yield return new("vmix/stream", "vmix", "vMix: " + L.T("Transmissão"), vmix.Streaming ? TallyTarget.Program : TallyTarget.Idle);
            yield return new("vmix/record", "vmix", "vMix: " + L.T("Gravação"), vmix.Recording ? TallyTarget.Program : TallyTarget.Idle);
            foreach (var input in vmix.Inputs)
                yield return new("vmix/input:" + input.Key, "vmix", $"vMix: {input.Number}. {input.Title}",
                    tallyOf(input.Number) switch
                    {
                        VmixIntegration.TallyProgram => TallyTarget.Program,
                        VmixIntegration.TallyPreview => TallyTarget.Preview,
                        _ => TallyTarget.Idle,
                    });
        }

        public object GetState() => new
        {
            manual = _manual,
            targets = Targets().ToDictionary(t => t.Id, t => new { label = t.Label, state = t.State, source = t.Source }),
        };

        public List<FieldOption> Options() =>
            Targets().Select(t => new FieldOption(t.Id, t.Label)).ToList();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Sem ninguem olhando nao ha para quem mandar; quem conectar recebe o estado na hora.
                    if (DeckHub.ConnectedCount > 0)
                    {
                        PollVoicemeeter();
                        var key = string.Join("|", Targets().Select(t => t.Id + "=" + t.State));
                        if (key != _lastKey)
                        {
                            _lastKey = key;
                            await _hub.SendLocalizedAsync(Event, GetState);
                        }
                    }
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested) { }
                try { await Task.Delay(Tick, stoppingToken); }
                catch (TaskCanceledException) { }
            }
        }

        /// <summary>Le o mudo de cada entrada: microfone aberto = no ar. Uma entrada a mais recusa e acaba a lista.</summary>
        private void PollVoicemeeter()
        {
            var now = DateTime.UtcNow;
            if (now < _nextVoicemeeter) return;
            var open = ReadStrips(_voicemeeter);
            _stripsOpen = open;
            _nextVoicemeeter = now + (open.Length > 0 ? VoicemeeterEvery : VoicemeeterRetry);
        }

        public static bool[] ReadStrips(IVoicemeeterApi api)
        {
            var open = new List<bool>();
            try
            {
                for (var i = 1; i <= MaxStrips; i++)
                    open.Add(api.Get(VoicemeeterProvider.Parameter("strip", i, "Mute")) < 0.5f);
            }
            catch (Exception)
            {
                // Fechado, nao instalado ou passou da ultima entrada.
            }
            return open.ToArray();
        }
    }

    /// <summary>
    /// Acao "tally": abre a tela de tally (feito pelo app), mostra o estado de um alvo no botao ou
    /// muda o sinal manual de ON AIR.
    /// </summary>
    public class TallyProvider : IStatefulActionProvider
    {
        public const string TypeName = "tally";

        private readonly TallyService _tally;

        public TallyProvider(TallyService tally) => _tally = tally;

        public string Type => TypeName;
        public string StateEvent => TallyService.Event;
        public object GetState() => _tally.GetState();

        public ActionSchema Describe() => new()
        {
            Type = TypeName,
            Title = "Tally e ON AIR",
            Icon = "sensors",
            Category = "Estúdio",
            Stateful = true,
            StateEvent = StateEvent,
            Description = "Tela cheia de tally ou ON AIR, ou o estado de uma cena, entrada ou microfone no botão.",
            Fields =
            {
                ActionField.Select("operation", "O que fazer",
                    ("open", "Abrir a tela de tally"),
                    ("show", "Só mostrar o estado no botão"),
                    ("manual_toggle", "Sinal manual de ON AIR (alternar)"),
                    ("manual_on", "Ligar o sinal manual"),
                    ("manual_off", "Desligar o sinal manual")),
                ActionField.Dynamic("target", "O que acompanhar", "/api/tally/targets",
                    "A lista traz só o que está conectado agora.", allowCustom: true)
                    .When("operation", "open", "show"),
            },
        };

        public Task ExecuteAsync(ActionContext c)
        {
            switch (c.Operation("open"))
            {
                case "manual_toggle":
                    _tally.SetManual(!_tally.Manual);
                    break;
                case "manual_on":
                    _tally.SetManual(true);
                    break;
                case "manual_off":
                    _tally.SetManual(false);
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
