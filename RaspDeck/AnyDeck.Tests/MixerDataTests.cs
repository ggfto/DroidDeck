using AnyDeck;
using Xunit;

namespace AnyDeck.Tests
{
    public class MixerDataTests
    {
        [Fact]
        public void Constructor_Initialization_ShouldSetProperties()
        {
            var data = new MixerData(50, true, 123);
            Assert.Equal(50, data.Volume);
            Assert.True(data.Mute);
            Assert.Equal(123, data.Session);
        }

        [Fact]
        public void DefaultConstructor_ShouldSetDefaults()
        {
            var data = new MixerData();
            Assert.Null(data.Volume);
            Assert.Null(data.Mute);
            Assert.Equal(-1, data.Session);
        }
    }
}
