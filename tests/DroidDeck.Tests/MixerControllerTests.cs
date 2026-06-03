using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DroidDeck.Controllers;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc;

namespace DroidDeck.Tests
{
    public class MixerControllerTests
    {
        [Fact]
        public void GetAllOutputs_ReturnsOk()
        {
                var mockEnum = new Mock<DroidDeck.Audio.IAudioDeviceEnumerator>();
                var mockLoggerSvc = new Mock<ILogger<MixerService>>();
                mockEnum.Setup(e => e.EnumerateAudioEndPoints(It.IsAny<NAudio.CoreAudioApi.DataFlow>(), It.IsAny<NAudio.CoreAudioApi.DeviceState>()))
                    .Returns(new List<DroidDeck.Audio.IAudioDevice>());

                var service = new MixerService(mockLoggerSvc.Object, mockEnum.Object);
                var mockLogger = new Mock<ILogger<MixerController>>();

                var controller = new MixerController(service, mockLogger.Object);
            var result = controller.GetAllOutputs() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }
    }
}
