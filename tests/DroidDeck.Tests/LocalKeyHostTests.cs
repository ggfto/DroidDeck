using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DroidDeck.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace DroidDeck.Tests
{
    /// <summary>
    /// DNS rebinding: um site aponta o proprio nome para 127.0.0.1. O navegador passa a
    /// tratar o pedido como same-origin (sem Origin num GET) e a conexao e loopback; so o
    /// cabecalho Host ainda mostra o nome do site.
    /// </summary>
    public class LocalKeyHostTests : IClassFixture<WebApplicationFactory<DroidDeck.Program>>
    {
        private const string Route = "/api/pairing/local-key";
        private readonly WebApplicationFactory<DroidDeck.Program> _factory;

        public LocalKeyHostTests(WebApplicationFactory<DroidDeck.Program> factory) => _factory = factory;

        [Theory]
        [InlineData("localhost")]
        [InlineData("localhost:4787")]
        [InlineData("LocalHost:4787")]
        [InlineData("127.0.0.1")]
        [InlineData("127.0.0.1:4787")]
        [InlineData("[::1]:4787")]
        public async Task EnderecoDoProprioPc_RecebeAChave(string host)
        {
            var response = await Ask(host);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(ApiKeyProvider.GetKey(), await response.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData("evil.example")]
        [InlineData("evil.example:4787")]
        [InlineData("localhost.evil.example:4787")]
        [InlineData("127.0.0.1.evil.example:4787")]
        [InlineData("192.168.0.10:4787")]
        public async Task OutroNomeNoHost_NaoRecebeAChave(string host)
        {
            var response = await Ask(host);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain(ApiKeyProvider.GetKey(), await response.Content.ReadAsStringAsync());
        }

        private async Task<HttpResponseMessage> Ask(string host)
        {
            using var factory = _factory.WithWebHostBuilder(b =>
                b.ConfigureServices(s => s.AddSingleton<IStartupFilter, FromThisPc>()));
            var request = new HttpRequestMessage(HttpMethod.Get, Route);
            request.Headers.Host = host;
            // O que o navegador manda depois do rebinding.
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            return await factory.CreateClient().SendAsync(request);
        }

        /// <summary>O TestServer nao tem IP remoto: aqui ele passa a ser o proprio PC.</summary>
        private sealed class FromThisPc : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.Use((context, following) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Loopback;
                    return following();
                });
                next(app);
            };
        }
    }
}
