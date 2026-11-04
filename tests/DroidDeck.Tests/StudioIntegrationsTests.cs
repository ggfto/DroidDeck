using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Actions;
using DroidDeck.Actions.Providers;
using DroidDeck.Auth;
using DroidDeck.Integrations;
using DroidDeck.Lib;
using DroidDeck.Models;
using DroidDeck.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DroidDeck.Tests
{
    /// <summary>Servidor TCP de mentira, de protocolo em linhas: faz o papel do vMix e do Companion.</summary>
    internal sealed class FakeLineServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private NetworkStream? _stream;

        public ConcurrentQueue<string> Received { get; } = new();

        public int Port { get; }

        public FakeLineServer(Func<string, FakeLineServer, Task> onLine, Func<FakeLineServer, Task>? onConnect = null)
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        using var tcp = await _listener.AcceptTcpClientAsync(_cts.Token);
                        _stream = tcp.GetStream();
                        if (onConnect != null) await onConnect(this);
                        using var reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
                        try
                        {
                            string? line;
                            while ((line = await reader.ReadLineAsync(_cts.Token)) != null)
                            {
                                Received.Enqueue(line);
                                await onLine(line, this);
                            }
                        }
                        // DropClient fechou esta conexao: volta a esperar a proxima.
                        catch (Exception e) when (e is IOException or ObjectDisposedException) { }
                    }
                }
                catch (Exception e) when (e is OperationCanceledException or IOException or SocketException) { }
            });
        }

        public Task SendAsync(string text) => _stream!.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();

        public void DropClient() => _stream?.Close();

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try { await _loop; }
            catch { }
        }
    }

    /// <summary>Servidor HTTP de mentira (mAirList, RadioDJ).</summary>
    internal sealed class FakeHttpServer : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public string Url { get; }

        private FakeHttpServer(WebApplication app, string url)
        {
            _app = app;
            Url = url;
        }

        public static async Task<FakeHttpServer> StartAsync(RequestDelegate handler)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.Run(handler);
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            return new FakeHttpServer(app, address);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    internal sealed class SimpleHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    /// <summary>O HttpClient da factory registra a URL no log; o RadioDJ leva a senha na URL.</summary>
    internal sealed class ForbiddenHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("O RadioDJ não pode usar o HttpClient da factory.");
    }

    // ---- Rede: OSC, TCP e UDP ----

    public class NetworkProviderTests
    {
        [Fact]
        public void Osc_Inteiro_BytesDaEspecificacao()
        {
            var bytes = OscMessage.Encode("/a", new List<object> { 1 });
            Assert.Equal(new byte[]
            {
                (byte)'/', (byte)'a', 0, 0,
                (byte)',', (byte)'i', 0, 0,
                0, 0, 0, 1,
            }, bytes);
        }

        [Fact]
        public void Osc_TextoComTamanhoMultiploDeQuatro_LevaQuatroZeros()
        {
            var bytes = OscMessage.Encode("/abc", new List<object> { "wxyz" });
            // "/abc" + 4 zeros, ",s" + 2 zeros, "wxyz" + 4 zeros.
            Assert.Equal(8 + 4 + 8, bytes.Length);
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, bytes[^4..]);
        }

        [Fact]
        public void Osc_Decimal_BigEndian_E_BoolSemCorpo()
        {
            var bytes = OscMessage.Encode("/a", new List<object> { 0.5f, true, false });
            Assert.Equal(",fTF", Encoding.ASCII.GetString(bytes, 4, 4));
            // ",fTF" ocupa 4 bytes e ganha mais 4 zeros de terminador.
            Assert.Equal(new byte[] { 0x3F, 0x00, 0x00, 0x00 }, bytes[12..16]);
            Assert.Equal(16, bytes.Length);
        }

        [Fact]
        public void Osc_Argumentos_PrefixoEDeducao()
        {
            var args = OscMessage.ParseArgs("i:3\nf:0.25\ns:12\nT\nF\n7\n1.5\ncasa");
            Assert.Equal(new object[] { 3, 0.25f, "12", true, false, 7, 1.5f, "casa" }, args);
        }

        [Theory]
        [InlineData("sem-barra")]
        [InlineData("/com espaco")]
        [InlineData("")]
        public void Osc_CaminhoInvalido_Lanca(string path) =>
            Assert.Throws<InvalidOperationException>(() => OscMessage.Encode(path, new List<object>()));

        [Fact]
        public void Osc_NumeroInvalido_Lanca() =>
            Assert.Throws<InvalidOperationException>(() => OscMessage.ParseArgs("i:abc"));

        [Fact]
        public void Mensagem_FimDeLinha_E_Hex()
        {
            Assert.Equal("PLAY\r\n", Encoding.UTF8.GetString(NetworkProvider.BuildPayload("PLAY", "text", "crlf")));
            Assert.Equal("PLAY", Encoding.UTF8.GetString(NetworkProvider.BuildPayload("PLAY", "text", "none")));
            Assert.Equal(new byte[] { 0xDE, 0xAD, 0x01 }, NetworkProvider.BuildPayload("DE AD\n01", "hex", "lf"));
            Assert.Throws<InvalidOperationException>(() => NetworkProvider.BuildPayload("xyz", "hex", "none"));
        }

        [Fact]
        public async Task Udp_ChegaNoDestino()
        {
            using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
            await new NetworkProvider().ExecuteAsync(Ctx.Of("network",
                ("protocol", "osc"), ("host", "127.0.0.1"), ("port", port.ToString()), ("path", "/ch/01/mix/on"), ("args", "i:0")));
            var got = await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.StartsWith("/ch/01/mix/on", Encoding.ASCII.GetString(got.Buffer));
        }

        [Fact]
        public async Task Tcp_ChegaNoDestino()
        {
            await using var server = new FakeLineServer((_, _) => Task.CompletedTask);
            await new NetworkProvider().ExecuteAsync(Ctx.Of("network",
                ("protocol", "tcp"), ("host", "127.0.0.1"), ("port", server.Port.ToString()), ("message", "GO"), ("ending", "lf")));
            await Fakes.Until(() => server.Received.Contains("GO"));
        }

        [Fact]
        public async Task SemEndereco_Lanca() =>
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new NetworkProvider().ExecuteAsync(Ctx.Of("network", ("host", ""), ("port", "1"))));

        [Fact]
        public void EhPrivilegiada() => Assert.True(ActionPolicy.IsPrivileged(new DeckAction { Type = "network" }));

        [Fact]
        public void Endereco()
        {
            Assert.Equal(("10.0.0.5", 8099), TcpLineClient.ParseAddress("10.0.0.5", "127.0.0.1", 8099));
            Assert.Equal(("estudio.local", 9000), TcpLineClient.ParseAddress(" estudio.local:9000 ", "127.0.0.1", 8099));
            Assert.Equal(("127.0.0.1", 8099), TcpLineClient.ParseAddress("", "127.0.0.1", 8099));
            Assert.Throws<InvalidOperationException>(() => TcpLineClient.ParseAddress("a/b?c", "127.0.0.1", 8099));
        }
    }

    // ---- vMix ----

    public class VmixTests
    {
        // Exemplo da documentacao da Web API do vMix, encurtado.
        private const string Xml = """
            <vmix>
              <version>29.0.0.1</version>
              <inputs>
                <input key="aaa" number="1" type="Xaml" title="Camera 1" state="Paused" muted="True" />
                <input key="bbb" number="2" type="Video" title="Vinheta" state="Running" muted="False" />
                <input key="ccc" number="3" type="Capture" title="Camera 2" state="Running" />
              </inputs>
              <overlays>
                <overlay number="2">3</overlay>
                <overlay number="1"/>
              </overlays>
              <preview>1</preview>
              <active>2</active>
              <fadeToBlack>True</fadeToBlack>
              <recording>False</recording>
              <external>False</external>
              <streaming>True</streaming>
              <multiCorder>False</multiCorder>
            </vmix>
            """;

        [Fact]
        public void Xml_Interpretado()
        {
            var s = VmixIntegration.ParseXml(Xml);
            Assert.Equal("29.0.0.1", s.Version);
            Assert.Equal(3, s.Inputs.Count);
            Assert.True(s.Inputs[0].Muted);
            Assert.False(s.Inputs[2].Muted);
            Assert.Equal("bbb", s.KeyOf(s.Active));
            Assert.Equal("aaa", s.KeyOf(s.Preview));
            Assert.True(s.Streaming);
            Assert.False(s.Recording);
            Assert.True(s.FadeToBlack);
            Assert.Equal(new[] { 0, 3 }, s.Overlays);
        }

        [Fact]
        public void Xml_ComDtd_Recusado() =>
            Assert.ThrowsAny<System.Xml.XmlException>(() => VmixIntegration.ParseXml("<!DOCTYPE x [<!ENTITY a 'b'>]><vmix/>"));

        [Theory]
        [InlineData("cut", "Cut", "")]
        [InlineData("toggle_record", "StartStopRecording", "")]
        [InlineData("toggle_stream", "StartStopStreaming", "")]
        [InlineData("fade_to_black", "FadeToBlack", "")]
        [InlineData("master_audio", "MasterAudio", "")]
        [InlineData("overlay_off", "OverlayInputAllOff", "")]
        public void Funcoes_SemEntrada(string op, string function, string query) =>
            Assert.Equal((function, query), VmixProvider.Build(Ctx.Of("vmix", ("operation", op))));

        [Fact]
        public void Funcoes_ComEntrada_EscapamOValor()
        {
            Assert.Equal(("PreviewInput", "Input=Camera%20%26%201"),
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "preview"), ("input", "Camera & 1"))));
            Assert.Equal(("ActiveInput", "Input=aaa"),
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "program"), ("input", "aaa"))));
            Assert.Equal(("OverlayInput3", "Input=aaa"),
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "overlay"), ("input", "aaa"), ("overlay", "3"))));
            Assert.Equal(("SetVolume", "Input=aaa&Value=50"),
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "set_volume"), ("input", "aaa"), ("volume", "50"))));
            Assert.Equal(("Fade", "Duration=500"),
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "fade"), ("duration", "500"))));
        }

        [Fact]
        public void SemEntrada_Lanca() =>
            Assert.Throws<InvalidOperationException>(() => VmixProvider.Build(Ctx.Of("vmix", ("operation", "preview"))));

        [Fact]
        public void Outra_NaoInjetaComando()
        {
            Assert.Throws<InvalidOperationException>(() =>
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "custom"), ("function", "Cut\r\nQUIT"))));
            Assert.Throws<InvalidOperationException>(() =>
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "custom"), ("function", "Cut"), ("query", "a=1\r\nQUIT"))));
            Assert.Equal(("SetText", "Input=1&Value=Oi"),
                VmixProvider.Build(Ctx.Of("vmix", ("operation", "custom"), ("function", "SetText"), ("query", "Input=1&Value=Oi"))));
        }

        private static async Task Reply(string line, FakeLineServer s, Func<string> xml, bool[]? refuse = null)
        {
            var command = line.Split(' ')[0];
            switch (command)
            {
                case "SUBSCRIBE":
                    await s.SendAsync("SUBSCRIBE OK TALLY\r\n");
                    await s.SendAsync("TALLY OK 201\r\n");
                    break;
                case "XML":
                    var bytes = Encoding.UTF8.GetByteCount(xml());
                    await s.SendAsync($"XML {bytes}\r\n{xml()}");
                    break;
                case "FUNCTION":
                    await s.SendAsync(refuse?[0] == true ? "FUNCTION ER Input not found\r\n" : "FUNCTION OK Completed\r\n");
                    break;
            }
        }

        [Fact]
        public async Task PontaAPonta_Tally_Estado_Funcao_E_Queda()
        {
            var refuse = new[] { false };
            await using var server = new FakeLineServer((line, s) => Reply(line, s, () => Xml, refuse));
            var vmix = new VmixIntegration(NullLogger<VmixIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await vmix.StartAsync(CancellationToken.None);
            try
            {
                await vmix.ConfigureAsync(new() { ["host"] = "127.0.0.1:" + server.Port });
                await Fakes.Until(() => vmix.IsConnected);
                Assert.Equal("vMix 29.0.0.1", vmix.AccountName);

                // A resposta binaria do XML nao pode bagunçar a leitura das linhas seguintes.
                await Fakes.Until(() => vmix.TallyOf(1) == VmixIntegration.TallyPreview);
                Assert.Equal(VmixIntegration.TallyOff, vmix.TallyOf(2));
                Assert.Equal(VmixIntegration.TallyProgram, vmix.TallyOf(3));
                Assert.Equal(VmixIntegration.TallyOff, vmix.TallyOf(99));

                await new VmixProvider(vmix).ExecuteAsync(Ctx.Of("vmix", ("operation", "preview"), ("input", "ccc")));
                Assert.Contains("FUNCTION PreviewInput Input=ccc", server.Received);

                refuse[0] = true;
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    new VmixProvider(vmix).ExecuteAsync(Ctx.Of("vmix", ("operation", "cut"))));
                Assert.Equal("Input not found", error.Message);

                var options = await vmix.OptionsAsync("inputs", new QueryCollection());
                Assert.Equal("1. Camera 1", options[0].Label);
                Assert.Equal("aaa", options[0].Value);

                // Caiu: reconecta sozinho e assina o tally de novo.
                server.DropClient();
                await Fakes.Until(() => server.Received.Count(l => l.StartsWith("SUBSCRIBE")) == 2);
            }
            finally
            {
                await vmix.LogoutAsync();
                await vmix.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task SemServidor_ExplicaOErro()
        {
            var vmix = new VmixIntegration(NullLogger<VmixIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await vmix.StartAsync(CancellationToken.None);
            try
            {
                // Porta fechada.
                var free = new TcpListener(IPAddress.Loopback, 0);
                free.Start();
                var port = ((IPEndPoint)free.LocalEndpoint).Port;
                free.Stop();
                var status = await vmix.ConfigureAsync(new() { ["host"] = "127.0.0.1:" + port });
                Assert.False(status.Connected);
                Assert.Contains("vMix", status.Message);
                await Assert.ThrowsAsync<InvalidOperationException>(() => vmix.FunctionAsync("Cut", ""));
            }
            finally
            {
                await vmix.LogoutAsync();
                await vmix.StopAsync(CancellationToken.None);
            }
        }
    }

    // ---- Radio ----

    public class MairlistTests
    {
        [Theory]
        [InlineData("on_air_toggle", "ON/OFF AIR")]
        [InlineData("on_air", "ON AIR")]
        [InlineData("off_air", "OFF AIR")]
        [InlineData("automation_toggle", "AUTOMATION 2 ON/OFF")]
        [InlineData("next", "PLAYLIST 2 NEXT")]
        [InlineData("player_toggle", "PLAYER 2-3 START/STOP")]
        [InlineData("player_fadeout", "PLAYER 2-3 FADEOUT")]
        [InlineData("cart", "CARTWALL 7 CLICK")]
        [InlineData("cart_stop_all", "CARTWALL ALL STOP")]
        public void Comandos(string op, string expected) =>
            Assert.Equal(expected, MairlistProvider.BuildCommand(Ctx.Of("mairlist",
                ("operation", op), ("playlist", "2"), ("player", "3"), ("cart", "7"))));

        [Fact]
        public void Outro_ComQuebraDeLinha_Lanca() =>
            Assert.Throws<InvalidOperationException>(() => MairlistProvider.BuildCommand(Ctx.Of("mairlist",
                ("operation", "custom"), ("command", "ON AIR\nOFF AIR"))));

        private static async Task<(MairlistIntegration mairlist, FakeHttpServer server, ConcurrentQueue<string> calls)> Start(string onAir, string password = "segredo")
        {
            var calls = new ConcurrentQueue<string>();
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("dj:" + password));
            var state = new[] { onAir };
            var server = await FakeHttpServer.StartAsync(async ctx =>
            {
                if (ctx.Request.Headers.Authorization != expected)
                {
                    ctx.Response.StatusCode = 401;
                    return;
                }
                if (ctx.Request.Path == "/status/onair")
                {
                    await ctx.Response.WriteAsync(state[0]);
                }
                else if (ctx.Request.Path == "/execute" && ctx.Request.Method == "POST")
                {
                    var form = await ctx.Request.ReadFormAsync();
                    calls.Enqueue(form["command"].ToString());
                    if (form["command"] == "ON AIR") state[0] = "1";
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            });
            var mairlist = new MairlistIntegration(NullLogger<MairlistIntegration>.Instance, Fakes.Hub(), Fakes.Config(), new SimpleHttpFactory());
            await mairlist.StartAsync(CancellationToken.None);
            return (mairlist, server, calls);
        }

        [Fact]
        public async Task PontaAPonta_Estado_E_Comando()
        {
            var (mairlist, server, calls) = await Start("0");
            await using (server)
            {
                try
                {
                    var status = await mairlist.ConfigureAsync(new() { ["url"] = server.Url, ["username"] = "dj", ["password"] = "segredo" });
                    Assert.True(status.Connected);
                    Assert.False(mairlist.OnAir);

                    await new MairlistProvider(mairlist).ExecuteAsync(Ctx.Of("mairlist", ("operation", "on_air")));
                    Assert.Contains("ON AIR", calls);
                    await Fakes.Until(() => mairlist.OnAir == true);
                    // A senha nao volta para a tela.
                    Assert.False(mairlist.GetStatus().Fields.ContainsKey("password"));
                }
                finally
                {
                    await mairlist.LogoutAsync();
                    await mairlist.StopAsync(CancellationToken.None);
                }
            }
        }

        [Fact]
        public async Task SenhaErrada_DizQueFoiASenha()
        {
            var (mairlist, server, _) = await Start("0");
            await using (server)
            {
                try
                {
                    var status = await mairlist.ConfigureAsync(new() { ["url"] = server.Url, ["username"] = "dj", ["password"] = "outra" });
                    Assert.False(status.Connected);
                    Assert.Equal("Usuário ou senha recusados pelo mAirList.", status.Message);
                    Assert.Null(mairlist.OnAir);
                }
                finally
                {
                    await mairlist.LogoutAsync();
                    await mairlist.StopAsync(CancellationToken.None);
                }
            }
        }

        [Theory]
        [InlineData("127.0.0.1:9300", "http://127.0.0.1:9300")]
        [InlineData("http://estudio:9300/", "http://estudio:9300")]
        [InlineData("", "http://127.0.0.1:9300")]
        public void Url(string input, string expected) =>
            Assert.Equal(expected, PolledHttpIntegration.NormalizeUrl(input, MairlistIntegration.DefaultUrl));

        [Fact]
        public void Url_ForaDeHttp_Lanca() =>
            Assert.Throws<InvalidOperationException>(() => PolledHttpIntegration.NormalizeUrl("ftp://x", MairlistIntegration.DefaultUrl));
    }

    public class RadioDjTests
    {
        // Exemplo do /np no topico do plugin REST Server, encurtado.
        private const string NowPlaying = """
            <SongData xmlns="http://schemas.datacontract.org/2004/07/rdjInterface" xmlns:i="http://www.w3.org/2001/XMLSchema-instance">
              <Album>Disco</Album><Artist>Sia</Artist><ID>1</ID><Title>Extraordinary</Title><Remaining>59.5</Remaining>
            </SongData>
            """;

        [Theory]
        [InlineData("true", true)]
        [InlineData("FALSE", false)]
        [InlineData("<boolean xmlns=\"x\">true</boolean>", true)]
        [InlineData("<?xml version=\"1.0\"?><b>false</b>", false)]
        [InlineData("OK", null)]
        [InlineData("", null)]
        // Nome de tag nao conta como valor.
        [InlineData("<true>nada</true>", null)]
        public void Booleano(string text, bool? expected) => Assert.Equal(expected, RadioDjIntegration.ParseBool(text));

        [Fact]
        public void TocandoAgora_ComNamespace() =>
            Assert.Equal(("Extraordinary", "Sia"), RadioDjIntegration.ParseNowPlaying(NowPlaying));

        [Theory]
        [InlineData("stop", "StopPlayer", null)]
        [InlineData("pause", "PausePlayer", "1")]
        [InlineData("resume", "PausePlayer", "0")]
        [InlineData("restart", "RestartPlayer", null)]
        [InlineData("play_intro", "PlayFromIntro", null)]
        [InlineData("events_on", "EnableEvents", "1")]
        public void Comandos(string op, string command, string? arg) =>
            Assert.Equal((command, arg), RadioDjProvider.Build(Ctx.Of("radiodj", ("operation", op)), null, null, null));

        [Fact]
        public void Faixa_ContaDeUm_NoEditor_EDeZero_NoRadioDj() =>
            Assert.Equal(("PlayPlaylistTrack", "0"), RadioDjProvider.Build(Ctx.Of("radiodj", ("operation", "play_track"), ("track", "1")), null, null, null));

        [Fact]
        public void Alternar_InverteOEstadoLido()
        {
            Assert.Equal(("EnableAutoDJ", "0"), RadioDjProvider.Build(Ctx.Of("radiodj", ("operation", "autodj_toggle")), true, null, null));
            Assert.Equal(("EnableAssisted", "1"), RadioDjProvider.Build(Ctx.Of("radiodj", ("operation", "assisted_toggle")), null, false, null));
            Assert.Equal(("EnableInput", "1"), RadioDjProvider.Build(Ctx.Of("radiodj", ("operation", "input_toggle")), null, null, false));
        }

        [Fact]
        public void Alternar_SemEstado_NaoChuta() =>
            Assert.Throws<InvalidOperationException>(() => RadioDjProvider.Build(Ctx.Of("radiodj", ("operation", "input_toggle")), null, null, null));

        [Fact]
        public void Outro_SoNomeSimples() =>
            Assert.Throws<InvalidOperationException>(() => RadioDjProvider.Build(Ctx.Of("radiodj", ("operation", "custom"), ("command", "a&b=c")), null, null, null));

        [Fact]
        public async Task PontaAPonta()
        {
            var calls = new ConcurrentQueue<string>();
            var input = new[] { false };
            await using var server = await FakeHttpServer.StartAsync(async ctx =>
            {
                if (ctx.Request.Query["auth"] != "changeme")
                {
                    await ctx.Response.WriteAsync("Wrong auth");
                    return;
                }
                if (ctx.Request.Path == "/np")
                {
                    ctx.Response.ContentType = "text/xml";
                    await ctx.Response.WriteAsync(NowPlaying);
                    return;
                }
                var command = ctx.Request.Query["command"].ToString();
                var arg = ctx.Request.Query["arg"].ToString();
                if (!command.StartsWith("Status")) calls.Enqueue($"{command}:{arg}");
                if (command == "EnableInput") input[0] = arg == "1";
                await ctx.Response.WriteAsync(command switch
                {
                    "StatusAutoDJ" => "true",
                    "StatusAssisted" => "false",
                    "StatusInput" => input[0] ? "true" : "false",
                    _ => "OK",
                });
            });
            var radio = new RadioDjIntegration(NullLogger<RadioDjIntegration>.Instance, Fakes.Hub(), Fakes.Config(), new ForbiddenHttpFactory());
            await radio.StartAsync(CancellationToken.None);
            try
            {
                var status = await radio.ConfigureAsync(new() { ["url"] = server.Url, ["password"] = "changeme" });
                Assert.True(status.Connected);
                Assert.True(radio.AutoDj);
                Assert.False(radio.Assisted);
                Assert.False(radio.Input);

                await new RadioDjProvider(radio).ExecuteAsync(Ctx.Of("radiodj", ("operation", "input_toggle")));
                Assert.Contains("EnableInput:1", calls);
                await Fakes.Until(() => radio.Input == true);

                var state = JsonSerializer.Serialize(radio.GetState());
                Assert.Contains("Extraordinary", state);
                Assert.DoesNotContain("changeme", state);
            }
            finally
            {
                await radio.LogoutAsync();
                await radio.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task SenhaErrada_FicaDesconectado_ComOrientacao()
        {
            await using var server = await FakeHttpServer.StartAsync(ctx => ctx.Response.WriteAsync("Wrong auth"));
            var radio = new RadioDjIntegration(NullLogger<RadioDjIntegration>.Instance, Fakes.Hub(), Fakes.Config(), new ForbiddenHttpFactory());
            await radio.StartAsync(CancellationToken.None);
            try
            {
                var status = await radio.ConfigureAsync(new() { ["url"] = server.Url, ["password"] = "errada" });
                Assert.False(status.Connected);
                Assert.Contains("senha", status.Message);
                Assert.Null(radio.Input);
            }
            finally
            {
                await radio.LogoutAsync();
                await radio.StopAsync(CancellationToken.None);
            }
        }
    }

    // ---- Corte de tosse e tally ----

    internal sealed class FakeVoicemeeter : IVoicemeeterApi
    {
        public readonly Dictionary<string, float> Values = new();
        public int Strips = 3;
        public bool Closed;

        public float Get(string parameter)
        {
            if (Closed) throw new InvalidOperationException("O Voicemeeter não está aberto.");
            var index = int.Parse(parameter["Strip[".Length..parameter.IndexOf(']')]);
            if (index >= Strips) throw new InvalidOperationException("Parâmetro do Voicemeeter desconhecido.");
            return Values.GetValueOrDefault(parameter);
        }

        public void Set(string parameter, float value)
        {
            if (Closed) throw new InvalidOperationException("O Voicemeeter não está aberto.");
            Values[parameter] = value;
        }

        public void SetMacroButton(int button, float value) { }
    }

    public class MicHoldTests
    {
        private const string Mic = "Strip[0].Mute";
        private readonly FakeVoicemeeter _api = new();
        private readonly MicHoldService _hold;

        public MicHoldTests() => _hold = new MicHoldService(_api, NullLogger<MicHoldService>.Instance);

        [Fact]
        public void Segura_Corta_E_Solta_Volta()
        {
            _hold.Down("a", Mic);
            Assert.Equal(1, _api.Values[Mic]);
            _hold.Up("a", Mic);
            Assert.Equal(0, _api.Values[Mic]);
        }

        [Fact]
        public void JaEstavaMudo_ContinuaMudo()
        {
            _api.Values[Mic] = 1;
            _hold.Down("a", Mic);
            _hold.Up("a", Mic);
            Assert.Equal(1, _api.Values[Mic]);
        }

        [Fact]
        public void CelularCaiu_MicrofoneVolta()
        {
            _hold.Down("a", Mic);
            _hold.ReleaseOwner("a");
            Assert.Equal(0, _api.Values[Mic]);
        }

        [Fact]
        public void DoisCelulares_SoVoltaQuandoOUltimoSolta()
        {
            _hold.Down("a", Mic);
            _hold.Down("b", Mic);
            _hold.Up("a", Mic);
            Assert.Equal(1, _api.Values[Mic]);
            _hold.Up("b", Mic);
            Assert.Equal(0, _api.Values[Mic]);
        }

        [Fact]
        public void VoicemeeterFechado_NaoDerrubaOHub()
        {
            _api.Closed = true;
            _hold.Down("a", Mic);
            _hold.Up("a", Mic);
            _hold.ReleaseOwner("a");
        }

        [Fact]
        public void Parametro_DoBotao()
        {
            var action = new DeckAction
            {
                Type = "voicemeeter",
                Parameters = new() { ["operation"] = "hold_mute", ["target"] = "strip", ["index"] = "2" },
            };
            Assert.Equal("Strip[1].Mute", VoicemeeterProvider.HoldMuteParameter(action));
            action.Parameters["operation"] = "mute";
            Assert.Null(VoicemeeterProvider.HoldMuteParameter(action));
            Assert.Null(VoicemeeterProvider.HoldMuteParameter(new DeckAction { Type = "key" }));
        }

        [Fact]
        public async Task ToqueAvulso_NaoCortaNada()
        {
            await new VoicemeeterProvider(_api).ExecuteAsync(Ctx.Of("voicemeeter", ("operation", "hold_mute")));
            Assert.Empty(_api.Values);
        }
    }

    public class TallyTests
    {
        private static TallyTarget Find(IEnumerable<TallyTarget> list, string id) => list.Single(t => t.Id == id);

        [Fact]
        public void Obs_ProgramaPreviewEOcioso()
        {
            var list = TallyService.ObsTargets(new[] { "Cam 1", "Cam 2", "Cam 3" }, "Cam 1", "Cam 2", streaming: true, recording: false).ToList();
            Assert.Equal(TallyTarget.Program, Find(list, "obs/scene:Cam 1").State);
            Assert.Equal(TallyTarget.Preview, Find(list, "obs/scene:Cam 2").State);
            Assert.Equal(TallyTarget.Idle, Find(list, "obs/scene:Cam 3").State);
            Assert.Equal(TallyTarget.Program, Find(list, "obs/stream").State);
            Assert.Equal(TallyTarget.Idle, Find(list, "obs/record").State);
        }

        [Fact]
        public void Vmix_UsaOTallyDaEntrada_PelaChave()
        {
            var snapshot = VmixIntegration.ParseXml("""
                <vmix><inputs>
                  <input key="aaa" number="1" title="A"/>
                  <input key="bbb" number="2" title="B"/>
                  <input key="ccc" number="3" title="C"/>
                </inputs><streaming>False</streaming><recording>True</recording></vmix>
                """);
            var list = TallyService.VmixTargets(snapshot, n => n switch { 1 => 2, 3 => 1, _ => 0 }).ToList();
            Assert.Equal(TallyTarget.Preview, Find(list, "vmix/input:aaa").State);
            Assert.Equal(TallyTarget.Idle, Find(list, "vmix/input:bbb").State);
            Assert.Equal(TallyTarget.Program, Find(list, "vmix/input:ccc").State);
            Assert.Equal(TallyTarget.Program, Find(list, "vmix/record").State);
            Assert.Equal(TallyTarget.Idle, Find(list, "vmix/stream").State);
        }

        [Fact]
        public void Voicemeeter_AteAUltimaEntrada_AbertoEhDesmutado()
        {
            var api = new FakeVoicemeeter { Strips = 3 };
            api.Values["Strip[1].Mute"] = 1;
            Assert.Equal(new[] { true, false, true }, TallyService.ReadStrips(api));
            api.Closed = true;
            Assert.Empty(TallyService.ReadStrips(api));
        }
    }

    // ---- Tally no resto do deck: botao, condicoes e automacoes ----

    public class TallyDeckTests
    {
        // Uma instancia so: cada StreamDeckConfigService novo le todos os perfis do disco, e no
        // Windows essa leitura faz falhar a gravacao de outro teste que rode ao mesmo tempo.
        private static readonly StreamDeckConfigService SharedConfig = Fakes.Config();

        private sealed class Recorder : IActionProvider
        {
            public readonly ConcurrentQueue<string> Ran = new();
            public string Type => "rec";
            public ActionSchema Describe() => new() { Type = Type, Title = "rec" };
            public Task ExecuteAsync(ActionContext c)
            {
                Ran.Enqueue(c.Get("tag"));
                return Task.CompletedTask;
            }
        }

        private static TallyService Tally(FakeVoicemeeter? voicemeeter = null)
        {
            var hub = Fakes.Hub();
            var config = SharedConfig;
            return new TallyService(
                new ObsService(NullLogger<ObsService>.Instance, hub),
                new VmixIntegration(NullLogger<VmixIntegration>.Instance, hub, config),
                new MairlistIntegration(NullLogger<MairlistIntegration>.Instance, hub, config, new SimpleHttpFactory()),
                new RadioDjIntegration(NullLogger<RadioDjIntegration>.Instance, hub, config, new ForbiddenHttpFactory()),
                voicemeeter ?? new FakeVoicemeeter { Closed = true },
                hub);
        }

        private static TallyTarget Target(string id, string state) => new(id, "x", id, state);

        // ---- Botao ----

        [Fact]
        public async Task SoMostrar_NoSinalManual_Alterna()
        {
            var tally = Tally();
            var provider = new TallyProvider(tally);
            await provider.ExecuteAsync(Ctx.Of("tally", ("operation", "show"), ("target", TallyService.ManualId)));
            Assert.True(tally.Manual);
            await provider.ExecuteAsync(Ctx.Of("tally", ("operation", "show"), ("target", TallyService.ManualId)));
            Assert.False(tally.Manual);
        }

        [Fact]
        public async Task SoMostrar_EmOutroAlvo_NaoMudaNada()
        {
            var tally = Tally();
            await new TallyProvider(tally).ExecuteAsync(Ctx.Of("tally", ("operation", "show"), ("target", "obs/stream")));
            Assert.False(tally.Manual);
        }

        // ---- Mudancas ----

        [Fact]
        public void Mudancas_AlvoNovoNaoConta_MudancaConta()
        {
            var tally = Tally();
            Assert.Empty(tally.DetectChanges(new[] { Target("a", TallyTarget.Idle) }));
            Assert.Empty(tally.DetectChanges(new[] { Target("a", TallyTarget.Idle) }));
            var change = Assert.Single(tally.DetectChanges(new[] { Target("a", TallyTarget.Program) }));
            Assert.Equal(TallyTarget.Idle, change.previous);
            Assert.Equal(TallyTarget.Program, change.target.State);
        }

        [Fact]
        public void Mudancas_AlvoQueSome_NaoConta_EVoltaComparandoComOUltimo()
        {
            var tally = Tally();
            tally.DetectChanges(new[] { Target("a", TallyTarget.Program) });
            // Programa fechou: nao se sabe se saiu do ar.
            Assert.Empty(tally.DetectChanges(Array.Empty<TallyTarget>()));
            // Voltou igual: nada aconteceu.
            Assert.Empty(tally.DetectChanges(new[] { Target("a", TallyTarget.Program) }));
            tally.DetectChanges(Array.Empty<TallyTarget>());
            // Voltou diferente: agora se sabe que saiu.
            var change = Assert.Single(tally.DetectChanges(new[] { Target("a", TallyTarget.Idle) }));
            Assert.Equal(TallyTarget.Program, change.previous);
        }

        // ---- Condicoes ----

        private static Conditions ConditionsWith(TallyService tally)
        {
            var services = new ServiceCollection();
            services.AddSingleton(tally);
            return new Conditions(services.BuildServiceProvider());
        }

        [Fact]
        public void Condicao_NoAr_SegueOEstado()
        {
            var tally = Tally();
            var conditions = ConditionsWith(tally);
            var on = new StepCondition { Check = "tally_program", Value = TallyService.ManualId };
            Assert.False(conditions.Evaluate(on));
            tally.SetManual(true);
            Assert.True(conditions.Evaluate(on));
            Assert.False(conditions.Evaluate(new StepCondition { Check = "tally_preview", Value = TallyService.ManualId }));
        }

        [Fact]
        public void Condicao_AlvoIndisponivel_NaoEstaNoAr()
        {
            var conditions = ConditionsWith(Tally());
            Assert.False(conditions.Evaluate(new StepCondition { Check = "tally_program", Value = "obs/scene:Cam 1" }));
            Assert.False(conditions.Evaluate(new StepCondition { Check = "tally_preview", Value = "obs/scene:Cam 1" }));
        }

        [Fact]
        public void Condicao_Voicemeeter_LeNaHora_MesmoSemCelular()
        {
            var api = new FakeVoicemeeter { Strips = 2 };
            var conditions = ConditionsWith(Tally(api));
            var open = new StepCondition { Check = "tally_program", Value = "voicemeeter/strip:1" };
            Assert.True(conditions.Evaluate(open));
            api.Values["Strip[0].Mute"] = 1;
            Assert.False(conditions.Evaluate(open));
        }

        // ---- Automacoes ----

        private static (TriggerService service, Recorder rec, TallyService tally) Automation(params TriggerService.Trigger[] triggers)
        {
            var config = SharedConfig;
            var profileId = "tally-" + Guid.NewGuid().ToString("N");
            config.SaveProfile(new DeckProfile
            {
                Id = profileId,
                Name = "Tally",
                Buttons =
                {
                    new DeckButton { Id = "on", Action = new DeckAction { Type = "rec", Parameters = { ["tag"] = "on" } } },
                    new DeckButton { Id = "off", Action = new DeckAction { Type = "rec", Parameters = { ["tag"] = "off" } } },
                },
            });
            foreach (var t in triggers) t.ProfileId = profileId;
            var rec = new Recorder();
            var tally = Tally();
            var service = new TriggerService(config, new ButtonToggleService(NullLogger<ButtonToggleService>.Instance),
                new ActionExecutorService(NullLogger<ActionExecutorService>.Instance, new IActionProvider[] { rec }),
                ConditionsWith(tally), NullLogger<TriggerService>.Instance, tally);
            service.SaveAll(new List<TriggerService.Trigger>(triggers));
            return (service, rec, tally);
        }

        private static TriggerService.Trigger On(string target = "a") =>
            new() { Event = "tally_on", Target = target, ButtonId = "on" };

        private static TriggerService.Trigger Off(string target = "a") =>
            new() { Event = "tally_off", Target = target, ButtonId = "off" };

        [Theory]
        [InlineData(TallyTarget.Idle, TallyTarget.Program, "on")]
        [InlineData(TallyTarget.Preview, TallyTarget.Program, "on")]
        [InlineData(TallyTarget.Program, TallyTarget.Idle, "off")]
        [InlineData(TallyTarget.Program, TallyTarget.Preview, "off")]
        public async Task Automacao_EntrarESairDoAr(string previous, string current, string expected)
        {
            var (service, rec, _) = Automation(On(), Off());
            await service.OnTallyChangedAsync("a", previous, current);
            Assert.Equal(new[] { expected }, rec.Ran);
        }

        [Fact]
        public async Task Automacao_PreviewParaOcioso_NaoDispara()
        {
            var (service, rec, _) = Automation(On(), Off());
            await service.OnTallyChangedAsync("a", TallyTarget.Preview, TallyTarget.Idle);
            await service.OnTallyChangedAsync("a", TallyTarget.Idle, TallyTarget.Preview);
            Assert.Empty(rec.Ran);
        }

        [Fact]
        public async Task Automacao_SoDoAlvoDela()
        {
            var (service, rec, _) = Automation(On("a"));
            await service.OnTallyChangedAsync("b", TallyTarget.Idle, TallyTarget.Program);
            Assert.Empty(rec.Ran);
        }

        [Fact]
        public async Task Automacao_Desligada_OuComCondicaoFalsa_NaoDispara()
        {
            var off = On();
            off.Enabled = false;
            var blocked = On("b");
            blocked.If = new StepCondition { Check = "tally_program", Value = TallyService.ManualId };
            var (service, rec, tally) = Automation(off, blocked);
            await service.OnTallyChangedAsync("a", TallyTarget.Idle, TallyTarget.Program);
            await service.OnTallyChangedAsync("b", TallyTarget.Idle, TallyTarget.Program);
            Assert.Empty(rec.Ran);
            tally.SetManual(true);
            await service.OnTallyChangedAsync("b", TallyTarget.Idle, TallyTarget.Program);
            Assert.Single(rec.Ran);
        }

        [Fact]
        public void Automacao_SoPedeAcompanhamentoSeTiverGatilhoDeTallyLigado()
        {
            var (service, _, tally) = Automation();
            Assert.False(tally.WatchRequested!());
            var trigger = On();
            service.SaveAll(new List<TriggerService.Trigger> { trigger });
            Assert.True(tally.WatchRequested!());
            trigger.Enabled = false;
            Assert.False(tally.WatchRequested!());
        }

        /// <summary>O caminho inteiro, sem celular conectado: o laco do tally ve a mudanca e aperta o botao.</summary>
        [Fact]
        public async Task PontaAPonta_SinalManual_ApertaOBotao_SemCelular()
        {
            var (_, rec, tally) = Automation(On(TallyService.ManualId), Off(TallyService.ManualId));
            await tally.StartAsync(CancellationToken.None);
            try
            {
                // Da tempo da primeira leitura, que so serve de base.
                await Task.Delay(400);
                tally.SetManual(true);
                await Fakes.Until(() => rec.Ran.Contains("on"));
                tally.SetManual(false);
                await Fakes.Until(() => rec.Ran.Contains("off"));
            }
            finally
            {
                await tally.StopAsync(CancellationToken.None);
            }
        }
    }

    // ---- Companion ----

    public class SatelliteCodecTests
    {
        // Casos do satellite-protocol-corpus.json do Companion.
        [Fact]
        public void Parse_Corpus()
        {
            Assert.Equal("value", SatelliteCodec.Parse("KEY=value")["KEY"]);
            Assert.Equal("", SatelliteCodec.Parse("KEY=")["KEY"]);
            Assert.Null(SatelliteCodec.Parse("FLAG")["FLAG"]);
            Assert.Equal("a=b=c", SatelliteCodec.Parse("X=a=b=c")["X"]);
            var mixed = SatelliteCodec.Parse("A=1 FLAG B=2");
            Assert.Equal("1", mixed["A"]);
            Assert.True(mixed.ContainsKey("FLAG"));
            Assert.Equal("2", mixed["B"]);
            Assert.Equal("com espaco", SatelliteCodec.Parse("T=\"com espaco\"")["T"]);
            Assert.Equal("a\"b\\c", SatelliteCodec.Parse("T=\"a\\\"b\\\\c\"")["T"]);
            Assert.Equal("data:image/png;base64,AA==", SatelliteCodec.Parse("BITMAP=\"data:image/png;base64,AA==\"")["BITMAP"]);
            Assert.Empty(SatelliteCodec.Parse("   "));
        }

        [Fact]
        public void Format_IgualAoPacote()
        {
            Assert.Equal("KEY-PRESS DEVICEID=\"x\" KEY=3 PRESSED=1 \n",
                SatelliteCodec.Format("KEY-PRESS", "x", ("KEY", 3), ("PRESSED", true)));
            Assert.Equal("ADD-DEVICE PRODUCT_NAME=\"a \\\"b\\\"\" \n",
                SatelliteCodec.Format("ADD-DEVICE", null, ("PRODUCT_NAME", "a \"b\"")));
            Assert.Throws<ArgumentException>(() => SatelliteCodec.Format("X", null, ("A", "linha\nnova")));
            Assert.Throws<ArgumentException>(() => SatelliteCodec.Format("X", null, ("A B", "v")));
        }

        [Fact]
        public void IdaEVolta()
        {
            var line = SatelliteCodec.Format("KEY-STATE", "dev 1", ("TEXT", "a \\ \" b"), ("KEY", 4));
            var (command, body) = SatelliteCodec.Split(line.TrimEnd('\n'));
            Assert.Equal("KEY-STATE", command);
            var p = SatelliteCodec.Parse(body);
            Assert.Equal("dev 1", p["DEVICEID"]);
            Assert.Equal("a \\ \" b", p["TEXT"]);
            Assert.Equal("4", p["KEY"]);
        }
    }

    public class CompanionTests
    {
        [Fact]
        public void Bitmap_DataUrl_SoOsDados() =>
            Assert.Equal("QUJD", CompanionIntegration.ImageFrom("data:image/png;base64,QUJD"));

        [Fact]
        public void Bitmap_RgbCru_ViraPng()
        {
            // 2x2 vermelho.
            var rgb = Enumerable.Range(0, 4).SelectMany(_ => new byte[] { 255, 0, 0 }).ToArray();
            var png = Convert.FromBase64String(CompanionIntegration.RgbToPng(rgb)!);
            using var image = new System.Drawing.Bitmap(new MemoryStream(png));
            Assert.Equal(2, image.Width);
            var pixel = image.GetPixel(1, 1);
            Assert.Equal((255, 0, 0), (pixel.R, pixel.G, pixel.B));
        }

        [Fact]
        public void Bitmap_TamanhoQueNaoFechaUmQuadrado_Ignora() =>
            Assert.Null(CompanionIntegration.RgbToPng(new byte[10]));

        [Fact]
        public void AddDevice_NegociaPng_SoSeOCompanionOferecer()
        {
            var with = CompanionIntegration.BuildAddDevice("d", 8, 4, png: true);
            Assert.Contains("KEYS_TOTAL=32", with);
            Assert.Contains("KEYS_PER_ROW=8", with);
            Assert.Contains("BITMAP_FORMAT=\"png\"", with);
            Assert.DoesNotContain("BITMAP_FORMAT", CompanionIntegration.BuildAddDevice("d", 8, 4, png: false));
        }

        private static async Task<CompanionIntegration> StartAsync()
        {
            var companion = new CompanionIntegration(NullLogger<CompanionIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await companion.StartAsync(CancellationToken.None);
            return companion;
        }

        [Fact]
        public async Task PontaAPonta_Negociacao_Teclas_Toque_EQueda()
        {
            await using var server = new FakeLineServer(async (line, s) =>
            {
                if (line.StartsWith("ADD-DEVICE"))
                {
                    var id = SatelliteCodec.Parse(SatelliteCodec.Split(line).body)["DEVICEID"];
                    await s.SendAsync($"ADD-DEVICE OK DEVICEID=\"{id}\" \n");
                    var text = Convert.ToBase64String(Encoding.UTF8.GetBytes("CAM 1"));
                    await s.SendAsync($"KEY-STATE DEVICEID=\"{id}\" KEY=5 TYPE=\"BUTTON\" PRESSED=0 BITMAP=\"data:image/png;base64,QUJD\" COLOR=\"#ff0000\" TEXTCOLOR=\"#ffffff\" TEXT=\"{text}\" \n");
                    await s.SendAsync("PING abc \n");
                }
            }, async s =>
            {
                await s.SendAsync("BEGIN CompanionVersion=\"5.1.1\" ApiVersion=\"1.16.0\" \n");
                await s.SendAsync("CAPS SUBSCRIPTIONS=1 BITMAP_FORMATS=\"rgb,png,webp\" \n");
            });

            var companion = await StartAsync();
            try
            {
                var status = await companion.ConfigureAsync(new()
                {
                    ["address"] = "127.0.0.1:" + server.Port, ["columns"] = "4", ["rows"] = "2",
                });
                await Fakes.Until(() => companion.IsConnected);
                Assert.Equal("Companion 5.1.1", companion.AccountName);
                Assert.True(status.Configured);

                var add = server.Received.Single(l => l.StartsWith("ADD-DEVICE"));
                Assert.Contains("KEYS_TOTAL=8", add);
                Assert.Contains("BITMAP_FORMAT=\"png\"", add);

                string Snapshot() => JsonSerializer.Serialize(companion.Snapshot());
                await Fakes.Until(() => Snapshot().Contains("CAM 1"));
                Assert.Contains("QUJD", Snapshot());
                Assert.Contains("#ff0000", Snapshot());
                await Fakes.Until(() => server.Received.Any(l => l.StartsWith("PONG abc")));

                await companion.SetKeyAsync("celular", 5, true);
                await Fakes.Until(() => server.Received.Any(l => l.StartsWith("KEY-PRESS") && l.Contains("KEY=5") && l.Contains("PRESSED=1")));

                // Tecla fora da grade nao vai para o Companion.
                await companion.SetKeyAsync("celular", 99, true);
                Assert.DoesNotContain(server.Received, l => l.Contains("KEY=99"));

                // O celular caiu com o dedo em cima: a tecla e solta.
                companion.ReleaseOwner("celular");
                await Fakes.Until(() => server.Received.Any(l => l.StartsWith("KEY-PRESS") && l.Contains("KEY=5") && l.Contains("PRESSED=0")));

                // Conexao caiu: a superficie apaga.
                server.DropClient();
                await Fakes.Until(() => !Snapshot().Contains("CAM 1"));
            }
            finally
            {
                await companion.LogoutAsync();
                await companion.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task CompanionAntigo_SemCaps_AindaAdicionaOAparelho()
        {
            await using var server = new FakeLineServer(async (line, s) =>
            {
                if (line.StartsWith("ADD-DEVICE"))
                    await s.SendAsync("ADD-DEVICE OK DEVICEID=\"x\" \n");
            }, s => s.SendAsync("BEGIN CompanionVersion=\"3.0.0\" ApiVersion=\"1.5.0\" \n"));
            var companion = await StartAsync();
            try
            {
                await companion.ConfigureAsync(new() { ["address"] = "127.0.0.1:" + server.Port });
                await Fakes.Until(() => companion.IsConnected);
                Assert.DoesNotContain("BITMAP_FORMAT", server.Received.Single(l => l.StartsWith("ADD-DEVICE")));
            }
            finally
            {
                await companion.LogoutAsync();
                await companion.StopAsync(CancellationToken.None);
            }
        }

        [Fact]
        public async Task Recusado_MostraOMotivo()
        {
            await using var server = new FakeLineServer(async (line, s) =>
            {
                if (line.StartsWith("ADD-DEVICE"))
                    await s.SendAsync("ADD-DEVICE ERROR DEVICEID=\"x\" MESSAGE=\"Device exists elsewhere\" \n");
            }, async s =>
            {
                await s.SendAsync("BEGIN CompanionVersion=\"5\" \n");
                await s.SendAsync("CAPS BITMAP_FORMATS=\"rgb\" \n");
            });
            var companion = await StartAsync();
            try
            {
                var status = await companion.ConfigureAsync(new() { ["address"] = "127.0.0.1:" + server.Port });
                await Fakes.Until(() => companion.GetStatus().Message != null);
                Assert.False(companion.IsConnected);
                Assert.Equal("Device exists elsewhere", companion.GetStatus().Message);
            }
            finally
            {
                await companion.LogoutAsync();
                await companion.StopAsync(CancellationToken.None);
            }
        }

        [Theory]
        [InlineData("0", "4")]
        [InlineData("99", "4")]
        [InlineData("x", "4")]
        public async Task GradeInvalida_Lanca(string columns, string rows)
        {
            var companion = new CompanionIntegration(NullLogger<CompanionIntegration>.Instance, Fakes.Hub(), Fakes.Config());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                companion.ConfigureAsync(new() { ["address"] = "127.0.0.1", ["columns"] = columns, ["rows"] = rows }));
        }
    }

    // ---- Schema e traducoes ----

    public class StudioSchemaTests : IClassFixture<WebApplicationFactory<DroidDeck.Program>>
    {
        private static readonly string[] Types = { "network", "tally", "vmix", "companion", "mairlist", "radiodj" };

        private readonly WebApplicationFactory<DroidDeck.Program> _factory;

        public StudioSchemaTests(WebApplicationFactory<DroidDeck.Program> factory) => _factory = factory;

        private async Task<List<ActionSchema>> Schemas()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", ApiKeyProvider.GetKey());
            client.DefaultRequestHeaders.Add("Accept-Language", "pt-BR");
            var all = (await client.GetFromJsonAsync<List<ActionSchema>>("/api/actions/schema"))!;
            return all.Where(s => Types.Contains(s.Type) || s.Type == "voicemeeter").ToList();
        }

        [Fact]
        public async Task TodosRegistrados_SoARedeEhPrivilegiada()
        {
            var schemas = await Schemas();
            Assert.All(Types, t => Assert.Contains(schemas, s => s.Type == t));
            Assert.All(schemas, s => Assert.Equal(s.Type == "network", s.Privileged));
            Assert.All(schemas.Where(s => s.Settings != null), s => Assert.NotNull(s.StateEvent));
        }

        [Fact]
        public async Task TodoTextoTemInglesEEspanhol()
        {
            var en = L.StringsOf("en");
            var es = L.StringsOf("es");
            var texts = new List<string>();
            foreach (var s in await Schemas())
            {
                texts.Add(s.Title);
                texts.Add(s.Category);
                if (s.Description != null) texts.Add(s.Description);
                foreach (var f in s.Fields)
                {
                    texts.Add(f.Label);
                    if (f.Help != null) texts.Add(f.Help);
                    texts.AddRange((f.Options ?? new()).Select(o => o.Label));
                }
            }
            // Nomes proprios ficam iguais em todo idioma.
            var missing = texts.Distinct()
                .Where(t => t is not ("vMix" or "mAirList" or "RadioDJ" or "Companion" or "Voicemeeter" or "OSC" or "UDP" or "TCP"))
                .Where(t => !en.ContainsKey(t) || !es.ContainsKey(t))
                .ToList();
            Assert.True(missing.Count == 0, "Sem tradução: " + string.Join(" | ", missing));
        }

        [Fact]
        public async Task Tally_ListaEEstado_ComOSinalManual()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", ApiKeyProvider.GetKey());
            var targets = (await client.GetFromJsonAsync<List<FieldOption>>("/api/tally/targets"))!;
            Assert.Contains(targets, t => t.Value == TallyService.ManualId);

            var keys = await client.GetStringAsync("/api/companion/keys");
            Assert.Contains("\"connected\":false", keys);
        }

        [Fact]
        public async Task Tally_SemChave_Recusa()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/tally/targets");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
