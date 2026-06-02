using System;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace AnyDeck.Hubs
{
    public class DeckHub : Hub
    {
        private readonly IServiceProvider _services;

        public DeckHub(IServiceProvider services)
        {
            _services = services;
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        // Ao conectar (ou reconectar), envia o estado atual do Discord só para este cliente,
        // pra UI não ficar desatualizada após reconexão / reinício do backend.
        public override async Task OnConnectedAsync()
        {
            if (_services.GetService(typeof(Services.DiscordRpcService)) is Services.DiscordRpcService discord)
            {
                try
                {
                    await Clients.Caller.SendAsync("ReceiveDiscordState",
                        new { connected = discord.Connected, mute = discord.SelfMute, deaf = discord.SelfDeaf });
                }
                catch { }
            }
            await base.OnConnectedAsync();
        }

        // Add more methods here if clients need to send commands back via SignalR
    }
}
