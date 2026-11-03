using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DroidDeck.Lib.Input;
using DroidDeck.Services;

namespace DroidDeck.Actions.Providers
{
    /// <summary>Abre um programa, arquivo ou URL. Privilegiada: so nasce no configurador.</summary>
    public class LaunchAppProvider : IActionProvider
    {
        private readonly IAppActivator _activator;

        public LaunchAppProvider(IAppActivator activator) => _activator = activator;

        public string Type => "launch_app";
        public IReadOnlyCollection<string> Aliases => new[] { "launchapp" };

        public ActionSchema Describe() => new()
        {
            Type = Type,
            Title = "Abrir programa",
            Icon = "launch",
            Category = "Sistema",
            Description = "Abre um programa, arquivo ou endereço no PC.",
            Fields =
            {
                ActionField.Text("path", "Caminho", @"C:\Windows\System32\notepad.exe",
                    "Também aceita um arquivo ou uma URL.", required: true),
                ActionField.Text("arguments", "Argumentos (opcional)"),
            },
        };

        public Task ExecuteAsync(ActionContext c)
        {
            var path = c.Get("path");
            if (path.Length > 0) _activator.LaunchApp(path, c.Get("arguments"));
            return Task.CompletedTask;
        }
    }

    /// <summary>Traz uma janela para a frente pelo titulo. Privilegiada.</summary>
    public class ActivateWindowProvider : IActionProvider
    {
        private readonly IAppActivator _activator;

        public ActivateWindowProvider(IAppActivator activator) => _activator = activator;

        public string Type => "activatewindow";

        public ActionSchema Describe() => new()
        {
            Type = Type,
            Title = "Ativar janela",
            Icon = "web_asset",
            Category = "Sistema",
            Fields = { ActionField.Text("windowName", "Título exato da janela", required: true) },
        };

