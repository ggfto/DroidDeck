using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// Reconecta a Tuya no startup e vigia a conexao, no mesmo espirito do ObsAutoConnect.
    ///
    /// A diferenca importante: o intervalo aqui e LONGO. Cada reconexao reenumera dispositivos
    /// (~4 chamadas por aparelho) e a cota gratuita e de 26 mil chamadas/mes; um watchdog
    /// agressivo queimaria a cota do usuario sozinho. O push MQTT ja tem reconexao propria com
    /// backoff, entao este laco so cobre o caso de a sessao inteira ter caido.
    /// </summary>
    public class TuyaAutoConnect : IHostedService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

        private readonly TuyaService _tuya;
        private readonly ILogger<TuyaAutoConnect> _logger;
        private CancellationTokenSource? _cts;

        public TuyaAutoConnect(TuyaService tuya, ILogger<TuyaAutoConnect> logger)
        {
            _tuya = tuya;
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
            // Nao competir com o resto do startup.
            try { await Task.Delay(3000, ct); } catch { return; }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_tuya.HasSavedSession() && !_tuya.Connected)
                    {
                        await _tuya.ConnectAsync();
                        _logger.LogInformation("Tuya auto-connect: conectado.");
                    }
                }
                catch (Exception ex)
                {
                    // Sem internet ou token revogado: tenta de novo no proximo ciclo.
                    _logger.LogDebug("Tuya auto-connect: {Msg}", ex.Message);
                }

                try { await Task.Delay(Interval, ct); } catch { return; }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try { _cts?.Cancel(); } catch { }
            await _tuya.DisposeAsync();
        }
    }
}
