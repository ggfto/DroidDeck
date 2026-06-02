using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AnyDeck.Lib
{
    internal class DiscoveryServer : BackgroundService
    {
        private const int DiscoveryPort = 7573;
        private UdpClient? udpServer;
        private readonly ILogger<DiscoveryServer> _logger;

        public DiscoveryServer(ILogger<DiscoveryServer> logger)
        {
            _logger = logger;
        }

        private string FindMyIP()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                if (endPoint == null)
                {
                    return "127.0.0.1";
                }
                return endPoint.Address.ToString();
            }
        }
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            try
            {
                udpServer = new UdpClient(DiscoveryPort);
                _logger.LogInformation("[Discovery] Servidor iniciado na porta {Port}", DiscoveryPort);
                string ipAddress = FindMyIP();
                string computerName = Environment.MachineName;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var receiveTask = udpServer.ReceiveAsync();
                        var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));

                        if (completed != receiveTask)
                        {
                            // cancellation requested
                            break;
                        }

                        var result = receiveTask.Result;
                        string dataReceived = Encoding.ASCII.GetString(result.Buffer);
                        _logger.LogDebug("[Discovery] Recebido '{Data}' de {Remote}", dataReceived, result.RemoteEndPoint);

                        if ("AnyDeckDiscoveryRequest".Equals(dataReceived))
                        {
                            string responseMessage = "{\"ip\": \"IP_ADDR\", \"name\": \"COMPUTER_NAME\"}";
                            responseMessage = responseMessage.Replace("IP_ADDR", ipAddress);
                            responseMessage = responseMessage.Replace("COMPUTER_NAME", computerName);
                            byte[] sendBytes = Encoding.ASCII.GetBytes(responseMessage);
                            await udpServer.SendAsync(sendBytes, sendBytes.Length, result.RemoteEndPoint);
                            _logger.LogDebug("[Discovery] Resposta enviada para {Remote}", result.RemoteEndPoint);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException se)
                    {
                        _logger.LogError(se, "Socket error in Discovery listener");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in Discovery listener");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Discovery server");
            }
            finally
            {
                try { udpServer?.Close(); udpServer?.Dispose(); } catch { }
            }
        }
    }
}
