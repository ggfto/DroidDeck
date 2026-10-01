using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DroidDeck.Auth;
using DroidDeck.Models;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DroidDeck.Tests
{
    public class ActionPolicyTests
    {
        private static DeckAction Act(string type, params (string k, string v)[] ps)
        {
            var d = new Dictionary<string, string>();
            foreach (var (k, v) in ps) d[k] = v;
            return new DeckAction { Type = type, Parameters = d };
        }

        private static DeckProfile Profile(params DeckButton[] buttons) =>
            new() { Id = "p1", Buttons = new List<DeckButton>(buttons) };

        [Theory]
        [InlineData("hotkey")]
        [InlineData("launch_app")]
        [InlineData("launchApp")]
        [InlineData("ActivateWindow")]
        public void TiposQueExecutamCodigo_SaoPrivilegiados(string type) =>
            Assert.True(ActionPolicy.IsPrivileged(Act(type)));

        [Theory]
        [InlineData("discord")]
        [InlineData("mixer")]
        [InlineData("media")]
        [InlineData("obs")]
        [InlineData("tuya")]
        [InlineData("soundboard")]
        public void DemaisTipos_NaoSaoPrivilegiados(string type) =>
            Assert.False(ActionPolicy.IsPrivileged(Act(type)));

        [Fact]
        public void Multi_HerdaDosPassos()
        {
            Assert.True(ActionPolicy.IsPrivileged(Act("multi", ("steps", "[{\"type\":\"mixer\"},{\"type\":\"hotkey\",\"parameters\":{\"keys\":\"^c\"}}]"))));
            Assert.False(ActionPolicy.IsPrivileged(Act("multi", ("steps", "[{\"type\":\"mixer\",\"parameters\":{\"operation\":\"toggleMute\"}}]"))));
        }

        [Fact]
        public void Multi_ComJsonInvalido_ETratadaComoPrivilegiada() =>
            Assert.True(ActionPolicy.IsPrivileged(Act("multi", ("steps", "nao e json"))));

        [Fact]
        public void Remoto_NaoPodeCriarBotaoPrivilegiado()
        {
            var saved = Profile();
            var incoming = Profile(new DeckButton { Id = "b1", Action = Act("launch_app", ("path", "calc.exe")) });

            Assert.Equal("b1", ActionPolicy.FindRemoteViolation(incoming, saved)?.Id);
            Assert.NotNull(ActionPolicy.FindRemoteViolation(incoming, null));
        }

        [Fact]
        public void Remoto_NaoPodeAlterarParametrosDeBotaoPrivilegiado()
        {
            var saved = Profile(new DeckButton { Id = "b1", Action = Act("hotkey", ("keys", "^c")) });
            var incoming = Profile(new DeckButton { Id = "b1", Action = Act("hotkey", ("keys", "^{ESC}powershell{ENTER}")) });

            Assert.NotNull(ActionPolicy.FindRemoteViolation(incoming, saved));
        }

        [Fact]
        public void Remoto_NaoPodeTransformarBotaoComumEmPrivilegiado()
        {
            var saved = Profile(new DeckButton { Id = "b1", Action = Act("media", ("command", "next")) });
            var incoming = Profile(new DeckButton { Id = "b1", Action = Act("hotkey", ("keys", "^c")) });

            Assert.NotNull(ActionPolicy.FindRemoteViolation(incoming, saved));
        }

        [Fact]
        public void Remoto_PodeMoverERenomearBotaoPrivilegiadoSemMudarAAcao()
        {
            var saved = Profile(new DeckButton { Id = "b1", Row = 0, Label = "Calc", Action = Act("launch_app", ("path", "calc.exe")) });
            var incoming = Profile(
                new DeckButton { Id = "b1", Row = 2, Label = "Calculadora", BackgroundColor = "#FF0000", Action = Act("launch_app", ("path", "calc.exe")) },
                new DeckButton { Id = "b2", Action = Act("discord", ("operation", "toggleMute")) });

            Assert.Null(ActionPolicy.FindRemoteViolation(incoming, saved));
        }

        [Fact]
        public void Remoto_PodeApagarBotaoPrivilegiado()
        {
            var saved = Profile(new DeckButton { Id = "b1", Action = Act("hotkey", ("keys", "^c")) });

            Assert.Null(ActionPolicy.FindRemoteViolation(Profile(), saved));
        }

        [Fact]
        public void IsLocal_SoAceitaLoopback()
        {
            Assert.True(ActionPolicy.IsLocal(IPAddress.Loopback));
            Assert.True(ActionPolicy.IsLocal(IPAddress.IPv6Loopback));
            Assert.True(ActionPolicy.IsLocal(IPAddress.Loopback.MapToIPv6()));
            Assert.False(ActionPolicy.IsLocal(IPAddress.Parse("192.168.0.20")));
            Assert.False(ActionPolicy.IsLocal(null));
        }
    }

    /// <summary>
    /// O TestServer nao tem IP remoto, entao as requisicoes contam como vindas de fora do PC
    /// -- exatamente o caso do celular. Nenhum destes testes grava perfil.
    /// </summary>
    public class ActionPolicyEndpointTests : IClassFixture<WebApplicationFactory<DroidDeck.Program>>
    {
        private readonly WebApplicationFactory<DroidDeck.Program> _factory;

        public ActionPolicyEndpointTests(WebApplicationFactory<DroidDeck.Program> factory) => _factory = factory;

        private System.Net.Http.HttpClient Client()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", ApiKeyProvider.GetKey());
            return client;
        }

        [Fact]
        public async Task ExecuteRemoto_DeAcaoPrivilegiada_E403()
        {
            var response = await Client().PostAsJsonAsync("/api/StreamDeck/execute",
                new { type = "launch_app", parameters = new { path = "" } });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ExecuteRemoto_DeAcaoComum_ContinuaLiberado()
        {
            var response = await Client().PostAsJsonAsync("/api/StreamDeck/execute",
                new { type = "none", parameters = new { } });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Press_DeBotaoInexistente_E404()
        {
            var response = await Client().PostAsJsonAsync("/api/StreamDeck/press",
                new { profileId = "nao-existe", buttonId = "nada" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
