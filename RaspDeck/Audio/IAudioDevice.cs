using NAudio.CoreAudioApi;
using System.Collections.Generic;

namespace DroidDeck.Audio
{
    public interface IAudioSession
    {
        bool IsSystemSoundsSession { get; }
        uint GetProcessID();
        float Volume { get; }
        bool Mute { get; set; }
    }

    public interface IAudioDevice
    {
        string Id { get; }
        string FriendlyName { get; }
        string DeviceFriendlyName { get; }
        DataFlow DataFlow { get; }
        float MasterVolumeLevelScalar { get; }
        bool Mute { get; }
        IEnumerable<IAudioSession> Sessions { get; }
    }
}
