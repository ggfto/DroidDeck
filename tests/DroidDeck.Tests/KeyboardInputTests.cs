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
            Assert.Equal("olÃ¡", new string(KeySequence.Parse("olÃ¡").Select(s => s.Char).ToArray()));

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
            Assert.All(Capture("â˜ƒ"), e => Assert.Equal(0xE7u, e.Vk)); // VK_PACKET

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

    public class KeyboardInputUnitTests
    {
        [Theory]
        [InlineData("UP", 0x26)]
        [InlineData("DOWN", 0x28)]
        [InlineData("LEFT", 0x25)]
        [InlineData("RIGHT", 0x27)]
        [InlineData("{UP}", 0x26)]
        [InlineData("F1", 0x70)]
        [InlineData("F5", 0x74)]
        [InlineData("F12", 0x7B)]
        [InlineData("{F12}", 0x7B)]
        [InlineData("SHIFT", 0x10)]
        [InlineData("CTRL", 0x11)]
        [InlineData("CONTROL", 0x11)]
        [InlineData("ALT", 0x12)]
        [InlineData("WIN", 0x5B)]
        [InlineData("ENTER", 0x0D)]
        [InlineData("ESC", 0x1B)]
        [InlineData("TAB", 0x09)]
        [InlineData("INSERT", 0x2D)]
        [InlineData("DELETE", 0x2E)]
        [InlineData("HOME", 0x24)]
        [InlineData("END", 0x23)]
        [InlineData("PGUP", 0x21)]
        [InlineData("PGDN", 0x22)]
        [InlineData("A", 0x41)]
        [InlineData("a", 0x41)]
        [InlineData("1", 0x31)]
        public void TryResolveKey_TeclasComunsEMapas_RetornaCodigoCorreto(string keyName, ushort expectedVk)
        {
            Assert.True(KeyboardInput.TryResolveKey(keyName, out var vk));
            Assert.Equal(expectedVk, vk);
        }

        [Theory]
        [InlineData("^C")]
        [InlineData("{ENTER}{TAB}")]
        [InlineData("0x00")]
        [InlineData("")]
        public void TryResolveKey_ComboOuInvalida_Recusa(string keyName)
        {
            Assert.False(KeyboardInput.TryResolveKey(keyName, out _));
        }

        [Fact]
        public void Tecla_Privilegiada()
        {
            foreach (var type in new[] { "key", "keyboard_key", "send_key" })
                Assert.True(DroidDeck.Services.ActionPolicy.IsPrivileged(new DroidDeck.Models.DeckAction { Type = type }));
        }
    }

    /// <summary>HeldKeys com um envio falso: nada chega ao teclado do PC.</summary>
    public class HeldKeysTests
    {
        private readonly List<(ushort vk, bool down)> _sent = new();

        private HeldKeys Make() => new(vk => { lock (_sent) _sent.Add((vk, true)); }, vk => { lock (_sent) _sent.Add((vk, false)); })
        {
            RepeatDelay = TimeSpan.FromHours(1),
        };

        private (ushort, bool)[] Sent() { lock (_sent) return _sent.ToArray(); }

        [Fact]
        public void ApertaESolta()
        {
            using var keys = Make();
            keys.Down("cel", 0x26);
            Assert.Contains((ushort)0x26, keys.Keys);
            keys.Up("cel", 0x26);
            Assert.Empty(keys.Keys);
            Assert.Equal(new (ushort, bool)[] { (0x26, true), (0x26, false) }, Sent());
        }

        [Fact]
        public void MesmaTeclaPorDoisDonos_SoSobeQuandoOsDoisSoltam()
        {
            using var keys = Make();
            keys.Down("a", 0x26);
            keys.Down("b", 0x26);
            keys.Up("a", 0x26);
            Assert.DoesNotContain(((ushort)0x26, false), Sent());
            keys.Up("b", 0x26);
            Assert.Equal(((ushort)0x26, false), Sent().Last());
        }

        [Fact]
        public void ConexaoQueCai_SoltaSoAsDela()
        {
            using var keys = Make();
            keys.Down("cel1", 0x10);
            keys.Down("cel1", 0x26);
            keys.Down("latch:x", 0x11);
            keys.ReleaseOwner("cel1");
            Assert.Equal(new ushort[] { 0x11 }, keys.Keys.ToArray());
            Assert.Contains(((ushort)0x10, false), Sent());
            Assert.Contains(((ushort)0x26, false), Sent());
        }

        [Fact]
        public void SoltarSemTerApertado_NaoMandaNada()
        {
            using var keys = Make();
            keys.Up("cel", 0x26);
            keys.ReleaseOwner("cel");
            Assert.Empty(Sent());
        }

        [Fact]
        public void TravaAteAProximaTecla_SoltaQuandoOutraTeclaSobe()
        {
            using var keys = Make();
            var soltos = new List<string>();
            keys.OneShotReleased += soltos.Add;

            keys.Down(HeldKeys.OneShotPrefix + "shift", 0x10);
            keys.Down(HeldKeys.OneShotPrefix + "ctrl", 0x11);
            keys.Down("latch:alt", 0x12);

            // Outro modificador nao conta; a seta segurada mantem o Shift ate ela subir.
            keys.Down("cel", 0x5B);
            keys.Up("cel", 0x5B);
            keys.Down("cel", 0x26);
            Assert.Contains((ushort)0x10, keys.Keys);
            keys.Up("cel", 0x26);

            Assert.Equal(new ushort[] { 0x12 }, keys.Keys.ToArray());
            Assert.Equal(new[] { HeldKeys.OneShotPrefix + "shift", HeldKeys.OneShotPrefix + "ctrl" }, soltos.OrderByDescending(s => s));
        }

        [Fact]
        public void TravaAteAProximaTecla_SoltaDepoisDeUmToqueOuAtalho()
        {
            using var keys = Make();
            keys.Down(HeldKeys.OneShotPrefix + "shift", 0x10);
            keys.KeyTapped(0x11); // so Ctrl: nao conta
            Assert.Contains((ushort)0x10, keys.Keys);
            keys.KeyTapped();
            Assert.Empty(keys.Keys);
        }

        [Fact]
        public void TeclaSegurada_Repete_ModificadorNao()
        {
            using var keys = Make();
            keys.RepeatDelay = TimeSpan.FromMilliseconds(20);
            keys.RepeatInterval = TimeSpan.FromMilliseconds(10);

            keys.Down("cel", 0x10); // Shift
            Thread.Sleep(150);
            Assert.Single(Sent(), s => s == (0x10, true));

            keys.Down("cel", 0x26); // seta
            Thread.Sleep(150);
            Assert.True(Sent().Count(s => s == (0x26, true)) > 2);

            keys.Up("cel", 0x26);
            var depois = Sent().Length;
            Thread.Sleep(100);
            Assert.Equal(depois, Sent().Length);
        }
    }
}

namespace DroidDeck.Tests
{
    public class KeyboardProfileSeedTests
    {
        [Fact]
        public void PerfilTeclado_CabeNaGradePadrao_EPastasExistem()
        {
            var profiles = DroidDeck.Services.StreamDeckConfigService.CreateKeyboardProfiles();
            var ids = profiles.Select(p => p.Id).ToHashSet();
            var layout = new DroidDeck.Models.DeviceLayout();
            foreach (var p in profiles)
            {
                Assert.All(p.Buttons, b => Assert.True(b.Row < layout.Rows && b.Column < layout.Columns, $"{p.Name}: {b.Label} fora da grade"));
                Assert.Equal(p.Buttons.Count, p.Buttons.Select(b => (b.Row, b.Column)).Distinct().Count());
                foreach (var b in p.Buttons.Where(b => b.Action?.Type == "open_profile"))
                    Assert.Contains(b.Action!.Parameters["profileId"], ids);
                foreach (var b in p.Buttons.Where(b => b.Action?.Type == "key"))
                    Assert.True(KeyboardInput.TryResolveKey(b.Action!.Parameters["key"], out _), b.Action.Parameters["key"]);
            }
        }
    }
}
