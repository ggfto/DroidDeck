using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Actions;
using DroidDeck.Hubs;
using DroidDeck.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Integrations
{
    /// <summary>
    /// mAirList pela interface REST (Config → Remote Control → REST, porta padrao 9300, HTTP Basic).
    /// Comandos em POST /execute; o estado "no ar" em GET /status/onair. Cada um depende de um
    /// plugin da propria interface (ExecuteCommand e GeneralRead).
    /// </summary>
    public class MairlistIntegration : PolledHttpIntegration
    {
        public const string Event = "ReceiveMairlistState";
        public const string DefaultUrl = "http://127.0.0.1:9300";

        private volatile string? _onAir;

        public MairlistIntegration(ILogger<MairlistIntegration> logger, IHubContext<DeckHub> hub,
            StreamDeckConfigService config, IHttpClientFactory http)
            : base(logger, hub, config, http) { }

        public override string Name => "mairlist";
        public override string Title => "mAirList";
        public override string StateEvent => Event;

        private string? Url => Get("url");

        protected override bool HasConfig => Url != null;

        protected override string UnreachableMessage =>
            "Não foi possível falar com o mAirList. Confira se ele está aberto e a interface REST ligada.";

        /// <summary>No ar? Null se a leitura de estado estiver desligada no mAirList.</summary>
        public bool? OnAir => Connected && _onAir != null ? _onAir == "1" : null;

        public override IntegrationStatus GetStatus() => new()
        {
            Connected = Connected,
            Configured = HasConfig,
            Account = Connected ? Url : null,
            Message = Error,
            Fields = new()
            {
                ["url"] = Url ?? DefaultUrl,
                ["username"] = Get("username") ?? "",
            },
        };

        public override object GetState() => new { connected = Connected, onAir = OnAir };

        public override Task<IntegrationStatus> ConfigureAsync(Dictionary<string, string> fields)
        {
            var url = NormalizeUrl(fields.GetValueOrDefault("url"), DefaultUrl);
            var password = fields.GetValueOrDefault("password") ?? "";
            Set(("url", url),
                ("username", fields.GetValueOrDefault("username")?.Trim()),
                ("password", password.Length > 0 ? password : Get("password")));
            return RestartAndWaitAsync();
        }

        public override Task LogoutAsync()
        {
            Clear();
            PollNow();
            return Task.CompletedTask;
        }

        protected override void ResetState() => _onAir = null;

        protected override async Task PollAsync(CancellationToken ct)
        {
            using var request = NewRequest(HttpMethod.Get, "/status/onair");
            using var response = await NewClient().SendAsync(request, ct);
            CheckAuth(response);
            // Sem o plugin GeneralRead a conexao vale, so nao ha estado para ler.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _onAir = null;
                return;
            }
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
            _onAir = (await response.Content.ReadAsStringAsync(ct)).Trim();
        }

        /// <summary>Executa um comando do mAirList (POST /execute).</summary>
        public async Task ExecuteCommandAsync(string command)
        {
            if (!HasConfig) throw new InvalidOperationException("Configure o mAirList primeiro.");
            using var request = NewRequest(HttpMethod.Post, "/execute");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["command"] = command });
            using var response = await NewClient().SendAsync(request);
            CheckAuth(response);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException("Ligue o plugin ExecuteCommand na interface REST do mAirList.");
            response.EnsureSuccessStatusCode();
            PollNow();
        }

        private static void CheckAuth(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Usuário ou senha recusados pelo mAirList.");
        }

        private HttpRequestMessage NewRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, Url + path);
            if (Get("username") is { } user)
            {
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{Get("password")}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }
            return request;
        }
    }

    /// <summary>Acao "mairlist": no ar, automacao, players e cartwall.</summary>
    public class MairlistProvider : IStatefulActionProvider
    {
        public const string TypeName = "mairlist";

        private static readonly string[] PlaylistOps =
            { "automation_toggle", "automation_on", "automation_off", "next", "player_toggle", "player_start", "player_stop", "player_fadeout" };

        private static readonly string[] PlayerOps = { "player_toggle", "player_start", "player_stop", "player_fadeout" };

        private readonly MairlistIntegration _mairlist;

        public MairlistProvider(MairlistIntegration mairlist) => _mairlist = mairlist;

        public string Type => TypeName;
        public string StateEvent => MairlistIntegration.Event;
        public object GetState() => _mairlist.GetState();

        public ActionSchema Describe() => new()
        {
            Type = TypeName,
            Title = "mAirList",
            Icon = "radio",
            Category = "Estúdio",
            Stateful = true,
            Settings = "mairlist",
            StateEvent = StateEvent,
            Description = "No ar, automação, players e cartwall do mAirList, pela interface REST dele.",
            Fields =
            {
                ActionField.Select("operation", "O que fazer",
                    ("on_air_toggle", "No ar (alternar)"),
                    ("on_air", "Entrar no ar"),
                    ("off_air", "Sair do ar"),
                    ("automation_toggle", "Automação (alternar)"),
                    ("automation_on", "Ligar a automação"),
                    ("automation_off", "Desligar a automação"),
                    ("next", "Próximo da playlist"),
                    ("player_toggle", "Player: tocar ou parar"),
                    ("player_start", "Player: tocar"),
                    ("player_stop", "Player: parar"),
                    ("player_fadeout", "Player: fade out"),
                    ("cart", "Tocar um cart"),
                    ("cart_stop_all", "Parar todos os carts"),
                    ("custom", "Outro comando")),
                ActionField.Number("playlist", "Playlist", 1, 1, 16).When("operation", PlaylistOps),
                ActionField.Number("player", "Player", 1, 1, 16).When("operation", PlayerOps),
                ActionField.Number("cart", "Número do cart", 1, 1, 999).When("operation", "cart"),
                ActionField.Text("command", "Comando", "PLAYLIST 1 NEXT",
                    "Um comando da lista de comandos remotos do mAirList.", required: true)
                    .When("operation", "custom"),
            },
        };

        public Task ExecuteAsync(ActionContext ctx) => _mairlist.ExecuteCommandAsync(BuildCommand(ctx));

        public static string BuildCommand(ActionContext c)
        {
            var playlist = Math.Clamp(c.Int("playlist", 1), 1, 16);
            var player = Math.Clamp(c.Int("player", 1), 1, 16);
            return c.Operation("on_air_toggle") switch
            {
                "on_air_toggle" => "ON/OFF AIR",
                "on_air" => "ON AIR",
                "off_air" => "OFF AIR",
                "automation_toggle" => $"AUTOMATION {playlist} ON/OFF",
                "automation_on" => $"AUTOMATION {playlist} ON",
                "automation_off" => $"AUTOMATION {playlist} OFF",
                "next" => $"PLAYLIST {playlist} NEXT",
                "player_toggle" => $"PLAYER {playlist}-{player} START/STOP",
                "player_start" => $"PLAYER {playlist}-{player} START",
                "player_stop" => $"PLAYER {playlist}-{player} STOP",
                "player_fadeout" => $"PLAYER {playlist}-{player} FADEOUT",
                "cart" => $"CARTWALL {Math.Clamp(c.Int("cart", 1), 1, 999)} CLICK",
                "cart_stop_all" => "CARTWALL ALL STOP",
                "custom" => CheckCommand(c.Get("command")),
                _ => throw new InvalidOperationException("Operação desconhecida."),
            };
        }

        public static string CheckCommand(string command)
        {
            var text = command.Trim();
            if (text.Length == 0) throw new InvalidOperationException("Preencha o comando.");
            foreach (var ch in text)
                if (char.IsControl(ch)) throw new InvalidOperationException("Comando inválido.");
            return text;
        }
    }
}
