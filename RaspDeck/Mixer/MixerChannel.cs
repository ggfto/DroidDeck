using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace DroidDeck
{
    /// <summary>
    /// Snapshot (DTO) de uma sessão de áudio. NÃO guarda o <see cref="AudioSessionControl"/>:
    /// ele é COM e ficava vivo enquanto o objeto sobrevivia à serialização, vazando um proxy
    /// por sessão por poll (o app consulta /api/v1/Mixer/{in,out} a cada 60s). Todos os campos
    /// abaixo já são cópias por valor, então o COM pode ser liberado assim que o snapshot é
    /// montado. Para escrever, use <see cref="Apply"/> com uma sessão resolvida na hora.
    /// </summary>
    public class MixerChannel
    {
        public MixerChannel() { }

        public MixerChannel(AudioSessionControl session, float masterVolume)
        {
            Id = 0;
            Description = "Windows";

            if (!session.IsSystemSoundsSession)
            {
                DescribeFromProcess((int)session.GetProcessID);
            }

            Volume = (int)(session.SimpleAudioVolume.Volume * masterVolume * 100);
            Mute = session.SimpleAudioVolume.Mute;
        }

        /// <summary>
        /// Preenche Id/Descrição/Ícone a partir do PID da sessão.
        ///
        /// Compartilhado pelos dois caminhos de enumeração (NAudio direto e a abstração
        /// <see cref="DroidDeck.Audio.IAudioDevice"/>): antes só o caminho do NAudio fazia
        /// isto, e a abstração devolvia canais sem nome nem ícone — divergência silenciosa,
        /// já que ambos populam o mesmo DTO.
        ///
        /// Um único GetProcessById (antes eram dois: ProcessExists + este). Se o processo
        /// morreu entre a enumeração e aqui, lança e o canal fica no default. O
        /// Process/Icon/Bitmap são descartados (using) pra não vazar handle de kernel e
        /// GDI (HICON/HBITMAP).
        /// </summary>
        public void DescribeFromProcess(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                Id = pid;
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

        /// <summary>
        /// Aplica volume/mute numa sessão viva. Estático de propósito: quem chama é dono do
        /// <paramref name="session"/> e o descarta logo em seguida.
        /// </summary>
        public static void Apply(AudioSessionControl session, MixerData data, float masterVolume)
        {
            float volume = (data.Volume ?? -1.0f) / 100.0f;
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

        public int Id { get; set; }
        public string? Description { get; set; }
        public int Volume { get; set; }
        public string? Icon { get; set; }
        public bool Mute { get; set; }
    }
}
