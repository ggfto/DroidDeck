using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// Watchdog de conexão do OBS. Enquanto o backend roda, verifica a cada poucos
    /// segundos: se o OBS já foi configurado (obs.json existe) e não estamos conectados,
    /// tenta conectar. Cobre os casos de o OBS abrir DEPOIS do backend (autostart) ou
    /// reiniciar no meio da sessão — sem precisar tocar em "Conectar" toda vez.
    /// Tentativas que falham (OBS fechado) são silenciosas (log Debug). Espelha a ideia
    /// do DiscordAutoConnect, mas reconecta continuamente em vez de só no boot.
    /// </summary>
    public class ObsAutoConnect : IHostedService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

        private readonly ObsService _obs;
        private readonly ILogger<ObsAutoConnect> _logger;
        private CancellationTokenSource? _cts;

        public ObsAutoConnect(ObsService obs, ILogger<ObsAutoConnect> logger)
        {
            _obs = obs;
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
                    if (_obs.HasSavedConfig() && !_obs.Connected)
                    {
                        await _obs.ConnectAsync();
                        _logger.LogInformation("OBS auto-connect: conectado.");
                    }
                }
                catch (Exception ex)
                {
                    // OBS fechado / handshake falhou: tenta de novo no próximo ciclo.
                    _logger.LogDebug("OBS auto-connect: {Msg}", ex.Message);
                }

                try { await Task.Delay(Interval, ct); } catch { return; }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try { _cts?.Cancel(); } catch { }
            _obs.Disconnect();
            return Task.CompletedTask;
        }
    }
}
