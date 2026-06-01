using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace AnyDeck.Hubs
{
    public class DeckHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        // Add more methods here if clients need to send commands back via SignalR
    }
}
