using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using DroidDeck.Integrations;
using DroidDeck.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DroidDeck.Tests
{
    /// <summary>
    /// Servidor WebSocket de mentira (Kestrel numa porta livre de 127.0.0.1) que responde cada
    /// mensagem JSON com o que <c>reply</c> devolver. Faz o papel do Streamer.bot e do VTube Studio.
    /// </summary>
    internal sealed class FakeWsServer : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public ConcurrentQueue<JsonNode> Received { get; } = new();

        public string Url { get; }

        private FakeWsServer(WebApplication app, string url)
        {
            _app = app;
            Url = url;
        }

        /// <param name="hello">Mensagem mandada logo ao conectar (null = nenhuma).</param>
        /// <param name="reply">Resposta a cada mensagem recebida (null = nao responde).</param>
        public static async Task<FakeWsServer> StartAsync(JsonNode? hello, Func<JsonNode, JsonNode?> reply)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.UseWebSockets();
            FakeWsServer? server = null;
            app.Run(async ctx =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                if (hello != null) await Send(ws, hello);
                var buffer = new byte[64 * 1024];
                while (ws.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult r;
                    do
                    {
                        try { r = await ws.ReceiveAsync(buffer, ctx.RequestAborted); }
                        catch { return; }
                        if (r.MessageType == WebSocketMessageType.Close) return;
                        ms.Write(buffer, 0, r.Count);
                    } while (!r.EndOfMessage);
                    var msg = JsonNode.Parse(ms.ToArray())!;
                    server!.Received.Enqueue(msg);
                    if (reply(msg) is { } answer) await Send(ws, answer);
                }
            });
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            server = new FakeWsServer(app, address.Replace("http://", "ws://") + "/");
            return server;
        }

        private static Task Send(WebSocket ws, JsonNode msg) =>
            ws.SendAsync(Encoding.UTF8.GetBytes(msg.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    internal static class Fakes
    {
        public static IHubContext<DeckHub> Hub() => new Mock<IHubContext<DeckHub>> { DefaultValue = DefaultValue.Mock }.Object;

        public static StreamDeckConfigService Config() => new(NullLogger<StreamDeckConfigService>.Instance);

        public static async Task Until(Func<bool> condition, int seconds = 10)
        {
            for (var i = 0; i < seconds * 20 && !condition(); i++) await Task.Delay(50);
            Assert.True(condition());
        }
    }

    [Collection(RealTimeNetworkCollection.Name)]
    public class StreamerBotTests
    {
        [Fact]
        public void Autenticacao_DuasVezesSha256ComSalEDesafio()
        {
            var secret = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("senha" + "sal")));
            var expected = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(secret + "desafio")));
            Assert.Equal(expected, StreamerBotIntegration.AuthResponse("senha", "sal", "desafio"));
        }

        [Fact]
        public void DoAction_PorIdOuPorNome()
        {
            var byId = StreamerBotIntegration.BuildDoAction("1", "f6b7a3a2-1c2d-4e5f-8a9b-0c1d2e3f4a5b", "");
            Assert.Equal("f6b7a3a2-1c2d-4e5f-8a9b-0c1d2e3f4a5b", byId["action"]!["id"]!.GetValue<string>());
            Assert.False(byId.ContainsKey("args"));

            var byName = StreamerBotIntegration.BuildDoAction("2", " Clipe ", "user=gabriel\nquantidade = 3\nlixo");
            Assert.Equal("Clipe", byName["action"]!["name"]!.GetValue<string>());
            Assert.Equal("gabriel", byName["args"]!["user"]!.GetValue<string>());
            Assert.Equal("3", byName["args"]!["quantidade"]!.GetValue<string>());
            Assert.Equal("DoAction", byName["request"]!.GetValue<string>());
        }

        [Fact]
        public void Argumentos_EmJson()
        {
            Assert.Equal(5, StreamerBotIntegration.ParseArgs("""{"n":5}""")!["n"]!.GetValue<int>());
            Assert.Throws<InvalidOperationException>(() => StreamerBotIntegration.ParseArgs("{quebrado"));
            Assert.Null(StreamerBotIntegration.ParseArgs("  "));
        }

        [Fact]
        public void ListaDeAcoes_SoAtivas_ComGrupo()
        {
            var list = StreamerBotIntegration.ParseActions(JsonNode.Parse("""
                {"id":"x","status":"ok","actions":[
                  {"id":"a1","name":"Clipe","group":"Twitch","enabled":true},
                  {"id":"a2","name":"Velha","enabled":false},
                  {"id":"a3","name":"Alerta","group":"","enabled":true}]}
                """)!);
            Assert.Equal(new[] { "Alerta", "Twitch / Clipe" }, list.Select(o => o.Label));
            Assert.Equal(new[] { "a3", "a1" }, list.Select(o => o.Value));
        }

        [Theory]
        [InlineData("", "ws://127.0.0.1:8080/")]
        [InlineData("192.168.0.5:8080", "ws://192.168.0.5:8080/")]
        public void Url(string input, string expected) => Assert.Equal(expected, StreamerBotIntegration.ParseUrl(input).ToString());

        [Fact]
        public void UrlHttp_Lanca() => Assert.Throws<InvalidOperationException>(() => StreamerBotIntegration.ParseUrl("http://x:8080"));

        [Fact]
        public async Task ComSenha_AutenticaDisparaAcaoELista()
        {
            const string salt = "c2FsdA==", challenge = "ZGVzYWZpbw==";
            var expectedAuth = StreamerBotIntegration.AuthResponse("segredo", salt, challenge);
            var hello = new JsonObject
            {
                ["timestamp"] = "2026-01-01",
                ["request"] = "Hello",
                ["info"] = new JsonObject { ["version"] = "0.2.5" },
                ["authentication"] = new JsonObject { ["salt"] = salt, ["challenge"] = challenge },
            };
            await using var server = await FakeWsServer.StartAsync(hello, msg =>
            {
                var id = msg["id"]?.GetValue<string>();
                return msg["request"]?.GetValue<string>() switch
                {
                    "Authenticate" => new JsonObject { ["id"] = id, ["status"] = msg["authentication"]?.GetValue<string>() == expectedAuth ? "ok" : "error" },
                    "DoAction" => new JsonObject { ["id"] = id, ["status"] = "ok" },
                    "GetActions" => new JsonObject
                    {
                        ["id"] = id,
                        ["status"] = "ok",
                        ["actions"] = new JsonArray(new JsonObject { ["id"] = "a1", ["name"] = "Clipe", ["group"] = "", ["enabled"] = true }),
                    },
                    _ => null,
                };
            });

            var bot = new StreamerBotIntegration(NullLogger<StreamerBotIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await bot.StartAsync(CancellationToken.None);
            try
            {
                var status = await bot.ConfigureAsync(new() { ["url"] = server.Url, ["password"] = "segredo" });
                Assert.True(status.Connected, status.Message);
                Assert.Equal("Streamer.bot 0.2.5", status.Account);

                await new StreamerBotProvider(bot).ExecuteAsync(Ctx.Of("streamerbot", ("action", "Clipe"), ("args", "x=1")));
                var doAction = server.Received.Single(m => m["request"]?.GetValue<string>() == "DoAction");
                Assert.Equal("Clipe", doAction["action"]!["name"]!.GetValue<string>());
                Assert.Equal("1", doAction["args"]!["x"]!.GetValue<string>());

                var options = await bot.OptionsAsync("actions", new QueryCollection());
                Assert.Equal("a1", Assert.Single(options).Value);
            }
            finally
            {
                await bot.LogoutAsync();
                await bot.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task SenhaErrada_NaoConecta()
        {
            var hello = JsonNode.Parse("""{"request":"Hello","authentication":{"salt":"a","challenge":"b"}}""");
            await using var server = await FakeWsServer.StartAsync(hello, msg =>
                new JsonObject { ["id"] = msg["id"]?.GetValue<string>(), ["status"] = "error", ["error"] = "Authentication failed" });

            var bot = new StreamerBotIntegration(NullLogger<StreamerBotIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await bot.StartAsync(CancellationToken.None);
            try
            {
                var status = await bot.ConfigureAsync(new() { ["url"] = server.Url, ["password"] = "errada" });
                Assert.False(status.Connected);
                Assert.Equal("Senha recusada pelo Streamer.bot.", status.Message);
            }
            finally
            {
                await bot.LogoutAsync();
                await bot.StopAsync(CancellationToken.None);
            }
        }
    }

    [Collection(RealTimeNetworkCollection.Name)]
    public class VTubeStudioTests
    {
        [Fact]
        public void Envelope_DaApiPublica()
        {
            var msg = VTubeStudioIntegration.Envelope("r1", "HotkeyTriggerRequest", new JsonObject { ["hotkeyID"] = "Sorriso" });
            Assert.Equal("""{"apiName":"VTubeStudioPublicAPI","apiVersion":"1.0","requestID":"r1","messageType":"HotkeyTriggerRequest","data":{"hotkeyID":"Sorriso"}}""",
                msg.ToJsonString());
            Assert.False(VTubeStudioIntegration.Envelope("r2", "CurrentModelRequest", null).ContainsKey("data"));
        }

        [Fact]
        public void Erro_ViraExcecao()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => VTubeStudioIntegration.ThrowIfError(JsonNode.Parse(
                """{"messageType":"APIError","data":{"errorID":50,"message":"User has denied API access for your plugin."}}""")!));
            Assert.Contains("denied", ex.Message);
        }

        [Fact]
        public void Listas_AtalhosEModelos()
        {
            var hotkeys = VTubeStudioIntegration.ParseHotkeys(JsonNode.Parse("""
                {"data":{"modelLoaded":true,"availableHotkeys":[
                  {"name":"Sorriso","type":"ToggleExpression","hotkeyID":"h1"},
                  {"name":"","type":"TriggerAnimation","hotkeyID":"h2"}]}}
                """)!);
            Assert.Equal(new[] { "Sorriso (ToggleExpression)", "h2 (TriggerAnimation)" }, hotkeys.Select(o => o.Label));

            var models = VTubeStudioIntegration.ParseModels(JsonNode.Parse("""
                {"data":{"availableModels":[{"modelName":"Hiyori","modelID":"m2"},{"modelName":"Akari","modelID":"m1"}]}}
                """)!);
            Assert.Equal(new[] { "m1", "m2" }, models.Select(o => o.Value));
        }

        private static JsonNode Reply(JsonNode request, string type, JsonObject data) =>
            VTubeStudioIntegration.Envelope(request["requestID"]!.GetValue<string>(), type, data);

        [Fact]
        public async Task PedeToken_Autentica_EDisparaAtalho()
        {
            await using var server = await FakeWsServer.StartAsync(null, msg => msg["messageType"]?.GetValue<string>() switch
            {
                "AuthenticationTokenRequest" => Reply(msg, "AuthenticationTokenResponse", new JsonObject { ["authenticationToken"] = "tok" }),
                "AuthenticationRequest" => Reply(msg, "AuthenticationResponse", new JsonObject
                {
                    ["authenticated"] = msg["data"]?["authenticationToken"]?.GetValue<string>() == "tok",
                    ["reason"] = "ok",
                }),
                "CurrentModelRequest" => Reply(msg, "CurrentModelResponse", new JsonObject { ["modelName"] = "Hiyori" }),
                "HotkeyTriggerRequest" => Reply(msg, "HotkeyTriggerResponse", new JsonObject { ["hotkeyID"] = "h1" }),
                _ => null,
            });

            var vts = new VTubeStudioIntegration(NullLogger<VTubeStudioIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await vts.StartAsync(CancellationToken.None);
            try
            {
                await vts.ConfigureAsync(new() { ["url"] = server.Url });
                await Fakes.Until(() => vts.GetStatus().Connected);
                Assert.Equal("Hiyori", vts.GetStatus().Account);
                Assert.True(vts.GetStatus().Configured);

                await new VTubeStudioProvider(vts).ExecuteAsync(Ctx.Of("vtubestudio", ("operation", "hotkey"), ("hotkey", "h1")));
                var trigger = server.Received.Single(m => m["messageType"]?.GetValue<string>() == "HotkeyTriggerRequest");
                Assert.Equal("h1", trigger["data"]!["hotkeyID"]!.GetValue<string>());
                Assert.Equal("DroidDeck", server.Received.First(m => m["messageType"]?.GetValue<string>() == "AuthenticationTokenRequest")["data"]!["pluginName"]!.GetValue<string>());
            }
            finally
            {
                await vts.LogoutAsync();
                await vts.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task Negado_NaoPedeDeNovoSozinho()
        {
            await using var server = await FakeWsServer.StartAsync(null, msg =>
                Reply(msg, "APIError", new JsonObject { ["errorID"] = 50, ["message"] = "User has denied API access for your plugin." }));

            var vts = new VTubeStudioIntegration(NullLogger<VTubeStudioIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await vts.StartAsync(CancellationToken.None);
            try
            {
                await vts.ConfigureAsync(new() { ["url"] = server.Url });
                await Fakes.Until(() => vts.GetStatus().Message == "O VTube Studio negou o acesso. Toque em Conectar para pedir de novo.");
                Assert.False(vts.GetStatus().Connected);
                // Passado o intervalo de reconexao, continua um pedido so.
                await Task.Delay(TimeSpan.FromSeconds(6));
                Assert.Single(server.Received, m => m["messageType"]?.GetValue<string>() == "AuthenticationTokenRequest");
            }
            finally
            {
                await vts.LogoutAsync();
                await vts.StopAsync(CancellationToken.None);
            }
        }
    }
}
