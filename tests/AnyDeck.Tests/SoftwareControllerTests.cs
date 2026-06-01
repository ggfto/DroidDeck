using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using AnyDeck.Controllers;
using AnyDeck.Software;
using AnyDeck.Services;

namespace AnyDeck.Tests
{
    public class SoftwareControllerTests
    {
        [Fact]
        public void Activate_ReturnsForbid_WhenFeatureDisabled()
        {
            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                { "EnableSoftwareActivation", "false" }
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            var mockActivator = new Mock<IAppActivator>();
            var mockAudio = new Mock<AnyDeck.Services.IAudioControlService>();
            var controller = new SoftwareController(cfg, mockActivator.Object, mockAudio.Object);

            var result = controller.Activate(new SoftwareData { Name = "SomeApp", Action = "" });

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public void Activate_ReturnsBadRequest_WhenModelInvalid()
        {
            var inMemory = new System.Collections.Generic.Dictionary<string, string?>
            {
                { "EnableSoftwareActivation", "true" }
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            var mockActivator = new Mock<IAppActivator>();
            var mockAudio = new Mock<AnyDeck.Services.IAudioControlService>();
            var controller = new SoftwareController(cfg, mockActivator.Object, mockAudio.Object);
            controller.ModelState.AddModelError("Name", "Required");

            var result = controller.Activate(new SoftwareData());

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
