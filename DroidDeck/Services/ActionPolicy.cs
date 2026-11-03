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
    /// Requisicao HTTP entra na mesma regra: ela sai do PC com o acesso de rede dele, inclusive
    /// a servicos que so aceitam localhost.
    ///
    /// Regra: acao privilegiada so nasce ou muda pelo proprio PC (configurador em
    /// localhost). Clientes remotos (o celular) continuam apertando esses botoes -- pelo id,
    /// e o servidor le a acao do perfil salvo -- e editando os demais tipos livremente.
    /// </summary>
    public static class ActionPolicy
    {
        private static readonly HashSet<string> PrivilegedTypes =
            new(StringComparer.OrdinalIgnoreCase) { "launchapp", "launch_app", "activatewindow", "hotkey", "key", "keyboard_key", "send_key", "http", "script" };

        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Requisicao vinda da propria maquina (loopback).</summary>
        public static bool IsLocal(IPAddress? ip)
        {
            if (ip == null) return false;
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            return IPAddress.IsLoopback(ip);
        }

        /// <summary>
        /// Requisicao do proprio PC: loopback que nao veio de um celular pelo cabo USB (o
        /// adb reverse tambem chega como loopback; ver Lib/UsbBridge.cs).
        /// </summary>
        public static bool IsLocal(Microsoft.AspNetCore.Http.ConnectionInfo connection) =>
            IsLocal(connection.RemoteIpAddress) &&
            !Lib.UsbBridge.IsForwarded(connection.RemoteIpAddress, connection.RemotePort);

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

        /// <summary>Tipos de botao de informacao que executam algo no PC.</summary>
        private static readonly HashSet<string> PrivilegedInfoTypes =
            new(StringComparer.OrdinalIgnoreCase) { "command", "url" };

        public static bool IsPrivilegedInfo(DeckButton button) =>
            PrivilegedInfoTypes.Contains(button.DynamicType ?? "");

        private static bool SameInfo(DeckButton a, DeckButton b) =>
            string.Equals(a.DynamicType, b.DynamicType, StringComparison.OrdinalIgnoreCase) &&
            SameAction(new DeckAction { Parameters = a.DynamicParameters ?? new() },
                       new DeckAction { Parameters = b.DynamicParameters ?? new() });

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
                var before = saved?.Buttons?.FirstOrDefault(b => b.Id == button.Id);
                // Toque, toque longo, toque duplo e a acao do estado ligado.
                foreach (var slot in DeckButton.ActionSlots)
                {
                    var action = button.ActionFor(slot);
                    if (!IsPrivileged(action)) continue;
                    if (before == null || !SameAction(before.ActionFor(slot), action))
                        return button;
                }
                // Botoes de informacao que rodam um comando ou leem uma URL no PC.
                if (IsPrivilegedInfo(button) &&
                    (before == null || !SameInfo(before, button)))
                    return button;
            }
            return null;
        }
    }
}
