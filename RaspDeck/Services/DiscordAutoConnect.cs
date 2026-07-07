using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// Watchdog de conexão do Discord. Enquanto o backend roda, verifica a cada poucos
    /// segundos: se há credenciais + token salvos (discord.json) e não estamos conectados,
    /// tenta reconectar sem popup (interactive:false). Cobre o Discord que abre DEPOIS do
    /// backend, que reinicia no meio da sessão, ou a conexão que caiu (ex.: PING/CLOSE) —
    /// sem precisar abrir a config e tocar "Conectar" toda vez. Espelha o ObsAutoConnect.
    /// Se o token expirou, a tentativa falha em silêncio (Debug) e o usuário reconecta
    /// manualmente uma vez (aí reautoriza).
    /// </summary>
    public class DiscordAutoConnect : IHostedService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

        private readonly DiscordRpcService _discord;
        private readonly ILogger<DiscordAutoConnect> _logger;
        private CancellationTokenSource? _cts;

        public DiscordAutoConnect(DiscordRpcService discord, ILogger<DiscordAutoConnect> logger)
        {
            _discord = discord;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => WatchdogAsync(_cts.Token));
            return Task.CompletedTask;
        }

        private async Task WatchdogAsync(CancellationToken ct)
        {
            // Pequeno atraso pra não competir com o resto do startup.
            try { await Task.Delay(2000, ct); } catch { return; }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var cfg = _discord.LoadConfig();
                    if (!string.IsNullOrEmpty(cfg.ClientId) &&
                        !string.IsNullOrEmpty(cfg.AccessToken) &&
                        !_discord.Connected)
                    {
                        await _discord.ConnectAsync(interactive: false);
                        _logger.LogInformation("Discord auto-connect: conectado.");
                    }
                }
                catch (Exception ex)
                {
                    // Discord fechado / token expirado: tenta de novo no próximo ciclo.
                    _logger.LogDebug("Discord auto-connect: {Msg}", ex.Message);
                }

                try { await Task.Delay(Interval, ct); } catch { return; }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try { _cts?.Cancel(); } catch { }
            _discord.Disconnect();
            return Task.CompletedTask;
        }
    }
}
