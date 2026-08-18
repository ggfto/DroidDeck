using System;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

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

        // Ao conectar (ou reconectar), envia o estado atual do Discord só para este cliente,
        // pra UI não ficar desatualizada após reconexão / reinício do backend.
        public override async Task OnConnectedAsync()
        {
            Interlocked.Increment(ref _connectedCount);
            if (_services.GetService(typeof(Services.DiscordRpcService)) is Services.DiscordRpcService discord)
            {
                try
                {
                    await Clients.Caller.SendAsync("ReceiveDiscordState", discord.GetStatePayload());
                }
                catch { }
            }
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
            // Estado do OBS (cena/gravando/transmitindo) pra os botões refletirem.
            if (_services.GetService(typeof(Services.ObsService)) is Services.ObsService obs)
            {
                try { await Clients.Caller.SendAsync("ReceiveObsState", obs.GetStatePayload()); }
                catch { }
            }
            // Estado da Tuya + dispositivos. Sem isto, quem conecta depois do backend só
            // receberia algo quando algum aparelho mudasse — os botões abririam sem cor.
            if (_services.GetService(typeof(Services.TuyaService)) is Services.TuyaService tuya)
            {
                try { await Clients.Caller.SendAsync("ReceiveTuyaState", tuya.GetStatePayload()); }
                catch { }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Interlocked.Decrement(ref _connectedCount);
            await base.OnDisconnectedAsync(exception);
        }

        // Add more methods here if clients need to send commands back via SignalR
    }
}
