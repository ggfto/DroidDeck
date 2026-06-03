using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// No startup, tenta reconectar ao Discord usando o token já salvo (sem popup).
    /// Se o Discord não estiver aberto ou o token expirou, apenas registra e segue —
    /// o usuário reconecta manualmente (que aí pode reautorizar).
    /// </summary>
    public class DiscordAutoConnect : IHostedService
    {
        private readonly DiscordRpcService _discord;
        private readonly ILogger<DiscordAutoConnect> _logger;

        public DiscordAutoConnect(DiscordRpcService discord, ILogger<DiscordAutoConnect> logger)
        {
            _discord = discord;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var cfg = _discord.LoadConfig();
            if (!string.IsNullOrEmpty(cfg.ClientId) && !string.IsNullOrEmpty(cfg.AccessToken))
            {
                _ = Task.Run(async () =>
                {
                    try { await _discord.ConnectAsync(interactive: false); }
                    catch (Exception ex) { _logger.LogInformation("Discord auto-connect: {Msg}", ex.Message); }
                });
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _discord.Disconnect();
            return Task.CompletedTask;
        }
    }
}
