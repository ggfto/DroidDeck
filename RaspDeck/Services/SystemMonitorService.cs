using AnyDeck.Hubs;
using AnyDeck.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AnyDeck.Services
{
    public class SystemMonitorService : BackgroundService
    {
        private readonly ILogger<SystemMonitorService> _logger;
        private readonly IHubContext<DeckHub> _hubContext;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        public SystemMonitorService(ILogger<SystemMonitorService> logger, IHubContext<DeckHub> hubContext)
        {
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("SystemMonitorService is only supported on Windows.");
                return;
            }

            try
            {
                try
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                }
                catch
                {
                    // Fallback for PT-BR
                    try { _cpuCounter = new PerformanceCounter("Processador", "% tempo de processador", "_Total"); }
                    catch (Exception ex) { _logger.LogError(ex, "Failed to init CPU Counter"); }
                }

                try
                {
                    _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                }
                catch
                {
                    // Fallback for PT-BR
                    try { _ramCounter = new PerformanceCounter("Memória", "MBytes disponíveis"); }
                    catch (Exception ex) { _logger.LogError(ex, "Failed to init RAM Counter"); }
                }

                if (_cpuCounter != null) _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal Error initializing Monitors");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cpu = _cpuCounter?.NextValue() ?? 0;
                    var ramAvailable = _ramCounter?.NextValue() ?? 0;

                    // DEBUG LOG TO FILE
                    try {
                        File.AppendAllText("monitor_debug.txt", $"{DateTime.Now}: CPU={cpu}, RAM={ramAvailable}\n");
                    } catch {}

                    // Simple Stats Object
                    var stats = new SystemStats
                    {
                        CpuUsage = (float)Math.Round(cpu, 1),
                        RamAvailable = ramAvailable
                    };

                    // Broadcast to clients listening on "ReceiveSystemStats"
                    await _hubContext.Clients.All.SendAsync("ReceiveSystemStats", stats, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText("monitor_debug.txt", $"{DateTime.Now}: ERROR {ex.Message}\n"); } catch {}
                    _logger.LogError(ex, "Error broadcasting system stats");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
