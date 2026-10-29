using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DroidDeck.Actions;
using DroidDeck.Hubs;
using DroidDeck.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// Despacha cada DeckAction para o IActionProvider do seu tipo. Os tipos em si vivem em
    /// Actions/Providers; aqui so ficam a busca pelo tipo e o tratamento de erro.
    /// </summary>
    public class ActionExecutorService
    {
        private readonly ILogger<ActionExecutorService> _logger;
        private readonly IHubContext<DeckHub>? _hub;
        private readonly IReadOnlyList<IActionProvider> _providers;
        private readonly Dictionary<string, IActionProvider> _byType = new(StringComparer.OrdinalIgnoreCase);

        public ActionExecutorService(ILogger<ActionExecutorService> logger, IEnumerable<IActionProvider> providers,
            IHubContext<DeckHub>? hub = null)
        {
            _logger = logger;
            _hub = hub;
            _providers = providers.ToList();
            foreach (var p in _providers)
            {
                _byType[p.Type] = p;
                foreach (var alias in p.Aliases) _byType[alias] = p;
            }
        }

        public IReadOnlyList<IActionProvider> Providers => _providers;

        public IEnumerable<IStatefulActionProvider> StatefulProviders => _providers.OfType<IStatefulActionProvider>();

        public IActionProvider? Find(string? type) =>
            type != null && _byType.TryGetValue(type, out var p) ? p : null;

        /// <summary>Schema de todos os tipos, com os campos derivados preenchidos.</summary>
        public List<ActionSchema> DescribeAll() => _providers.Select(p =>
        {
            var schema = p.Describe();
            schema.Type = p.Type;
            schema.Privileged = ActionPolicy.IsPrivileged(new DeckAction { Type = p.Type });
            if (p is IStatefulActionProvider sp) schema.StateEvent = sp.StateEvent;
            return schema;
        }).ToList();

        /// <param name="buttonId">Botao que disparou a acao (nulo em /execute avulso).</param>
        public async Task ExecuteActionAsync(DeckAction action, string? buttonId = null)
        {
            var provider = Find(action.Type);
            if (provider == null)
            {
                _logger.LogWarning("Unknown action type: {Type}", action.Type);
                return;
            }

            try
            {
                _logger.LogInformation("Executing action type: {Type}", action.Type);
                await provider.ExecuteAsync(new ActionContext(action, buttonId, ExecuteActionAsync));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing action {Type}", action.Type);

                // O app ja inverteu o botao na hora do toque (otimista). Se a acao falhou
                // (Discord desconectado, nuvem da Tuya recusou...), ninguem mais mandaria o
                // estado e o botao ficava mostrando o que nao aconteceu ate a proxima mudanca.
                if (provider is IStatefulActionProvider stateful && _hub != null)
                {
                    try { await _hub.Clients.All.SendAsync(stateful.StateEvent, stateful.GetState()); }
                    catch { }
                }
            }
        }
    }
}
