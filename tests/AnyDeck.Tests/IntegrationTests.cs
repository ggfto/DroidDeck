using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using AnyDeck.Audio;

namespace AnyDeck.Tests
{
    public class IntegrationTests : IClassFixture<WebApplicationFactory<AnyDeck.Program>>
    {
        private readonly WebApplicationFactory<AnyDeck.Program> _factory;

        public IntegrationTests(WebApplicationFactory<AnyDeck.Program> factory)
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

            client.DefaultRequestHeaders.Add("X-API-KEY", "changeme");
            var response = await client.GetAsync("/api/v1/mixer/out");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
