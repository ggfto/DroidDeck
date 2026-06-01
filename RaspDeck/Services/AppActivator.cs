using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AnyDeck.Services
{
    public class AppActivator : IAppActivator
    {
        [DllImport("USER32.DLL")]
        private static extern bool SetForegroundWindow(System.IntPtr hWnd);

        [DllImport("USER32.DLL", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern System.IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        public void ActivateWindow(string? name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var h = FindWindow(null, name);
            if (h != System.IntPtr.Zero)
                SetForegroundWindow(h);
        }

        public void SendKeys(string keys)
        {
            if (string.IsNullOrEmpty(keys)) return;
            System.Windows.Forms.SendKeys.SendWait(keys);
        }

        public void LaunchApp(string path, string? arguments = null)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments ?? "",
                UseShellExecute = true // Allows opening URLs and documents
            };
            System.Diagnostics.Process.Start(startInfo);
        }
    }
}
