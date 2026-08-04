using DroidDeck.Audio;
using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;

namespace DroidDeck.Audio
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

        public void Dispose() => _session.Dispose();
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
                    // sessions[i] cria um AudioSessionControl novo a cada acesso; ler uma vez
                    // e entregar a posse ao chamador, que descarta via using.
                    yield return new NAudioSessionAdapter(sessions[i]);
                }
            }
        }

        public void Dispose() => _device.Dispose();
    }

    public class NAudioDeviceEnumerator : IAudioDeviceEnumerator
    {
        public IEnumerable<IAudioDevice> EnumerateAudioEndPoints(DataFlow dataFlow, DeviceState deviceState)
        {
            // O enumerador em si é IDisposable e não é mais preciso após materializar a
            // lista (os MMDevice ficam nos adapters). Descartá-lo evita acumular objetos COM.
            // Os devices retornados são do chamador — ele é quem os descarta.
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(dataFlow, deviceState).Select(d => new NAudioDeviceAdapter(d)).ToList();
        }

        public IAudioDevice? GetDevice(string id)
        {
            try
            {
                using var devices = new MMDeviceEnumerator();
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
