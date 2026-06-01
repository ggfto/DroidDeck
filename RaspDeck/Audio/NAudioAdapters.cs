using AnyDeck.Audio;
using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;

namespace AnyDeck.Audio
{
    internal class NAudioSessionAdapter : IAudioSession
    {
        private readonly AudioSessionControl _session;

        public NAudioSessionAdapter(AudioSessionControl session)
        {
            _session = session;
        }

        public bool IsSystemSoundsSession => _session.IsSystemSoundsSession;

        public uint GetProcessID() => _session.GetProcessID;

        public float Volume => _session.SimpleAudioVolume.Volume;

        public bool Mute
        {
            get => _session.SimpleAudioVolume.Mute;
            set => _session.SimpleAudioVolume.Mute = value;
        }
    }

    internal class NAudioDeviceAdapter : IAudioDevice
    {
        private readonly MMDevice _device;

        public NAudioDeviceAdapter(MMDevice device)
        {
            _device = device;
        }

        public string Id => _device.ID;

        public string FriendlyName => _device.FriendlyName;

        public string DeviceFriendlyName => _device.DeviceFriendlyName;

        public DataFlow DataFlow => _device.DataFlow;

        public float MasterVolumeLevelScalar => _device.AudioEndpointVolume.MasterVolumeLevelScalar;

        public bool Mute => _device.AudioEndpointVolume.Mute;

        public IEnumerable<IAudioSession> Sessions
        {
            get
            {
                var sessions = _device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    yield return new NAudioSessionAdapter(sessions[i]);
                }
            }
        }
    }

    public class NAudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        public IEnumerable<IAudioDevice> EnumerateAudioEndPoints(DataFlow dataFlow, DeviceState deviceState)
        {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(dataFlow, deviceState).Select(d => new NAudioDeviceAdapter(d)).ToList();
        }

        public IAudioDevice? GetDevice(string id)
        {
            try
            {
                var devices = new MMDeviceEnumerator();
                var dev = devices.GetDevice(id);
                if (dev == null) return null;
                return new NAudioDeviceAdapter(dev);
            }
            catch
            {
                return null;
            }
        }
    }
}
