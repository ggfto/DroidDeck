using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using DroidDeck.Models;

namespace DroidDeck.Services
{
    /// <summary>
    /// Quem pode criar e disparar acoes que executam codigo no PC.
    ///
    /// Abrir programa, atalho de teclado e ativar janela dao execucao de codigo a quem os
    /// controla -- um atalho basta: Ctrl+Esc, digita "powershell", Enter. Antes, qualquer
    /// cliente com a chave mandava uma DeckAction arbitraria para /execute, ou gravava um
    /// perfil com um botao desses e depois o apertava. A chave trafega em HTTP pela LAN,
    /// entao vaza-la equivalia a entregar o PC.
    ///
    /// Regra: acao privilegiada so nasce ou muda pelo proprio PC (configurador em
    /// localhost). Clientes remotos (o celular) continuam apertando esses botoes -- pelo id,
    /// e o servidor le a acao do perfil salvo -- e editando os demais tipos livremente.
    /// </summary>
    public static class ActionPolicy
    {
        private static readonly HashSet<string> PrivilegedTypes =
            new(StringComparer.OrdinalIgnoreCase) { "launchapp", "launch_app", "activatewindow", "hotkey" };

        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Requisicao vinda da propria maquina (loopback).</summary>
        public static bool IsLocal(IPAddress? ip)
        {
            if (ip == null) return false;
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            return IPAddress.IsLoopback(ip);
        }

        /// <summary>A acao executa codigo no PC? Multi-acao herda dos passos.</summary>
        public static bool IsPrivileged(DeckAction? action)
        {
            if (action == null) return false;
            if (PrivilegedTypes.Contains(action.Type ?? "")) return true;
            if (!string.Equals(action.Type, "multi", StringComparison.OrdinalIgnoreCase)) return false;

            if (!action.Parameters.TryGetValue("steps", out var stepsJson) || string.IsNullOrWhiteSpace(stepsJson))
                return false;
            try
            {
                var steps = JsonSerializer.Deserialize<List<MultiStep>>(stepsJson, JsonOpts);
                return steps != null && steps.Any(s => PrivilegedTypes.Contains(s.Type ?? ""));
            }
            catch (JsonException)
            {
                // Nao da para saber o que tem dentro: na duvida, trata como privilegiada.
                return true;
            }
        }

        /// <summary>Mesmo tipo e mesmos parametros.</summary>
        public static bool SameAction(DeckAction? a, DeckAction? b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (!string.Equals(a.Type, b.Type, StringComparison.OrdinalIgnoreCase)) return false;
            if (a.Parameters.Count != b.Parameters.Count) return false;
            foreach (var (key, value) in a.Parameters)
            {
                if (!b.Parameters.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Para gravacoes vindas de fora do PC: devolve o primeiro botao que cria ou altera
        /// uma acao privilegiada (comparando com o mesmo botao, pelo id, no perfil salvo),
        /// ou null se a gravacao so mexe no que e permitido. Mover, renomear, recolorir ou
        /// apagar um botao privilegiado continua liberado.
        /// </summary>
        public static DeckButton? FindRemoteViolation(DeckProfile incoming, DeckProfile? saved)
        {
            foreach (var button in incoming.Buttons ?? new List<DeckButton>())
            {
                if (!IsPrivileged(button.Action)) continue;

                var before = saved?.Buttons?.FirstOrDefault(b => b.Id == button.Id);
                if (before == null || !SameAction(before.Action, button.Action))
                    return button;
            }
            return null;
        }
    }
}
