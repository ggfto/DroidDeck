using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DroidDeck.Services;
using DroidDeck.Audio;
using DroidDeck.Mixer;

namespace DroidDeck.Tests
{
    public class MixerServiceTests
    {
        [Fact]
        public void FindAllOutputs_ReturnsList()
        {
            var mockEnum = new Mock<IAudioDeviceEnumerator>();
            var mockLogger = new Mock<ILogger<MixerService>>();

            // Arrange: return a single fake device
            var fakeDevice = new Mock<IAudioDevice>();
            fakeDevice.SetupGet(d => d.Id).Returns("device-1");
            fakeDevice.SetupGet(d => d.FriendlyName).Returns("Device 1 (Test)");
            fakeDevice.SetupGet(d => d.DeviceFriendlyName).Returns("DeviceFriendly");
            fakeDevice.SetupGet(d => d.DataFlow).Returns(NAudio.CoreAudioApi.DataFlow.Render);
            fakeDevice.SetupGet(d => d.MasterVolumeLevelScalar).Returns(1.0f);
            fakeDevice.SetupGet(d => d.Mute).Returns(false);
            fakeDevice.Setup(d => d.Sessions).Returns(new List<IAudioSession>());

            mockEnum.Setup(e => e.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                    .Returns(new List<IAudioDevice> { fakeDevice.Object });

            var svc = new MixerService(mockLogger.Object, mockEnum.Object);

            // Act
            var list = svc.FindAllOutputs();

            // Assert
            Assert.NotNull(list);
            Assert.Single(list);
            Assert.Equal("device-1", list[0].Id);
        }
    }
}
