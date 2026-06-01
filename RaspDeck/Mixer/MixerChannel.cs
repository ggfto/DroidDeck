using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace AnyDeck
{
    public class MixerChannel
    {
        private readonly AudioSessionControl? session;

        public MixerChannel() { }
        public MixerChannel(AudioSessionControl session, float masterVolume)
        {
            Id = 0;
            Description = "Windows";
            Debug.WriteLine($"[MixerChannel] Processing session, IsSystemSounds: {session.IsSystemSoundsSession}");

            if (!session.IsSystemSoundsSession)
            {
                if (ProcessExists(session.GetProcessID))
                {
                    Process process = Process.GetProcessById((int)session.GetProcessID);
                    Description = (process.ProcessName == "Spotify" ? process.ProcessName + ": " : "") + (!string.IsNullOrEmpty(process.MainWindowTitle) ? process.MainWindowTitle : process.ProcessName);
                    Debug.WriteLine($"[MixerChannel] Found process: {process.ProcessName}, Description: {Description}");

                    string? SigBase64 = null;
                    Id = (int)session.GetProcessID;
                    try
                    {
                        var moduleFile = process.MainModule?.FileName;
                        Debug.WriteLine($"[MixerChannel] Module file: {moduleFile}");

                        if (!string.IsNullOrEmpty(moduleFile))
                        {
                            var icon = System.Drawing.Icon.ExtractAssociatedIcon(moduleFile!);
                            if (icon != null)
                            {
                                Bitmap bImage = icon.ToBitmap();
                                System.IO.MemoryStream ms = new MemoryStream();
                                bImage.Save(ms, ImageFormat.Png);
                                byte[] byteImage = ms.ToArray();
                                SigBase64 = "data:image/png;base64," + Convert.ToBase64String(byteImage);
                                Debug.WriteLine($"[MixerChannel] Icon extracted successfully, length: {SigBase64.Length}");
                            }
                            else
                            {
                                Debug.WriteLine("[MixerChannel] ExtractAssociatedIcon returned null");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[MixerChannel] Module file is null or empty");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MixerChannel] Icon extraction failed: {ex.Message}");
                    }
                    finally
                    {
                        Icon = SigBase64;
                        Debug.WriteLine($"[MixerChannel] Final Icon value: {(Icon == null ? "null" : $"{Icon.Length} chars")}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[MixerChannel] Process {session.GetProcessID} does not exist");
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
                var process = Process.GetProcessById((int)processId);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
