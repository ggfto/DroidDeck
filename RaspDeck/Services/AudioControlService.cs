using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace AnyDeck.Services
{
    public class AudioControlService : IAudioControlService
    {
        private readonly AnyDeck.Audio.IAudioDeviceEnumerator _enumerator;

        public AudioControlService(AnyDeck.Audio.IAudioDeviceEnumerator enumerator)
        {
            _enumerator = enumerator;
        }

        public int MuteByProcessName(string processName, bool mute)
        {
            if (string.IsNullOrEmpty(processName)) return 0;
            int affected = 0;

            // try to find sessions by enumerating audio endpoints and their sessions
            foreach (var device in _enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
            {
                try
                {
                    foreach (var s in device.Sessions)
                    {
                        try
                        {
                            var pid = (int)s.GetProcessID();
                            var p = System.Diagnostics.Process.GetProcessById(pid);
                            if (string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                            {
                                s.Mute = mute;
                                affected++;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return affected;
        }

        public bool ToggleMuteByProcessName(string processName)
        {
            var current = GetMuteStateByProcessName(processName);
            var newState = !(current ?? false);
            MuteByProcessName(processName, newState);
            return newState;
        }

        public bool? GetMuteStateByProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return null;
            foreach (var device in _enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
            {
                try
                {
                    foreach (var s in device.Sessions)
                    {
                        try
                        {
                            var pid = (int)s.GetProcessID();
                            var p = System.Diagnostics.Process.GetProcessById(pid);
                            if (string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                            {
                                return s.Mute;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
