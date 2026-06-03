using System.Net;
using System.Net.Sockets;

namespace DroidDeck.Lib
{
    public static class NetworkInfo
    {
        /// <summary>
        /// IP da LAN pela rota padrão (mesma técnica do DiscoveryServer: "conecta"
        /// um socket UDP a um IP externo e lê o endpoint local). Não envia nada.
        /// </summary>
        public static string GetLanIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
