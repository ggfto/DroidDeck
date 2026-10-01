using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace DroidDeck.Lib.Input
{
    /// <summary>
    /// Envia toques de teclado com SendInput. Substitui o SendKeys do WinForms, que nao manda
    /// a tecla Windows e gera so mensagens de virtual-key -- jogos que leem DirectInput/Raw
    /// Input nao viam nada. Aqui cada tecla vai com o scancode (o que esses jogos leem) e e
    /// segurada por alguns ms, porque quem le o teclado uma vez por frame perde um
    /// aperta-solta instantaneo.
    /// </summary>
    public static class KeyboardInput
    {
        /// <summary>Quanto cada tecla fica apertada.</summary>
        public static int HoldMs { get; set; } = 20;

        private const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B;

        // Teclas que precisam da flag "estendida". O MapVirtualKeyEx devolve para setas,
        // Home/End/PgUp/PgDn/Ins/Del o scancode do teclado numerico SEM o prefixo E0: sem esta
        // lista, {UP} virava o 8 do numpad (e digitava "8" com o NumLock ligado).
        private static readonly HashSet<ushort> ExtendedKeys = new()
        {
            0x03,                               // Break
            0x21, 0x22, 0x23, 0x24,             // PgUp, PgDn, End, Home
            0x25, 0x26, 0x27, 0x28,             // setas
            0x2C, 0x2D, 0x2E,                   // PrintScreen, Insert, Delete
            0x5B, 0x5C, 0x5D,                   // Win esq/dir, Menu
            0x6F, 0x90,                         // / do numpad, NumLock
            0xA3, 0xA5,                         // Ctrl e Alt da direita
            0xAD, 0xAE, 0xAF,                   // volume
            0xB0, 0xB1, 0xB2, 0xB3,             // midia
        };

        public static void Send(IReadOnlyList<KeyStroke> strokes)
        {
            if (strokes.Count == 0) return;

            // O layout do app em foco, nao o do DroidDeck: e nele que o caractere precisa existir.
            var hwnd = GetForegroundWindow();
            var hkl = GetKeyboardLayout(hwnd == IntPtr.Zero ? 0 : GetWindowThreadProcessId(hwnd, out _));

            for (int i = 0; i < strokes.Count; i++)
            {
                SendStroke(strokes[i], hkl);
                if (i < strokes.Count - 1) Thread.Sleep(5);
            }
        }

        private static void SendStroke(KeyStroke stroke, IntPtr hkl)
        {
            var mods = stroke.Modifiers;
            INPUT keyDown, keyUp;

            if (stroke.IsChar)
            {
                short scan = VkKeyScanEx(stroke.Char, hkl);
                if (scan == -1)
                {
                    // Caractere que nao existe no layout atual: vai como Unicode.
                    keyDown = Unicode(stroke.Char, up: false);
                    keyUp = Unicode(stroke.Char, up: true);
                }
                else
                {
                    ushort vk = (ushort)(scan & 0xFF);
                    var implied = (KeyModifiers)((scan >> 8) & 0x07); // 1 Shift, 2 Ctrl, 4 Alt
                    // Com Ctrl/Alt/Win, letra nao carrega Shift pela caixa: "^C" e Ctrl+C, como
                    // a dica do editor sempre disse. Shift explicito continua sendo "+".
                    bool isLetter = vk >= 'A' && vk <= 'Z';
                    if (isLetter && (mods & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win)) != 0)
                        implied &= ~KeyModifiers.Shift;
                    mods |= implied;
                    keyDown = Key(vk, hkl, up: false);
                    keyUp = Key(vk, hkl, up: true);
                }
            }
            else
            {
                keyDown = Key(stroke.VirtualKey, hkl, up: false);
                keyUp = Key(stroke.VirtualKey, hkl, up: true);
            }

            var modKeys = new List<ushort>(4);
            if (mods.HasFlag(KeyModifiers.Ctrl)) modKeys.Add(VK_CONTROL);
            if (mods.HasFlag(KeyModifiers.Alt)) modKeys.Add(VK_MENU);
            if (mods.HasFlag(KeyModifiers.Shift)) modKeys.Add(VK_SHIFT);
            if (mods.HasFlag(KeyModifiers.Win)) modKeys.Add(VK_LWIN);

            var down = new List<INPUT>(modKeys.Count + 1);
            foreach (var m in modKeys) down.Add(Key(m, hkl, up: false));
            down.Add(keyDown);

            var up = new List<INPUT>(modKeys.Count + 1) { keyUp };
            for (int k = modKeys.Count - 1; k >= 0; k--) up.Add(Key(modKeys[k], hkl, up: true));

            Dispatch(down);
            Thread.Sleep(HoldMs);
            Dispatch(up);
        }

        private static void Dispatch(List<INPUT> inputs)
        {
            var arr = inputs.ToArray();
            uint sent = SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
            // Bloqueio por UIPI (janela elevada) NAO aparece aqui: o Windows descarta calado.
            if (sent != arr.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput falhou");
        }

        private static INPUT Key(ushort vk, IntPtr hkl, bool up)
        {
            uint flags = up ? KEYEVENTF_KEYUP : 0;
            uint scan = MapVirtualKeyEx(vk, MAPVK_VK_TO_VSC_EX, hkl);
            if ((scan & 0xFF) != 0)
            {
                flags |= KEYEVENTF_SCANCODE;
                if ((scan & 0xFF00) is 0xE000 or 0xE100 || ExtendedKeys.Contains(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
            }
            // Sem scancode (teclas de midia/volume em alguns teclados): vai so pela virtual-key.
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = (ushort)(scan & 0xFF), dwFlags = flags } },
            };
        }

        private static INPUT Unicode(char c, bool up) => new()
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0) } },
        };

        // ---- Win32 ----
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4, KEYEVENTF_SCANCODE = 0x8;
        private const uint MAPVK_VK_TO_VSC_EX = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        // O union precisa do MOUSEINPUT (o maior membro) para o INPUT ter o tamanho que o
        // SendInput espera; com so o KEYBDINPUT ele recusa tudo.
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScanEx(char ch, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);
    }
}
