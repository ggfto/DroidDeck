using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using DroidDeck.Controllers;
using DroidDeck.Software;
using DroidDeck.Services;

namespace DroidDeck.Tests
{
    public class SoftwareAudioControllerTests
    {
        [Fact]
        public void Mute_ReturnsBadRequest_WhenNoProcess()
        {
            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                { "EnableSoftwareActivation", "true" }
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            var mockActivator = new Mock<IAppActivator>();
            var mockAudio = new Mock<IAudioControlService>();
            var controller = new SoftwareController(cfg, mockActivator.Object, mockAudio.Object);

            var result = controller.Mute(null);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public void ToggleMute_ReturnsForbid_WhenNotAllowed()
        {
            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                { "EnableSoftwareActivation", "true" },
                { "AllowedTargets", "OtherApp" }
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            var mockActivator = new Mock<IAppActivator>();
            var mockAudio = new Mock<IAudioControlService>();
            var controller = new SoftwareController(cfg, mockActivator.Object, mockAudio.Object);

            var payload = new DroidDeck.Software.AudioTarget { ProcessName = "Discord" };
            var result = controller.ToggleMute(payload);

            Assert.IsType<ForbidResult>(result);
        }
    }
}
