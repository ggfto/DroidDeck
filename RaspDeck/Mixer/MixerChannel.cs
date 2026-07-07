using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace DroidDeck
{
    public class MixerChannel
    {
        private readonly AudioSessionControl? session;

        public MixerChannel() { }
        public MixerChannel(AudioSessionControl session, float masterVolume)
        {
            Id = 0;
            Description = "Windows";

            if (!session.IsSystemSoundsSession && ProcessExists(session.GetProcessID))
            {
                Id = (int)session.GetProcessID;

                // GetProcessById fica DENTRO do try: se o processo terminar entre o
                // ProcessExists e aqui (corrida), lança ArgumentException — antes isso
                // subia e quebrava a enumeração inteira (500). O Process/Icon/Bitmap são
                // descartados (using) pra não vazar handle de kernel e GDI (HICON/HBITMAP).
                try
                {
                    using var process = Process.GetProcessById((int)session.GetProcessID);
                    Description = (process.ProcessName == "Spotify" ? process.ProcessName + ": " : "") + (!string.IsNullOrEmpty(process.MainWindowTitle) ? process.MainWindowTitle : process.ProcessName);

                    var moduleFile = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(moduleFile))
                    {
                        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(moduleFile!);
                        if (icon != null)
                        {
                            using var bmp = icon.ToBitmap();
                            using var ms = new MemoryStream();
                            bmp.Save(ms, ImageFormat.Png);
                            Icon = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
                catch
                {
                    // Descrição/ícone são best-effort; ficam no default se falhar.
                }
            }

            Volume = (int)(session.SimpleAudioVolume.Volume * masterVolume * 100);
            Mute = session.SimpleAudioVolume.Mute;
            this.session = session;
        }

        public AudioSessionControl? GetSession()
        {
            return session;
        }

        public void SetOptions(MixerData data, float masterVolume)
        {
            if (session != null)
            {
                float volume;
                volume = (data.Volume ?? -1.0f) / 100.0f;
                var newVolume = volume / masterVolume;
                bool mute = data.Mute == true;
                if (mute && session.SimpleAudioVolume.Volume >= newVolume)
                    session.SimpleAudioVolume.Mute = mute;
                else
                    if (newVolume > 0)
                {
                    if (newVolume <= 1)
                        session.SimpleAudioVolume.Volume = newVolume;
                    else
                        session.SimpleAudioVolume.Volume = 1;
                    session.SimpleAudioVolume.Mute = false;
                }
                else
                    session.SimpleAudioVolume.Mute = mute;
            }
        }

        public int Id { get; set; }
        public string? Description { get; set; }
        public int Volume { get; set; }
        public string? Icon { get; set; }
        public bool Mute { get; set; }

        private bool ProcessExists(uint processId)
        {
            try
            {
                using var process = Process.GetProcessById((int)processId);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
