using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using DroidDeck.Lib;

namespace DroidDeck.Hubs
{
    public class DeckHub : Hub
    {
        private readonly IServiceProvider _services;

        // Nº de clientes conectados. O SystemMonitorService usa isto pra NÃO ficar
        // fazendo broadcast e poll de mídia (WinRT caro) quando ninguém está ouvindo.
        private static int _connectedCount = 0;
        public static int ConnectedCount => Volatile.Read(ref _connectedCount);

        public DeckHub(IServiceProvider services)
        {
            _services = services;
        }

        // Ao conectar (ou reconectar), envia o estado atual de cada integração só para este
        // cliente, pra UI não ficar desatualizada após reconexão / reinício do backend.
        public override async Task OnConnectedAsync()
        {
            Interlocked.Increment(ref _connectedCount);
            // Idioma da conexao: ?lang= (o app manda) ou o Accept-Language (navegador).
            var http = Context.GetHttpContext();
            var language = http?.Request.Query["lang"].ToString() is { Length: > 0 } q
                ? L.LanguageOf(q)
                : L.LanguageOf(http?.Request.Headers.AcceptLanguage.ToString());
            Context.Items[LanguageKey] = language;
            L.Language = language;
            await Groups.AddToGroupAsync(Context.ConnectionId, LanguageGroup(language));
            // Manda a grade física atual (o configurador web a usa no lugar de um seletor manual).
            if (_services.GetService(typeof(Services.StreamDeckConfigService)) is Services.StreamDeckConfigService cfg)
            {
                try
                {
                    var layout = cfg.GetLayout();
                    await Clients.Caller.SendAsync("ReceiveLayoutUpdate",
                        new { rows = layout.Rows, columns = layout.Columns });
                }
                catch { }
            }
            // Estado de cada provedor com estado ao vivo (Discord, OBS, Tuya, timers...).
            if (_services.GetService(typeof(Services.ActionExecutorService)) is Services.ActionExecutorService executor)
            {
                foreach (var provider in executor.StatefulProviders)
                {
                    try { await Clients.Caller.SendAsync(provider.StateEvent, provider.GetState()); }
                    catch { }
                }
            }
            // Botoes de dois estados ligados e valores dos botoes de informacao.
            if (_services.GetService(typeof(Services.ButtonToggleService)) is Services.ButtonToggleService toggles)
            {
                try { await Clients.Caller.SendAsync(Services.ButtonToggleService.Event, toggles.GetState()); }
                catch { }
            }
            if (_services.GetService(typeof(Services.InfoService)) is Services.InfoService info)
            {
                try
                {
                    await Clients.Caller.SendAsync(Services.InfoService.Event, info.GetState());
                    if (info.NowPlaying is { } np)
                        await Clients.Caller.SendAsync(Services.InfoService.NowPlayingEvent, np);
                }
                catch { }
            }
            // Perfil do app em foco, para o celular abrir ja no perfil certo.
            if (_services.GetService(typeof(Services.ForegroundWatcher)) is Services.ForegroundWatcher fg)
            {
                try { await Clients.Caller.SendAsync(Services.ForegroundWatcher.Event, fg.GetState()); }
                catch { }
            }
            await base.OnConnectedAsync();
        }

        /// <summary>O app trocou de idioma: passa a receber no novo e ja recebe os textos traduzidos.</summary>
        public async Task SetLanguage(string language)
        {
            language = L.LanguageOf(language);
            if (Context.Items.TryGetValue(LanguageKey, out var old) && old is string o)
            {
                if (o == language) return;
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, LanguageGroup(o));
            }
            Context.Items[LanguageKey] = language;
            L.Language = language;
            await Groups.AddToGroupAsync(Context.ConnectionId, LanguageGroup(language));
            if (_services.GetService(typeof(Services.InfoService)) is Services.InfoService info)
            {
                try { await Clients.Caller.SendAsync(Services.InfoService.Event, info.GetState()); }
                catch { }
            }
        }

        private const string LanguageKey = "lang";

        /// <summary>Um grupo por arquivo de idioma.</summary>
        public static IReadOnlyList<string> Languages => L.Languages;

        public static string LanguageGroup(string language) => "lang:" + language;

        public Task SendKeyDown(string key)
        {
            Lib.Input.KeyboardInput.KeyDown(key);
            return Task.CompletedTask;
        }

        public Task SendKeyUp(string key)
        {
            Lib.Input.KeyboardInput.KeyUp(key);
            return Task.CompletedTask;
        }

        public Task ReleaseAllKeys()
        {
            Lib.Input.KeyboardInput.ReleaseAll();
            return Task.CompletedTask;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Lib.Input.KeyboardInput.ReleaseAll();
            Interlocked.Decrement(ref _connectedCount);
            await base.OnDisconnectedAsync(exception);
        }
    }

    public static class LocalizedHubExtensions
    {
        /// <summary>
        /// Envia para cada grupo de idioma a sua versao: payload() roda com L.Language daquele
        /// idioma, entao L.T e L.Text dentro dele saem traduzidos.
        /// </summary>
        public static async Task SendLocalizedAsync(this IHubContext<DeckHub> hub, string method, Func<object?> payload)
        {
            var previous = L.Language;
            try
            {
                foreach (var language in DeckHub.Languages)
                {
                    L.Language = language;
                    await hub.Clients.Group(DeckHub.LanguageGroup(language)).SendAsync(method, payload());
                }
            }
            finally
            {
                L.Language = previous;
            }
        }
    }
}
