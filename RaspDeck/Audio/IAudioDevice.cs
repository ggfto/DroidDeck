using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;

namespace DroidDeck.Audio
{
    // Ambas as abstrações são IDisposable porque a implementação real (NAudio) é COM: o
    // device e cada sessão são proxies que precisam ser liberados de forma determinística.
    // Quem obtém um IAudioDevice/IAudioSession é dono dele e deve descartá-lo.
    public interface IAudioSession : IDisposable
    {
        bool IsSystemSoundsSession { get; }
        uint GetProcessID();
        float Volume { get; }
        bool Mute { get; set; }
    }

    public interface IAudioDevice : IDisposable
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
