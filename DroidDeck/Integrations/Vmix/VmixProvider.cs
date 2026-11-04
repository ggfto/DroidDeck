using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DroidDeck.Actions;

namespace DroidDeck.Integrations
{
    /// <summary>
    /// Acao "vmix": corte, transicao, entradas, overlays, audio, gravacao e transmissao. Os nomes
    /// das funcoes sao os da referencia de atalhos do vMix.
    /// </summary>
    public class VmixProvider : IStatefulActionProvider
    {
        public const string TypeName = "vmix";

        private static readonly string[] InputOps = { "preview", "program", "quickplay", "overlay", "toggle_audio", "set_volume" };

        private readonly VmixIntegration _vmix;

        public VmixProvider(VmixIntegration vmix) => _vmix = vmix;

        public string Type => TypeName;
        public string StateEvent => VmixIntegration.Event;
        public object GetState() => _vmix.GetState();

        public ActionSchema Describe() => new()
        {
            Type = TypeName,
            Title = "vMix",
            Icon = "live_tv",
            Category = "Estúdio",
            Stateful = true,
            Settings = "vmix",
            StateEvent = StateEvent,
            Description = "Corte, transição, entradas, overlays, áudio, gravação e transmissão, com o estado do vMix no botão.",
            Fields =
            {
                ActionField.Select("operation", "O que fazer",
                    ("cut", "Cortar (preview para o programa)"),
                    ("fade", "Transição Fade"),
                    ("preview", "Mandar entrada para o preview"),
                    ("program", "Mandar entrada para o programa"),
                    ("quickplay", "Quick Play da entrada"),
                    ("overlay", "Overlay (alternar)"),
                    ("overlay_off", "Tirar todos os overlays"),
                    ("toggle_audio", "Áudio da entrada (alternar)"),
                    ("set_volume", "Volume da entrada"),
                    ("master_audio", "Áudio master (alternar)"),
                    ("master_volume", "Volume master"),
                    ("toggle_record", "Gravar (alternar)"),
                    ("toggle_stream", "Transmitir (alternar)"),
                    ("toggle_external", "Saída External (alternar)"),
                    ("toggle_multicorder", "MultiCorder (alternar)"),
                    ("fade_to_black", "Fade to Black (alternar)"),
                    ("snapshot", "Captura de tela"),
                    ("custom", "Outra função")),
                ActionField.Dynamic("input", "Entrada", "/api/integrations/vmix/options/inputs",
                    "Também aceita o número ou o nome da entrada.", allowCustom: true)
                    .When("operation", InputOps),
                ActionField.Number("duration", "Duração", 1000, 0, 60000, "ms").When("operation", "fade"),
                ActionField.Number("overlay", "Overlay", 1, 1, 8).When("operation", "overlay"),
                ActionField.Number("volume", "Volume", 100, 0, 100, "%").When("operation", "set_volume", "master_volume"),
                ActionField.Text("function", "Função", "SetText",
                    "Nome da função na referência de atalhos do vMix.", required: true)
                    .When("operation", "custom"),
                ActionField.Text("query", "Parâmetros (opcional)", "Input=1&SelectedName=Titulo&Value=Oi",
                    "Como na Web API do vMix, com os valores em formato de URL.")
                    .When("operation", "custom"),
            },
        };

        public Task ExecuteAsync(ActionContext ctx)
        {
            var (function, query) = Build(ctx);
            return _vmix.FunctionAsync(function, query);
        }

        /// <summary>Nome da funcao e parametros de uma acao.</summary>
        public static (string function, string query) Build(ActionContext c)
        {
            switch (c.Operation("cut"))
            {
                case "cut": return ("Cut", "");
                case "fade": return ("Fade", Query(("Duration", Whole(c, "duration", 1000, 0, 60000))));
                case "preview": return ("PreviewInput", InputQuery(c));
                case "program": return ("ActiveInput", InputQuery(c));
                case "quickplay": return ("QuickPlay", InputQuery(c));
                case "overlay": return ("OverlayInput" + Whole(c, "overlay", 1, 1, 8), InputQuery(c));
                case "overlay_off": return ("OverlayInputAllOff", "");
                case "toggle_audio": return ("Audio", InputQuery(c));
                case "set_volume":
                    return ("SetVolume", InputQuery(c) + "&" + Query(("Value", Whole(c, "volume", 100, 0, 100))));
                case "master_audio": return ("MasterAudio", "");
                case "master_volume": return ("SetMasterVolume", Query(("Value", Whole(c, "volume", 100, 0, 100))));
                case "toggle_record": return ("StartStopRecording", "");
                case "toggle_stream": return ("StartStopStreaming", "");
                case "toggle_external": return ("StartStopExternal", "");
                case "toggle_multicorder": return ("StartStopMultiCorder", "");
                case "fade_to_black": return ("FadeToBlack", "");
                case "snapshot": return ("Snapshot", "");
                case "custom": return (CheckFunction(c.Get("function")), CheckQuery(c.Get("query")));
                default: throw new InvalidOperationException("Operação desconhecida.");
            }
        }

        private static string InputQuery(ActionContext c)
        {
            var input = c.Get("input").Trim();
            if (input.Length == 0) throw new InvalidOperationException("Escolha uma entrada do vMix.");
            return Query(("Input", input));
        }

        private static string Whole(ActionContext c, string key, int fallback, int min, int max) =>
            Math.Clamp(c.Int(key, fallback), min, max).ToString(CultureInfo.InvariantCulture);

        private static string Query(params (string key, string value)[] items)
        {
            var parts = new string[items.Length];
            for (var i = 0; i < items.Length; i++)
                parts[i] = items[i].key + "=" + Uri.EscapeDataString(items[i].value);
            return string.Join("&", parts);
        }

        /// <summary>So letras e numeros: o nome vai direto numa linha do protocolo.</summary>
        public static string CheckFunction(string function)
        {
            var f = function.Trim();
            if (!Regex.IsMatch(f, "^[A-Za-z0-9]{1,64}$"))
                throw new InvalidOperationException("Função do vMix inválida.");
            return f;
        }

        /// <summary>Sem quebra de linha: ela comecaria outro comando na conexao.</summary>
        public static string CheckQuery(string query)
        {
            var q = query.Trim();
            foreach (var ch in q)
                if (char.IsControl(ch)) throw new InvalidOperationException("Parâmetros do vMix inválidos.");
            return q;
        }
    }
}
