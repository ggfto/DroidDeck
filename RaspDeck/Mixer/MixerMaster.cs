using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;

namespace AnyDeck
{
    public class MixerMaster
    {
        private Dictionary<int, MixerChannel>? channels;

        public MixerMaster()
        {
            channels = new Dictionary<int, MixerChannel>();
        }
        public MixerMaster(MMDevice device)
        {
            newMaster(device);
        }

        public MixerMaster(AnyDeck.Audio.IAudioDevice device)
        {
            // construct from abstraction
            Id = device.Id;
            Title = device.FriendlyName.Contains("(") ? device.FriendlyName.Substring(0, device.FriendlyName.IndexOf("(")).Trim() : device.FriendlyName;
            Description = device.DeviceFriendlyName;
            Volume = (int)(device.MasterVolumeLevelScalar * 100);
            Mute = device.Mute;
            Icon = null;
            if (device.DataFlow == DataFlow.Render)
            {
                channels = new Dictionary<int, MixerChannel>();
                foreach (var session in device.Sessions)
                {
                    if (session.IsSystemSoundsSession) continue;
                    var channel = new MixerChannel();
                    // best-effort: set basic properties
                    // Note: IAudioSession abstraction doesn't expose process info for icon extraction
                    channel.Id = (int)session.GetProcessID();
                    channel.Mute = session.Mute;
                    channel.Volume = (int)(session.Volume * device.MasterVolumeLevelScalar * 100);
                    channels[channel.Id] = channel;
                }
            }
        }
        public MixerMaster(string idString)
        {
            var devices = new MMDeviceEnumerator();
            MMDevice? device = null;
            try
            {
                device = devices.GetDevice(idString);
            }
            catch
            {
                device = null;
            }

            if (device == null) return;
            if (device.State == DeviceState.Unplugged ||
                device.State == DeviceState.NotPresent ||
                device.State == DeviceState.Disabled) return;

            newMaster(device);
        }

        private void newMaster(MMDevice device)
        {
            Id = device.ID;
            var friendly = device.FriendlyName ?? string.Empty;
            var idx = friendly.IndexOf("(");
            Title = idx >= 0 ? friendly.Substring(0, idx).Trim() : friendly.Trim();
            Description = device.DeviceFriendlyName;
            Volume = (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            Mute = device.AudioEndpointVolume.Mute;
            Icon = null;
            if (device.DataFlow == DataFlow.Render)
            {
                var sessions = device.AudioSessionManager.Sessions;
                channels = new Dictionary<int, MixerChannel>();
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i].IsSystemSoundsSession) continue;
                    var channel = new MixerChannel(sessions[i], device.AudioEndpointVolume.MasterVolumeLevelScalar);
                    channels[(int)sessions[i].GetProcessID] = channel;
                }
            }
        }
        public static List<MixerMaster> GetAllMixers(DataFlow dataFlow, DeviceState deviceState)
        {
            var devices = new List<MixerMaster>();
            foreach (MMDevice d in new MMDeviceEnumerator().EnumerateAudioEndPoints(dataFlow, deviceState))
            {
                var master = new MixerMaster(d);
                devices.Add(master);
            }
            return devices;
        }

        public static List<MixerMaster> GetAllMixers(AnyDeck.Audio.IAudioDeviceEnumerator enumerator, DataFlow dataFlow, DeviceState deviceState)
        {
            var devices = new List<MixerMaster>();
            foreach (var d in enumerator.EnumerateAudioEndPoints(dataFlow, deviceState))
            {
                devices.Add(new MixerMaster(d));
            }
            return devices;
        }
        public MixerChannel? GetChannel(int id)
        {
            if (channels == null) return null;
            channels.TryGetValue(id, out var ch);
            return ch;
        }

        public MixerMaster? SetOptions(string id, MixerData data)
        {
            var devices = new MMDeviceEnumerator();
            var device = devices.GetDevice(id);
            if (device == null) return null;
            else
            {
                float volume;
                volume = (data.Volume ?? -1.0f) / 100.0f;
                bool mute = data.Mute == true;
                if (data.Session >= 0 && device.DataFlow.CompareTo(DataFlow.Render) == 0)
                {
                    var ch = GetChannel(data.Session);
                    if (ch != null)
                        ch.SetOptions(data, device.AudioEndpointVolume.MasterVolumeLevelScalar);
                }
                else
                    if (mute && device.AudioEndpointVolume.MasterVolumeLevelScalar >= volume)
                    device.AudioEndpointVolume.Mute = mute;
                else
                    if (volume > 0)
                    {
                        device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
                        device.AudioEndpointVolume.Mute = false;
                    }
                    else
                        device.AudioEndpointVolume.Mute = mute;
            }
            return new MixerMaster(device);
        }

        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int Volume { get; set; }
        public string? Icon { get; set; }
        public bool Mute { get; set; }
        public List<MixerChannel>? Channels => channels?.Values.ToList();
    }
}
