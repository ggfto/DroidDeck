using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using DroidDeck.Actions;
using DroidDeck.Hubs;
using DroidDeck.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Integrations
{
    /// <summary>
    /// RadioDJ pelo plugin REST Server (no RadioDJ 2 ele vem na pasta "Disabled Plugins"). Tudo em
    /// GET: /opt?auth=senha&amp;command=X&amp;arg=Y e /np?auth=senha (tocando agora, em XML).
    ///
    /// A senha vai na URL, entao aqui nao se usa o IHttpClientFactory: o HttpClient dele registra a
    /// URL inteira no log a cada pedido. Por isso tambem nenhuma mensagem de erro leva a URL.
    /// </summary>
    public class RadioDjIntegration : PolledHttpIntegration
    {
        public const string Event = "ReceiveRadioDjState";
        public const string DefaultUrl = "http://127.0.0.1:8080";

        private static readonly HttpClient Client = new() { Timeout = RequestTimeout };

        private volatile string? _title;
        private volatile string? _artist;
        private bool? _autoDj;
        private bool? _assisted;
        private bool? _input;

        public RadioDjIntegration(ILogger<RadioDjIntegration> logger, IHubContext<DeckHub> hub,
            StreamDeckConfigService config, IHttpClientFactory http)
            : base(logger, hub, config, http) { }

        public override string Name => "radiodj";
        public override string Title => "RadioDJ";
        public override string StateEvent => Event;

        private string? Url => Get("url");

        protected override bool HasConfig => Url != null;

        protected override string UnreachableMessage =>
            "Não foi possível falar com o RadioDJ. Confira se ele está aberto e o plugin REST Server ligado.";

        public bool? AutoDj => Connected ? _autoDj : null;
        public bool? Assisted => Connected ? _assisted : null;

        /// <summary>A entrada ao vivo esta ligada? E o que o tally do RadioDJ mostra.</summary>
        public bool? Input => Connected ? _input : null;

        public override IntegrationStatus GetStatus() => new()
        {
            Connected = Connected,
            Configured = HasConfig,
            Account = Connected ? Url : null,
            Message = Error,
            Fields = new() { ["url"] = Url ?? DefaultUrl },
        };

        public override object GetState() => new
        {
            connected = Connected,
            autoDj = AutoDj,
            assisted = Assisted,
            input = Input,
            title = Connected ? _title : null,
            artist = Connected ? _artist : null,
        };

        public override Task<IntegrationStatus> ConfigureAsync(Dictionary<string, string> fields)
        {
            var url = NormalizeUrl(fields.GetValueOrDefault("url"), DefaultUrl);
            var password = fields.GetValueOrDefault("password") ?? "";
            Set(("url", url), ("password", password.Length > 0 ? password : Get("password")));
            return RestartAndWaitAsync();
        }

        public override Task LogoutAsync()
        {
            Clear();
            PollNow();
            return Task.CompletedTask;
        }

        protected override void ResetState()
        {
            _autoDj = _assisted = _input = null;
            _title = _artist = null;
        }

        protected override async Task PollAsync(CancellationToken ct)
        {
            var autoDj = ParseBool(await GetTextAsync(CommandUrl("StatusAutoDJ", null), ct));
            var assisted = ParseBool(await GetTextAsync(CommandUrl("StatusAssisted", null), ct));
            var input = ParseBool(await GetTextAsync(CommandUrl("StatusInput", null), ct));

            (string? title, string? artist) playing = (null, null);
            var hasNowPlaying = false;
            try
            {
                playing = ParseNowPlaying(await GetTextAsync($"{Url}/np?auth={Auth}", ct));
                hasNowPlaying = true;
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException or InvalidDataException)
            {
                // Sem nada tocando (ou versao sem /np): o resto continua valendo.
            }
            // Respondeu, mas nada do que devia: o caminho comum e a senha errada.
            if (autoDj == null && assisted == null && input == null && !hasNowPlaying)
                throw new InvalidOperationException("O RadioDJ não entendeu o pedido. Confira a senha do plugin REST Server.");

            _autoDj = autoDj;
            _assisted = assisted;
            _input = input;
            _title = playing.title;
            _artist = playing.artist;
        }

        private static async Task<string> GetTextAsync(string url, CancellationToken ct)
        {
            using var response = await Client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
            return await response.Content.ReadAsStringAsync(ct);
        }

        private string Auth => Uri.EscapeDataString(Get("password") ?? "");

        private string CommandUrl(string command, string? arg) =>
            $"{Url}/opt?auth={Auth}&command={Uri.EscapeDataString(command)}" +
            (arg == null ? "" : "&arg=" + Uri.EscapeDataString(arg));

        /// <summary>Executa um comando do plugin REST Server.</summary>
        public async Task CommandAsync(string command, string? arg)
        {
            if (!HasConfig) throw new InvalidOperationException("Configure o RadioDJ primeiro.");
            using var response = await Client.GetAsync(CommandUrl(command, arg));
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"O RadioDJ recusou o comando {command} (HTTP {(int)response.StatusCode}).");
            PollNow();
        }

        /// <summary>
        /// O formato da resposta dos Status* nao e documentado (so "booleano"), entao aceita XML ou
        /// texto: vale o primeiro true/false que aparecer. Null se nao tiver nenhum.
        /// </summary>
        public static bool? ParseBool(string text)
        {
            var stripped = Regex.Replace(text, "<[^>]*>", " ");
            var match = Regex.Match(stripped, @"\b(true|false)\b", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.Equals("true", StringComparison.OrdinalIgnoreCase) : null;
        }

        /// <summary>Titulo e artista do XML do /np (raiz SongData, com namespace).</summary>
        public static (string? title, string? artist) ParseNowPlaying(string xml)
        {
            using var reader = XmlReader.Create(new StringReader(xml.TrimStart('﻿')),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("XML vazio.");
            string? Field(string name)
            {
                var value = root.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }
            return (Field("Title"), Field("Artist"));
        }
    }

    /// <summary>Acao "radiodj": players, AutoDJ, assistido, entrada ao vivo e carts.</summary>
    public class RadioDjProvider : IStatefulActionProvider
    {
        public const string TypeName = "radiodj";

        private readonly RadioDjIntegration _radio;

        public RadioDjProvider(RadioDjIntegration radio) => _radio = radio;

        public string Type => TypeName;
        public string StateEvent => RadioDjIntegration.Event;
        public object GetState() => _radio.GetState();

        public ActionSchema Describe() => new()
        {
            Type = TypeName,
            Title = "RadioDJ",
            Icon = "radio",
            Category = "Estúdio",
            Stateful = true,
            Settings = "radiodj",
            StateEvent = StateEvent,
            Description = "Player, AutoDJ, modo assistido, entrada ao vivo e carts do RadioDJ, pelo plugin REST Server.",
            Fields =
            {
                ActionField.Select("operation", "O que fazer",
                    ("play_track", "Tocar uma faixa da playlist"),
                    ("stop", "Parar o player"),
                    ("pause", "Pausar"),
                    ("resume", "Retomar"),
                    ("restart", "Reiniciar a faixa"),
                    ("play_intro", "Tocar da introdução"),
                    ("autodj_toggle", "AutoDJ (alternar)"),
                    ("assisted_toggle", "Assistido (alternar)"),
                    ("input_toggle", "Entrada ao vivo (alternar)"),
                    ("events_on", "Ligar os eventos"),
                    ("events_off", "Desligar os eventos"),
                    ("cart", "Tocar um cart"),
                    ("load_playlist", "Carregar uma playlist"),
                    ("load_track", "Pôr uma faixa no topo"),
                    ("custom", "Outro comando")),
                ActionField.Number("track", "Posição na playlist", 1, 1, 999,
                    help: "Contando de cima, como na tela do RadioDJ (1 = a primeira).")
                    .When("operation", "play_track"),
                ActionField.Number("cart", "Número do cart", 1, 1, 999).When("operation", "cart"),
                ActionField.Number("id", "ID", 1, 1, 99999999,
                    help: "ID no banco do RadioDJ. Há relatos de erro nestes comandos na versão 2.0.4.")
                    .When("operation", "load_playlist", "load_track"),
                ActionField.Text("command", "Comando", "RefreshEvents",
                    "Um comando da lista nas opções do plugin REST Server.", required: true)
                    .When("operation", "custom"),
                ActionField.Text("arg", "Argumento (opcional)").When("operation", "custom"),
            },
        };

        public Task ExecuteAsync(ActionContext ctx)
        {
            var (command, arg) = Build(ctx, _radio.AutoDj, _radio.Assisted, _radio.Input);
            return _radio.CommandAsync(command, arg);
        }

        /// <summary>Comando e argumento. Os "alternar" dependem do estado lido do RadioDJ.</summary>
        public static (string command, string? arg) Build(ActionContext c, bool? autoDj, bool? assisted, bool? input)
        {
            static string Flip(bool? current)
            {
                if (current == null) throw new InvalidOperationException("O RadioDJ não informou o estado atual.");
                return current.Value ? "0" : "1";
            }

            string Num(string key, int max) => Math.Clamp(c.Int(key, 1), 1, max).ToString(System.Globalization.CultureInfo.InvariantCulture);

            switch (c.Operation("play_track"))
            {
                // O editor conta a partir de 1; o RadioDJ, de 0.
                case "play_track":
                    return ("PlayPlaylistTrack", (Math.Clamp(c.Int("track", 1), 1, 999) - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                case "stop": return ("StopPlayer", null);
                case "pause": return ("PausePlayer", "1");
                case "resume": return ("PausePlayer", "0");
                case "restart": return ("RestartPlayer", null);
                case "play_intro": return ("PlayFromIntro", null);
                case "autodj_toggle": return ("EnableAutoDJ", Flip(autoDj));
                case "assisted_toggle": return ("EnableAssisted", Flip(assisted));
                case "input_toggle": return ("EnableInput", Flip(input));
                case "events_on": return ("EnableEvents", "1");
                case "events_off": return ("EnableEvents", "0");
                case "cart": return ("PlaycartByNumber", Num("cart", 999));
                case "load_playlist": return ("LoadPlaylist", Num("id", 99999999));
                case "load_track": return ("LoadTrackToTop", Num("id", 99999999));
                case "custom":
                {
                    var command = c.Get("command").Trim();
                    if (!Regex.IsMatch(command, "^[A-Za-z0-9]{1,64}$"))
                        throw new InvalidOperationException("Comando inválido.");
                    var arg = c.Get("arg").Trim();
                    return (command, arg.Length > 0 ? arg : null);
                }
                default:
                    throw new InvalidOperationException("Operação desconhecida.");
            }
        }
    }
}
