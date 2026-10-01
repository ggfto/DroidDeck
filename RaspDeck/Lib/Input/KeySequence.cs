using System;
using System.Collections.Generic;
using System.Text;

namespace DroidDeck.Lib.Input
{
    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Shift = 1,
        Ctrl = 2,
        Alt = 4,
        Win = 8,
    }

    /// <summary>
    /// Um toque: modificadores segurados + uma tecla. A tecla e uma virtual-key (teclas
    /// nomeadas, como {ENTER}) ou um caractere (resolvido no layout do app em foco na hora
    /// de enviar).
    /// </summary>
    public readonly record struct KeyStroke(KeyModifiers Modifiers, ushort VirtualKey, char Char)
    {
        public bool IsChar => VirtualKey == 0;

        public static KeyStroke ForKey(KeyModifiers mods, ushort vk) => new(mods, vk, '\0');
        public static KeyStroke ForChar(KeyModifiers mods, char c) => new(mods, 0, c);
    }

    /// <summary>
    /// Le a sintaxe do SendKeys do WinForms, que os botoes ja salvos usam, mais "#" = tecla
    /// Windows (como no AutoHotkey; o SendKeys nao tinha como mandar a tecla Windows).
    ///
    ///   ^ Ctrl   + Shift   % Alt   # Win   -- valem para a proxima tecla ou grupo "( )"
    ///   ~ ENTER   {ENTER} {F5} {TAB 3}     -- tecla nomeada, opcionalmente repetida
    ///   {+} {^} {%} {#} {~} {(} {{} ...    -- o caractere literal
    ///   qualquer outro caractere e digitado.
    /// </summary>
    public static class KeySequence
    {
        // Virtual-key codes (WinUser.h).
        private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BACKSPACE"] = 0x08, ["BS"] = 0x08, ["BKSP"] = 0x08,
            ["TAB"] = 0x09,
            ["ENTER"] = 0x0D,
            ["BREAK"] = 0x13,
            ["CAPSLOCK"] = 0x14,
            ["ESC"] = 0x1B, ["ESCAPE"] = 0x1B,
            ["SPACE"] = 0x20,
            ["PGUP"] = 0x21, ["PGDN"] = 0x22,
            ["END"] = 0x23, ["HOME"] = 0x24,
            ["LEFT"] = 0x25, ["UP"] = 0x26, ["RIGHT"] = 0x27, ["DOWN"] = 0x28,
            ["PRTSC"] = 0x2C,
            ["INSERT"] = 0x2D, ["INS"] = 0x2D,
            ["DELETE"] = 0x2E, ["DEL"] = 0x2E,
            ["HELP"] = 0x2F,
            ["WIN"] = 0x5B, ["LWIN"] = 0x5B, ["RWIN"] = 0x5C, ["APPS"] = 0x5D, ["MENU"] = 0x5D,
            ["MULTIPLY"] = 0x6A, ["ADD"] = 0x6B, ["SUBTRACT"] = 0x6D, ["DECIMAL"] = 0x6E, ["DIVIDE"] = 0x6F,
            ["NUMLOCK"] = 0x90, ["SCROLLLOCK"] = 0x91,
            ["VOLUME_MUTE"] = 0xAD, ["VOLUME_DOWN"] = 0xAE, ["VOLUME_UP"] = 0xAF,
            ["MEDIA_NEXT"] = 0xB0, ["MEDIA_PREV"] = 0xB1, ["MEDIA_STOP"] = 0xB2, ["MEDIA_PLAY_PAUSE"] = 0xB3,
        };

        static KeySequence()
        {
            for (int i = 1; i <= 24; i++) NamedKeys["F" + i] = (ushort)(0x70 + i - 1);
            for (int i = 0; i <= 9; i++) NamedKeys["NUMPAD" + i] = (ushort)(0x60 + i);
        }

        public static List<KeyStroke> Parse(string keys)
        {
            var result = new List<KeyStroke>();
            int i = 0;
            ParseInto(keys ?? "", ref i, KeyModifiers.None, result, insideGroup: false);
            return result;
        }

        private static void ParseInto(string s, ref int i, KeyModifiers outer, List<KeyStroke> output, bool insideGroup)
        {
            var pending = KeyModifiers.None;
            while (i < s.Length)
            {
                char c = s[i];
                switch (c)
                {
                    case '+': pending |= KeyModifiers.Shift; i++; continue;
                    case '^': pending |= KeyModifiers.Ctrl; i++; continue;
                    case '%': pending |= KeyModifiers.Alt; i++; continue;
                    case '#': pending |= KeyModifiers.Win; i++; continue;
                    case '(':
                        i++;
                        ParseInto(s, ref i, outer | pending, output, insideGroup: true);
                        pending = KeyModifiers.None;
                        continue;
                    case ')':
                        if (!insideGroup) throw new FormatException($"')' sem '(' correspondente (posicao {i}).");
                        if (pending != KeyModifiers.None) throw new FormatException($"Modificador sem tecla antes de ')' (posicao {i}).");
                        i++;
                        return;
                    case '~':
                        output.Add(KeyStroke.ForKey(outer | pending, 0x0D));
                        pending = KeyModifiers.None;
                        i++;
                        continue;
                    case '{':
                        ParseBraced(s, ref i, outer | pending, output);
                        pending = KeyModifiers.None;
                        continue;
                    case '}':
                        throw new FormatException($"'}}' sem '{{' correspondente (posicao {i}).");
                    default:
                        output.Add(KeyStroke.ForChar(outer | pending, c));
                        pending = KeyModifiers.None;
                        i++;
                        continue;
                }
            }
            if (insideGroup) throw new FormatException("'(' sem ')' correspondente.");
            if (pending != KeyModifiers.None) throw new FormatException("Modificador no fim sem tecla (ex.: \"^\" sozinho).");
        }

        // {NOME}, {NOME n}, {x} literal (inclusive "{}}" e "{{}").
        private static void ParseBraced(string s, ref int i, KeyModifiers mods, List<KeyStroke> output)
        {
            int start = i + 1;
            // "{}}" e o literal '}': o primeiro '}' faz parte do nome.
            int close = s.IndexOf('}', start + (start < s.Length && s[start] == '}' ? 1 : 0));
            if (close < 0) throw new FormatException($"'{{' sem '}}' correspondente (posicao {i}).");

            var body = s.Substring(start, close - start);
            i = close + 1;

            var name = body;
            int count = 1;
            int space = body.LastIndexOf(' ');
            if (space > 0 && int.TryParse(body.AsSpan(space + 1), out var n))
            {
                if (n < 0 || n > 100) throw new FormatException($"Repeticao fora de 0-100 em {{{body}}}.");
                name = body.Substring(0, space);
                count = n;
            }

            KeyStroke stroke;
            if (NamedKeys.TryGetValue(name, out var vk))
                stroke = KeyStroke.ForKey(mods, vk);
            else if (name.Length == 1)
                stroke = KeyStroke.ForChar(mods, name[0]);
            else
                throw new FormatException($"Tecla desconhecida: {{{name}}}.");

            for (int k = 0; k < count; k++) output.Add(stroke);
        }

        /// <summary>Texto legivel de uma sequencia, para log.</summary>
        public static string Describe(IEnumerable<KeyStroke> strokes)
        {
            var sb = new StringBuilder();
            foreach (var st in strokes)
            {
                if (sb.Length > 0) sb.Append(' ');
                if (st.Modifiers.HasFlag(KeyModifiers.Ctrl)) sb.Append("Ctrl+");
                if (st.Modifiers.HasFlag(KeyModifiers.Alt)) sb.Append("Alt+");
                if (st.Modifiers.HasFlag(KeyModifiers.Shift)) sb.Append("Shift+");
                if (st.Modifiers.HasFlag(KeyModifiers.Win)) sb.Append("Win+");
                sb.Append(st.IsChar ? st.Char.ToString() : $"VK{st.VirtualKey:X2}");
            }
            return sb.ToString();
        }
    }
}