        public Task ExecuteAsync(ActionContext c)
        {
            _activator.ActivateWindow(c.Get("windowName"));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Uma tecla do teclado. Privilegiada (ActionPolicy): com teclas soltas da para chegar
    /// a Win+R e digitar um comando, entao so nasce no configurador, como o atalho.
    ///
    /// Modos: "hold" segura enquanto o dedo estiver no botao -- o app manda o aperta e o
    /// solta pelo hub (DeckHub.HoldKey/ReleaseKey); por aqui (gatilho, /press sem hub) vira
    /// toque rapido. "toggle" trava a tecla apertada ate o proximo toque (Shift, Ctrl...);
    /// "oneshot" trava so ate a proxima tecla do deck, como as teclas de aderencia.
    /// </summary>
    public class KeyProvider : IActionProvider, IDisposable
    {
        public const string LatchOwner = "latch";

        private readonly ButtonToggleService _toggles;
        private readonly HashSet<string> _latchedButtons = new();

        public KeyProvider(ButtonToggleService toggles)
        {
            _toggles = toggles;
            HeldKeys.Shared.OneShotReleased += OnOneShotReleased;
        }

        public string Type => "key";
        public IReadOnlyCollection<string> Aliases => new[] { "keyboard_key", "send_key" };

        public ActionSchema Describe() => new()
        {
            Type = Type,
            Title = "Tecla do teclado",
            Icon = "keyboard",
            Category = "Sistema",
            Description = "Aperta uma tecla no PC: segura enquanto o dedo estiver no botão, dá um toque rápido ou fica travada até o próximo toque.",
            Fields =
            {
                new ActionField
                {
                    Key = "key",
                    Label = "Tecla",
                    Kind = FieldKind.Select,
                    AllowCustom = true,
                    Required = true,
                    Default = "UP",
                    Help = "Escolha na lista ou digite outra: A-Z, 0-9, F13-F24, NUMPAD0 ou o código (0x41).",
                    Options = KeyOptions.Select(o => new FieldOption(o.value, o.label)).ToList(),
                },
                ActionField.Select("mode", "Modo de envio",
                    ("hold", "Segurar enquanto o dedo estiver no botão"),
                    ("tap", "Toque rápido"),
                    ("toggle", "Travar: um toque aperta, outro solta"),
                    ("oneshot", "Travar até a próxima tecla"))
                    .WithDefault("hold"),
            },
        };

        private static readonly (string value, string label)[] KeyOptions =
        {
            ("UP", "Seta para cima"), ("DOWN", "Seta para baixo"),
            ("LEFT", "Seta para a esquerda"), ("RIGHT", "Seta para a direita"),
            ("SHIFT", "Shift"), ("CTRL", "Ctrl"), ("ALT", "Alt"), ("WIN", "Windows"),
            ("ENTER", "Enter"), ("ESC", "Esc"), ("TAB", "Tab"), ("SPACE", "Espaço"), ("BACKSPACE", "Backspace"),
            ("INSERT", "Insert"), ("DELETE", "Delete"), ("HOME", "Home"), ("END", "End"),
            ("PGUP", "Page Up"), ("PGDN", "Page Down"),
            ("F1", "F1"), ("F2", "F2"), ("F3", "F3"), ("F4", "F4"), ("F5", "F5"), ("F6", "F6"),
            ("F7", "F7"), ("F8", "F8"), ("F9", "F9"), ("F10", "F10"), ("F11", "F11"), ("F12", "F12"),
        };

        public async Task ExecuteAsync(ActionContext c)
        {
            if (!KeyboardInput.TryResolveKey(c.Get("key"), out var vk)) return;

            var mode = c.Get("mode");
            var oneShot = string.Equals(mode, "oneshot", StringComparison.OrdinalIgnoreCase);
            if (oneShot || string.Equals(mode, "toggle", StringComparison.OrdinalIgnoreCase))
            {
                // A trava vale por botao (ou pela tecla, numa execucao avulsa). O estado aceso
                // segue o da tecla, nao o liga/desliga do botao: os dois nunca se desencontram.
                var owner = (oneShot ? HeldKeys.OneShotPrefix : LatchOwner + ":") + (c.ButtonId ?? vk.ToString());
                var on = !HeldKeys.Shared.IsHeld(owner, vk);
                if (on) HeldKeys.Shared.Down(owner, vk);
                else HeldKeys.Shared.Up(owner, vk);
                if (c.ButtonId != null)
                {
                    lock (_latchedButtons)
                    {
                        if (on) _latchedButtons.Add(c.ButtonId);
                        else _latchedButtons.Remove(c.ButtonId);
                    }
                    _toggles.Set(c.ButtonId, on);
                }
                return;
            }

            KeyboardInput.KeyDown(vk);
            await Task.Delay(KeyboardInput.HoldMs);
            KeyboardInput.KeyUp(vk);
            HeldKeys.Shared.KeyTapped(vk);
        }

        // Trava "ate a proxima tecla" que soltou sozinha: apaga o botao nos celulares.
        private void OnOneShotReleased(string owner)
        {
            var buttonId = owner[HeldKeys.OneShotPrefix.Length..];
            lock (_latchedButtons)
                if (!_latchedButtons.Remove(buttonId)) return;
            _toggles.Set(buttonId, false);
        }

        /// <summary>
        /// PC encerrando: solta as travas e apaga o aceso delas (o toggles.json sobrevive ao
        /// reinicio, a tecla apertada nao).
        /// </summary>
        public void Dispose()
        {
            HeldKeys.Shared.OneShotReleased -= OnOneShotReleased;
            string[] latched;
            lock (_latchedButtons) latched = _latchedButtons.ToArray();
            foreach (var id in latched) _toggles.Set(id, false);
            HeldKeys.Shared.ReleaseAll();
        }
    }

    /// <summary>Atalho de teclado via SendInput. Privilegiada.</summary>
    public class HotkeyProvider : IActionProvider
    {
        private readonly IAppActivator _activator;

        public HotkeyProvider(IAppActivator activator) => _activator = activator;

        public string Type => "hotkey";

        public ActionSchema Describe() => new()
        {
            Type = Type,
            Title = "Atalho de teclado",
            Icon = "keyboard",
            Category = "Sistema",
            Fields =
            {
                ActionField.Text("keys", "Teclas", "^C", "^ = Ctrl, + = Shift, % = Alt, # = Win, {ENTER}, {F5}…", required: true),
            },
        };

        public Task ExecuteAsync(ActionContext c)
        {
            var keys = c.Get("keys");
            if (keys.Length == 0) return Task.CompletedTask;
            _activator.SendKeys(keys);
            // Shift "ate a proxima tecla" vale para o atalho e solta depois dele.
            HeldKeys.Shared.KeyTapped();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Acoes que so o app executa (navegar entre perfis). Ficam no schema para aparecerem no
    /// seletor de tipo; se chegarem ao servidor, nao ha o que fazer.
    /// </summary>
    public class ClientOnlyProvider : IActionProvider
    {
        private readonly ActionSchema _schema;

        public ClientOnlyProvider(ActionSchema schema) => _schema = schema;

        public string Type => _schema.Type;
        public ActionSchema Describe() => _schema;
        public Task ExecuteAsync(ActionContext c) => Task.CompletedTask;

        public static IEnumerable<IActionProvider> All() => new IActionProvider[]
        {
            new ClientOnlyProvider(new ActionSchema
            {
                Type = "open_profile", Title = "Abrir perfil", Icon = "folder", Category = "Navegação",
                Description = "Abre outro perfil como uma pasta.", Editor = "open_profile",
            }),
            new ClientOnlyProvider(new ActionSchema
            {
                Type = "back", Title = "Voltar", Icon = "arrow_back", Category = "Navegação",
                Description = "Volta ao perfil anterior.",
            }),
        };
    }
}
