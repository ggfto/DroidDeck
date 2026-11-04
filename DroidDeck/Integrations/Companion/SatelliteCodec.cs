using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DroidDeck.Integrations
{
    /// <summary>
    /// Formato das linhas da Satellite API do Companion, igual ao pacote
    /// @companion-app/satellite-protocol: NOME STATUS? CHAVE=valor CHAVE="com espaco" FLAG.
    /// </summary>
    public static class SatelliteCodec
    {
        /// <summary>Parte a linha no primeiro espaco: comando e parametros.</summary>
        public static (string command, string body) Split(string line)
        {
            var i = line.IndexOf(' ');
            return i < 0 ? (line, "") : (line[..i], line[(i + 1)..]);
        }

        /// <summary>Parametros da linha. Flag sem valor vira null.</summary>
        public static Dictionary<string, string?> Parse(string body)
        {
            var fragments = new List<StringBuilder> { new() };
            var quoted = false;
            for (var i = 0; i < body.Length; i++)
            {
                var c = body[i];
                if (c == '\\')
                {
                    if (i + 1 < body.Length) fragments[^1].Append(body[i + 1]);
                    i++;
                }
                else if (c == '"') quoted = !quoted;
                else if (c == ' ' && !quoted) fragments.Add(new StringBuilder());
                else fragments[^1].Append(c);
            }

            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var fragment in fragments)
            {
                var text = fragment.ToString();
                var eq = text.IndexOf('=');
                if (eq < 0)
                {
                    if (text.Length > 0) result[text] = null;
                }
                else if (eq > 0)
                {
                    result[text[..eq]] = text[(eq + 1)..];
                }
            }
            return result;
        }

        /// <summary>Monta a linha, com o fim de linha. bool vira 1/0, numero vai puro e texto entre aspas.</summary>
        public static string Format(string name, string? deviceId, params (string key, object value)[] args)
        {
            var line = new StringBuilder(name);
            if (deviceId != null) line.Append(" DEVICEID=").Append(Quote(deviceId));
            foreach (var (key, value) in args)
            {
                if (!Regex.IsMatch(key, "^[A-Za-z0-9_-]+$")) throw new ArgumentException("Chave inválida.", nameof(args));
                line.Append(' ').Append(key).Append('=').Append(value switch
                {
                    bool b => b ? "1" : "0",
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    string s => Quote(s),
                    _ => throw new ArgumentException("Tipo inválido.", nameof(args)),
                });
            }
            return line.Append(" \n").ToString();
        }

        private static string Quote(string value)
        {
            if (value.Contains('\r') || value.Contains('\n')) throw new ArgumentException("Quebra de linha no valor.", nameof(value));
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public static bool IsFalsey(string? value) =>
            value != null && (value == "0" || value.Length == 0 || value.Equals("false", StringComparison.OrdinalIgnoreCase));
    }
}
