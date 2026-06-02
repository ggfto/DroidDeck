using AnyDeck.Hubs;
using AnyDeck.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AnyDeck.Services
{
    public class SystemMonitorService : BackgroundService
    {
        private readonly ILogger<SystemMonitorService> _logger;
        private readonly IHubContext<DeckHub> _hubContext;
        private PerformanceCounter? _cpuCounter;

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

                    // RAM via GlobalMemoryStatusEx: total, disponível e % de uso de uma vez.
                    var mem = new MEMORYSTATUSEX();
                    float ramTotal = 0, ramAvailable = 0, ramLoad = 0;
                    if (GlobalMemoryStatusEx(mem))
                    {
                        ramTotal = mem.ullTotalPhys / (1024f * 1024f); // MB
                        ramAvailable = mem.ullAvailPhys / (1024f * 1024f); // MB
                        ramLoad = mem.dwMemoryLoad; // % usada (0-100)
                    }

                    var stats = new SystemStats
                    {
                        CpuUsage = (float)Math.Round(cpu, 1),
                        RamTotal = ramTotal,
                        RamAvailable = ramAvailable,
                        RamUsage = ramLoad,
                    };

                    // Broadcast to clients listening on "ReceiveSystemStats"
                    await _hubContext.Clients.All.SendAsync("ReceiveSystemStats", stats, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting system stats");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    }
}
