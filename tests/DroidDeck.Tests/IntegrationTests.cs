using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using DroidDeck.Audio;
using DroidDeck.Auth;

namespace DroidDeck.Tests
{
    public class IntegrationTests : IClassFixture<WebApplicationFactory<DroidDeck.Program>>
    {
        private readonly WebApplicationFactory<DroidDeck.Program> _factory;

        public IntegrationTests(WebApplicationFactory<DroidDeck.Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetAllOutputs_ReturnsOk_WithMockedEnumerator()
        {
            var mockEnum = new Mock<IAudioDeviceEnumerator>();
            mockEnum.Setup(e => e.EnumerateAudioEndPoints(It.IsAny<NAudio.CoreAudioApi.DataFlow>(), It.IsAny<NAudio.CoreAudioApi.DeviceState>()))
                    .Returns(new System.Collections.Generic.List<IAudioDevice>());

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // replace the real enumerator with our mock
                    services.AddSingleton<IAudioDeviceEnumerator>(mockEnum.Object);
                });
            }).CreateClient();

            // A chave fixa "changeme" saiu quando a auth passou a gerar uma chave forte por
            // maquina (ApiKeyProvider). Pegar dela mantem o teste alinhado com o servidor.
            client.DefaultRequestHeaders.Add("X-API-KEY", ApiKeyProvider.GetKey());
            var response = await client.GetAsync("/api/v1/mixer/out");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
