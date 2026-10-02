using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DroidDeck.Lib.Input;
using Xunit;

namespace DroidDeck.Tests
{
    public class KeySequenceTests
    {
        private static KeyStroke Key(KeyModifiers m, ushort vk) => KeyStroke.ForKey(m, vk);
        private static KeyStroke Chr(KeyModifiers m, char c) => KeyStroke.ForChar(m, c);

        [Fact]
        public void Modificadores_ValemSoParaAProximaTecla() =>
            Assert.Equal(new[] { Chr(KeyModifiers.Ctrl, 'c'), Chr(KeyModifiers.None, 'v') }, KeySequence.Parse("^cv"));

        [Fact]
        public void ModificadoresSeAcumulam() =>
            Assert.Equal(new[] { Chr(KeyModifiers.Ctrl | KeyModifiers.Shift, 's') }, KeySequence.Parse("^+s"));

        [Fact]
        public void Cerquilha_ETeclaWindows() =>
            Assert.Equal(new[] { Chr(KeyModifiers.Win, 'd') }, KeySequence.Parse("#d"));

        [Fact]
        public void TeclasNomeadas_ComModificador() =>
            Assert.Equal(new[] { Key(KeyModifiers.Alt, 0x73) }, KeySequence.Parse("%{F4}"));

        [Fact]
        public void TeclaNomeada_IgnoraCaixa() =>
            Assert.Equal(KeySequence.Parse("{ENTER}"), KeySequence.Parse("{enter}"));

        [Fact]
        public void Til_EEnter() =>
            Assert.Equal(new[] { Key(KeyModifiers.None, 0x0D) }, KeySequence.Parse("~"));

        [Fact]
        public void Repeticao() =>
            Assert.Equal(3, KeySequence.Parse("{TAB 3}").Count(s => s.VirtualKey == 0x09));

        [Fact]
        public void Grupo_SeguraOModificadorEmTodasAsTeclas() =>
            Assert.Equal(new[] { Chr(KeyModifiers.Shift, 'e'), Chr(KeyModifiers.Shift, 'c'), Chr(KeyModifiers.None, 'x') },
                KeySequence.Parse("+(ec)x"));

        [Theory]
        [InlineData("{+}", '+')]
        [InlineData("{^}", '^')]
        [InlineData("{%}", '%')]
        [InlineData("{#}", '#')]
        [InlineData("{~}", '~')]
        [InlineData("{(}", '(')]
        [InlineData("{{}", '{')]
        [InlineData("{}}", '}')]
        public void Chaves_EscapamCaracteresEspeciais(string keys, char expected) =>
            Assert.Equal(new[] { Chr(KeyModifiers.None, expected) }, KeySequence.Parse(keys));

        [Fact]
        public void TextoComum_EDigitado() =>
            Assert.Equal("olá", new string(KeySequence.Parse("olá").Select(s => s.Char).ToArray()));

        [Theory]
        [InlineData("^")]
        [InlineData("{ENTER")]
        [InlineData("{NAOEXISTE}")]
        [InlineData("(abc")]
        [InlineData("abc)")]
        [InlineData("{TAB 999}")]
        public void SintaxeInvalida_LancaAntesDeEnviar(string keys) =>
            Assert.Throws<FormatException>(() => KeySequence.Parse(keys));
    }

    /// <summary>
    /// Envia de verdade com SendInput e confere o que o Windows recebeu num hook de teclado de
    /// baixo nivel. O hook engole os eventos injetados, entao nada chega ao app em foco.
    /// Precisa de uma sessao de desktop interativa: o CI exclui a categoria (o runner pode nao
    /// ter uma); localmente roda junto com o resto.
    /// </summary>
    [Trait("Category", "RequiresDesktop")]
    public class KeyboardInputTests
    {
        private record Event(uint Vk, uint Scan, bool Up, bool Extended);

        private static List<Event> Capture(string keys)
        {
            var events = new List<Event>();
            uint threadId = 0;
            var ready = new ManualResetEventSlim();
            Exception? error = null;

            LowLevelKeyboardProc proc = (nCode, wParam, lParam) =>
            {
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (nCode >= 0 && (info.flags & LLKHF_INJECTED) != 0)
                {
                    lock (events)
                        events.Add(new Event(info.vkCode, info.scanCode, (info.flags & LLKHF_UP) != 0, (info.flags & LLKHF_EXTENDED) != 0));
                    return (IntPtr)1; // engole: nao chega a janela nenhuma
                }
                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            };

            var hookThread = new Thread(() =>
            {
                threadId = GetCurrentThreadId();
                var hook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero) error = new InvalidOperationException("SetWindowsHookEx falhou");
                ready.Set();
                if (hook == IntPtr.Zero) return;
                while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
                UnhookWindowsHookEx(hook);
            });
            hookThread.Start();
            ready.Wait();
            if (error != null) throw error;

            try
            {
                KeyboardInput.Send(KeySequence.Parse(keys));
                Thread.Sleep(100); // o hook roda na outra thread
            }
            finally
            {
                PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                hookThread.Join();
                GC.KeepAlive(proc);
            }
            lock (events) return events.ToList();
        }

        [Fact]
        public void CtrlC_SaiComScancodesNaOrdemCerta()
        {
            var ev = Capture("^c");

            Assert.Equal(new[] { (0xA2u, false), (0x43u, false), (0x43u, true), (0xA2u, true) },
                ev.Select(e => (e.Vk, e.Up)).ToArray());
            Assert.Equal(0x2Eu, ev[1].Scan); // scancode do C: o que jogos com DirectInput leem
        }

        [Fact]
        public void CtrlCMaiusculo_NaoAcrescentaShift() =>
            Assert.DoesNotContain(Capture("^C"), e => e.Vk is 0xA0 or 0xA1 or 0x10);

        [Fact]
        public void TeclaWindows_SaiComoEstendida()
        {
            var ev = Capture("#{F24}");

            var win = ev.First();
            Assert.Equal(0x5Bu, win.Vk);
            Assert.True(win.Extended);
            Assert.Contains(ev, e => e.Vk == 0x87 && !e.Up); // F24
        }

        [Fact]
        public void SetaParaCima_SaiComoEstendida() =>
            Assert.True(Capture("{UP}").First().Extended);

        [Fact]
        public void CaractereForaDoLayout_VaiComoUnicode() =>
            Assert.All(Capture("☃"), e => Assert.Equal(0xE7u, e.Vk)); // VK_PACKET

        // ---- Win32 ----
        private const int WH_KEYBOARD_LL = 13;
        private const uint WM_QUIT = 0x0012;
        private const uint LLKHF_EXTENDED = 0x01, LLKHF_INJECTED = 0x10, LLKHF_UP = 0x80;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    }
}
