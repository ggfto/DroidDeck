using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DroidDeck.Auth;
using DroidDeck.Lib;

namespace DroidDeck
{
    /// <summary>
    /// Janela de pareamento: exibe um QR com ip+porta+chave para o app escanear.
    /// A chave de API só fica visível quando o usuário abre esta janela (pelo menu da bandeja).
    /// </summary>
    public class frmPairing : Form
    {
        public frmPairing()
        {
            Text = "DroidDeck — Parear dispositivo";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(360, 472);

            var ip = NetworkInfo.GetLanIp();
            var key = ApiKeyProvider.GetKey();
            var uri = PairingInfo.BuildUri();

            var info = new Label
            {
                Text = "No app DroidDeck, vá em Configurar → Parear (QR)\ne escaneie este código para conectar com segurança.",
                Location = new Point(10, 8),
                Size = new Size(340, 40),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            var pic = new PictureBox
            {
                Location = new Point(20, 52),
                Size = new Size(320, 320),
                SizeMode = PictureBoxSizeMode.Zoom,
            };
            try
            {
                // Um Bitmap construído a partir de um stream exige que o stream continue vivo
                // pelo resto da vida da imagem — daí a cópia independente, que permite fechar
                // o MemoryStream aqui mesmo. O PictureBox não descarta a própria Image, então
                // o descarte vai no FormClosed (senão vaza um HBITMAP por abertura do diálogo).
                using var ms = new MemoryStream(PairingInfo.BuildQrPng(uri));
                using var qr = new Bitmap(ms);
                pic.Image = new Bitmap(qr);
            }
            catch (Exception ex)
            {
                Log.Error($"Falha ao gerar QR de pareamento: {ex.Message}");
            }

            FormClosed += (_, _) =>
            {
                var img = pic.Image;
                pic.Image = null;
                try { img?.Dispose(); } catch { }
            };

            var details = new TextBox
            {
                Text = $"IP: {ip}:{PairingInfo.Port}    Chave: {key}",
                Location = new Point(10, 380),
                Size = new Size(340, 44),
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                TextAlign = HorizontalAlignment.Center,
            };

            var close = new Button
            {
                Text = "Fechar",
                Location = new Point(140, 432),
                Size = new Size(80, 30),
            };
            close.Click += (_, _) => Close();
            AcceptButton = close;

            Controls.Add(info);
            Controls.Add(pic);
            Controls.Add(details);
            Controls.Add(close);
        }
    }
}
