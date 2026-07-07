using System;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DroidDeck
{
    /// <summary>
    /// Janela invisível que serve apenas de host para o ícone de bandeja (NotifyIcon).
    /// O DroidDeck roda como app de bandeja: o servidor web sobe em paralelo (ver Program.cs)
    /// e o usuário interage pelo menu do ícone. Não há janela visível.
    /// </summary>
    public partial class frmPrincipal : Form
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRunName = "DroidDeck";
        private const string WebUrl = "http://localhost:4787/";

        private ToolStripMenuItem _autoStartItem = null!;

        public frmPrincipal()
        {
            InitializeComponent();

            // Nunca aparece na barra de tarefas nem como janela; só o ícone de bandeja.
            ShowInTaskbar = false;
            BuildTrayMenu();
            ntiTray.Visible = true;
        }

        // A janela nunca é exibida, mas o handle precisa existir para o message loop
        // (e o ícone de bandeja) continuarem vivos. Sem CreateHandle, o Application.Run
        // encerraria de imediato por não haver janela principal realizada.
        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated) CreateHandle();
            base.SetVisibleCore(false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ntiTray.ShowBalloonTip(3000, "DroidDeck", "Rodando na bandeja — servidor em " + WebUrl, ToolTipIcon.Info);
        }

        private void BuildTrayMenu()
        {
            var menu = new ContextMenuStrip();

            var openItem = new ToolStripMenuItem("Abrir no navegador");
            openItem.Click += (_, _) => OpenWebUi();

            var pairItem = new ToolStripMenuItem("Parear dispositivo (QR)…");
            pairItem.Click += (_, _) =>
            {
                using var f = new frmPairing();
                f.ShowDialog();
            };

            _autoStartItem = new ToolStripMenuItem("Iniciar com o Windows")
            {
                CheckOnClick = true,
                Checked = IsAutoStartEnabled(),
            };
            _autoStartItem.Click += (_, _) => SetAutoStart(_autoStartItem.Checked);

            var exitItem = new ToolStripMenuItem("Sair");
            exitItem.Click += (_, _) => Application.Exit();

            menu.Items.Add(openItem);
            menu.Items.Add(pairItem);
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            ntiTray.ContextMenuStrip = menu;
            ntiTray.DoubleClick += (_, _) => OpenWebUi();
        }

        private static void OpenWebUi()
        {
            try
            {
                Process.Start(new ProcessStartInfo(WebUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Lib.Log.Error($"Falha ao abrir a Web UI: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Remove o ícone para não deixar "fantasma" na bandeja.
            ntiTray.Visible = false;
            base.OnFormClosing(e);
        }

        private static bool IsAutoStartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppRunName) != null;
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                    ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (key == null) return;

                if (enable)
                    key.SetValue(AppRunName, $"\"{Application.ExecutablePath}\"");
                else
                    key.DeleteValue(AppRunName, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                Lib.Log.Error($"Falha ao configurar autostart: {ex.Message}");
            }
        }
    }
}
