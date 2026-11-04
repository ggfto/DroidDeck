using System;
using System.Globalization;
using System.Threading.Tasks;
using DroidDeck.Actions;

namespace DroidDeck.Integrations
{
    /// <summary>
    /// Acao "voicemeeter": mudo e ganho de uma entrada (Strip) ou saida (Bus), botoes do Macro
    /// Buttons e qualquer parametro numerico. Fala com o Voicemeeter aberto no PC pela Remote API;
    /// nao tem configuracao.
    /// </summary>
    public class VoicemeeterProvider : IActionProvider
    {
        public const string TypeName = "voicemeeter";

        /// <summary>Faixa do ganho no Voicemeeter (dB).</summary>
        public const float MinGain = -60, MaxGain = 12;

        private readonly IVoicemeeterApi _api;

        public VoicemeeterProvider(IVoicemeeterApi api) => _api = api;

        public string Type => TypeName;

        /// <summary>Mudo so enquanto o dedo estiver no botao (corte de tosse; ver MicHoldService).</summary>
        public const string HoldMute = "hold_mute";

        private static readonly string[] StripOps = { "toggle_mute", "mute", "unmute", HoldMute, "set_gain", "adjust_gain" };

        public ActionSchema Describe() => new()
        {
            Type = TypeName,
            Title = "Voicemeeter",
            Icon = "tune",
            Category = "Mídia",
            Description = "Mudo e ganho das entradas e saídas do Voicemeeter, botões do Macro Buttons e parâmetros.",
            Fields =
            {
                ActionField.Select("operation", "O que fazer",
                    ("toggle_mute", "Mudo (alternar)"),
                    ("mute", "Mutar"),
                    ("unmute", "Desmutar"),
                    (HoldMute, "Cortar enquanto segura (tosse)"),
                    ("set_gain", "Definir o ganho"),
                    ("adjust_gain", "Ganho (+/-)"),
                    ("macro", "Apertar botão do Macro Buttons"),
                    ("param_set", "Definir um parâmetro"),
                    ("param_toggle", "Alternar um parâmetro (0/1)")),
                ActionField.Select("target", "Onde",
                    ("strip", "Entrada (Strip)"),
                    ("bus", "Saída (Bus)"))
                    .When("operation", StripOps),
                ActionField.Number("index", "Número", 1, 1, 8,
                    help: "Contando da esquerda, como no Voicemeeter (1 = a primeira).")
                    .When("operation", StripOps),
                ActionField.Number("gain", "Ganho", 0, MinGain, MaxGain, "dB").When("operation", "set_gain"),
                ActionField.Number("step", "Passo", 3, -24, 24, "dB").When("operation", "adjust_gain"),
                ActionField.Number("button", "Número do botão", 0, 0, 79,
                    help: "O id do botão no Macro Buttons (o primeiro é 0).")
                    .When("operation", "macro"),
                ActionField.Text("parameter", "Parâmetro", "Strip[0].A1",
                    "Nome do parâmetro na Remote API do Voicemeeter.", required: true)
                    .When("operation", "param_set", "param_toggle"),
                ActionField.Number("value", "Valor", 1).When("operation", "param_set"),
            },
        };

        public async Task ExecuteAsync(ActionContext ctx)
        {
            var op = ctx.Operation("toggle_mute");
            switch (op)
            {
                // Vai pelo hub (encostar e soltar, ver DeckHub.HoldKey). Um toque avulso nao corta nada.
                case HoldMute:
                    break;
                case "toggle_mute":
                case "mute":
                case "unmute":
                {
                    var p = Parameter(ctx.Get("target", "strip"), ctx.Int("index", 1), "Mute");
                    var mute = op == "mute" || (op == "toggle_mute" && _api.Get(p) < 0.5f);
                    _api.Set(p, mute ? 1 : 0);
                    break;
                }
                case "set_gain":
                    _api.Set(Parameter(ctx.Get("target", "strip"), ctx.Int("index", 1), "Gain"),
                        ClampGain((float)ctx.Number("gain", 0)));
                    break;
                case "adjust_gain":
                {
                    var p = Parameter(ctx.Get("target", "strip"), ctx.Int("index", 1), "Gain");
                    _api.Set(p, ClampGain(_api.Get(p) + (float)ctx.Number("step", 3)));
                    break;
                }
                case "macro":
                {
                    var button = ctx.Int("button", 0);
                    _api.SetMacroButton(button, 1);
                    // Um clique: o botao solto logo depois roda os comandos de "soltar".
                    await Task.Delay(50);
                    _api.SetMacroButton(button, 0);
                    break;
                }
                case "param_set":
                    _api.Set(CheckParameter(ctx.Get("parameter")), (float)ctx.Number("value", 1));
                    break;
                case "param_toggle":
                {
                    var p = CheckParameter(ctx.Get("parameter"));
                    _api.Set(p, _api.Get(p) >= 0.5f ? 0 : 1);
                    break;
                }
                default:
                    throw new InvalidOperationException("Operação desconhecida.");
            }
        }

        /// <summary>Parametro de mudo de uma acao "cortar enquanto segura". Null se a acao for outra.</summary>
        public static string? HoldMuteParameter(DroidDeck.Models.DeckAction action)
        {
            if (!string.Equals(action.Type, TypeName, StringComparison.OrdinalIgnoreCase) ||
                !action.Parameters.TryGetValue("operation", out var op) ||
                !string.Equals(op, HoldMute, StringComparison.OrdinalIgnoreCase))
                return null;
            action.Parameters.TryGetValue("target", out var target);
            action.Parameters.TryGetValue("index", out var index);
            var number = int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 1;
            return Parameter(target ?? "strip", number, "Mute");
        }

        /// <summary>"strip", 1, "Mute" vira "Strip[0].Mute" (o editor conta a partir de 1).</summary>
        public static string Parameter(string target, int number, string name)
        {
            var kind = string.Equals(target, "bus", StringComparison.OrdinalIgnoreCase) ? "Bus" : "Strip";
            var index = Math.Clamp(number, 1, 8) - 1;
            return string.Create(CultureInfo.InvariantCulture, $"{kind}[{index}].{name}");
        }

        public static float ClampGain(float db) => Math.Clamp(db, MinGain, MaxGain);

        /// <summary>So nomes de parametro: "Strip[0].A1", "Bus[1].Gain", "Fx.Reverb.On".</summary>
        public static string CheckParameter(string parameter)
        {
            var p = parameter.Trim();
            if (p.Length == 0 || p.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(p, @"^[A-Za-z][A-Za-z0-9_\.\[\]]*$"))
                throw new InvalidOperationException("Parâmetro do Voicemeeter desconhecido.");
            return p;
        }
    }
}
