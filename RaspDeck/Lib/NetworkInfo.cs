using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DroidDeck.Lib
{
    public static class NetworkInfo
    {
        /// <summary>
        /// IP deste PC na interface que alcança <paramref name="remote"/>. "Conectar" um
        /// socket UDP só consulta a tabela de rotas, não envia nada. Responder o discovery com
        /// isto acerta a placa certa em PC com várias interfaces (VPN, WSL, Hyper-V), não
        /// depende de rota para a internet e acompanha troca de IP pelo DHCP.
        /// </summary>
        public static string GetLocalIpFor(IPAddress remote)
        {
            try
            {
                if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
                using var socket = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(remote, 9);
                if (socket.LocalEndPoint is IPEndPoint ep && !ep.Address.Equals(IPAddress.Any))
                    return ep.Address.ToString();
            }
            catch
            {
                // sem rota para o remetente: cai no IP genérico da LAN
            }
            return GetLanIp();
        }

        /// <summary>
        /// IP da LAN para o QR de pareamento: a interface da rota padrão; sem internet (sem
        /// rota padrão), a melhor interface ativa com IPv4 privado.
        /// </summary>
        public static string GetLanIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint ep && !ep.Address.Equals(IPAddress.Any))
                    return ep.Address.ToString();
            }
            catch
            {
                // sem rota padrão (PC offline): procura nas interfaces
            }
            return FindLanIpFromInterfaces() ?? "127.0.0.1";
        }

        private static string? FindLanIpFromInterfaces()
        {
            var candidates =
                from nic in NetworkInterface.GetAllNetworkInterfaces()
                where nic.OperationalStatus == OperationalStatus.Up
                      && nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                let props = nic.GetIPProperties()
                from addr in props.UnicastAddresses
                where addr.Address.AddressFamily == AddressFamily.InterNetwork && IsPrivate(addr.Address)
                orderby IsVirtual(nic) ascending,                      // placa física primeiro
                        props.GatewayAddresses.Count > 0 descending    // depois a que tem gateway
                select addr.Address.ToString();
            return candidates.FirstOrDefault();
        }

        private static bool IsVirtual(NetworkInterface nic)
        {
            var d = (nic.Description + " " + nic.Name).ToLowerInvariant();
            return d.Contains("virtual") || d.Contains("hyper-v") || d.Contains("vethernet")
                || d.Contains("vmware") || d.Contains("virtualbox") || d.Contains("wsl")
                || d.Contains("vpn") || d.Contains("tap-") || d.Contains("wireguard");
        }

        private static bool IsPrivate(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168);
        }
    }
}
