using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;

namespace DroidDeck
{
    /// <summary>
    /// Snapshot (DTO) de um dispositivo de áudio e suas sessões.
    ///
    /// Regra de ouro deste tipo: ele NUNCA guarda referência COM viva. Todo
    /// <see cref="MMDevice"/>/<see cref="AudioSessionControl"/> usado para montar o snapshot é
    /// descartado antes de o objeto ser devolvido. Sem isso, cada poll de
    /// /api/v1/Mixer/{in,out} (a cada 60s) deixava para trás um proxy COM por dispositivo e
    /// vários por sessão; a liberação só acontecia na finalização do GC, que precisa
    /// marshalar de volta para o apartamento de origem — o que empilhava threads bloqueadas
    /// em espera de RPC (LpcReply) até o processo passar de 14 mil threads / 1 GB de RAM.
    /// </summary>
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

        public MixerMaster(DroidDeck.Audio.IAudioDevice device)
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
                    // A sessão é COM por baixo do adapter; descartar após copiar os valores.
                    using (session)
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
        }
        public MixerMaster(string idString)
        {
            using var devices = new MMDeviceEnumerator();
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

            using (device)
            {
                if (device.State == DeviceState.Unplugged ||
                    device.State == DeviceState.NotPresent ||
                    device.State == DeviceState.Disabled) return;

                newMaster(device);
            }
        }

        private void newMaster(MMDevice device)
        {
            Id = device.ID;
            var friendly = device.FriendlyName ?? string.Empty;
            var idx = friendly.IndexOf("(");
            Title = idx >= 0 ? friendly.Substring(0, idx).Trim() : friendly.Trim();
            Description = device.DeviceFriendlyName;
            var masterVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
            Volume = (int)(masterVolume * 100);
            Mute = device.AudioEndpointVolume.Mute;
            Icon = null;
            if (device.DataFlow == DataFlow.Render)
            {
                var sessions = device.AudioSessionManager.Sessions;
                channels = new Dictionary<int, MixerChannel>();
                for (int i = 0; i < sessions.Count; i++)
                {
                    // sessions[i] materializa um AudioSessionControl NOVO a cada acesso
                    // (SessionCollection não cacheia). Ler o indexer uma única vez por
                    // iteração e descartar: antes eram três leituras por sessão, ou seja,
                    // três proxies COM vazados por sessão por chamada.
                    using var session = sessions[i];
                    if (session.IsSystemSoundsSession) continue;
                    channels[(int)session.GetProcessID] = new MixerChannel(session, masterVolume);
                }
            }
        }
        public static List<MixerMaster> GetAllMixers(DataFlow dataFlow, DeviceState deviceState)
        {
            var devices = new List<MixerMaster>();
            using var en = new MMDeviceEnumerator();
            foreach (MMDevice d in en.EnumerateAudioEndPoints(dataFlow, deviceState))
            {
                // O MixerMaster copia tudo que precisa no construtor, então o MMDevice pode
                // (e deve) morrer aqui — ele não sobrevive até a serialização da resposta.
                using (d)
                {
                    devices.Add(new MixerMaster(d));
                }
            }
            return devices;
        }

        public static List<MixerMaster> GetAllMixers(DroidDeck.Audio.IAudioDeviceEnumerator enumerator, DataFlow dataFlow, DeviceState deviceState)
        {
            var devices = new List<MixerMaster>();
            foreach (var d in enumerator.EnumerateAudioEndPoints(dataFlow, deviceState))
            {
                using (d)
                {
                    devices.Add(new MixerMaster(d));
                }
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
            using var devices = new MMDeviceEnumerator();
            MMDevice? device;
            try { device = devices.GetDevice(id); }
            catch { return null; }
            if (device == null) return null;

            using (device)
            {
                float volume;
                volume = (data.Volume ?? -1.0f) / 100.0f;
                bool mute = data.Mute == true;
                var masterVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;

                if (data.Session >= 0 && device.DataFlow.CompareTo(DataFlow.Render) == 0)
                {
                    // Resolve a sessão AO VIVO pelo PID, no device recém-aberto. Antes usava
                    // o AudioSessionControl que este MixerMaster tinha guardado desde a
                    // construção — um proxy COM potencialmente obsoleto (e vazado).
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        using var session = sessions[i];
                        if (session.IsSystemSoundsSession) continue;
                        if ((int)session.GetProcessID != data.Session) continue;
                        MixerChannel.Apply(session, data, masterVolume);
                        break;
                    }
                }
                else
                    if (mute && masterVolume >= volume)
                    device.AudioEndpointVolume.Mute = mute;
                else
                    if (volume > 0)
                    {
                        device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
                        device.AudioEndpointVolume.Mute = false;
                    }
                    else
                        device.AudioEndpointVolume.Mute = mute;

                return new MixerMaster(device);
            }
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
