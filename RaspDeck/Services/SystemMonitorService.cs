using DroidDeck.Hubs;
using DroidDeck.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DroidDeck.Services
{
    public class SystemMonitorService : BackgroundService
    {
        private readonly ILogger<SystemMonitorService> _logger;
        private readonly IHubContext<DeckHub> _hubContext;
        private readonly MediaControlService _media;
        private PerformanceCounter? _cpuCounter;

        // Estado para throughput de rede (deltas por segundo).
        private long _lastSent;
        private long _lastRecv;
        private DateTime _lastNetTime = DateTime.UtcNow;

        // Counters de GPU (uso) por instância de engine 3D; cacheados entre ticks.
        private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new();

        public SystemMonitorService(ILogger<SystemMonitorService> logger, IHubContext<DeckHub> hubContext, MediaControlService media)
        {
            _logger = logger;
            _hubContext = hubContext;
            _media = media;
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

                    var (netUp, netDown) = SampleNetwork();

                    var stats = new SystemStats
                    {
                        CpuUsage = (float)Math.Round(cpu, 1),
                        RamTotal = ramTotal,
                        RamAvailable = ramAvailable,
                        RamUsage = ramLoad,
                        NetUpKBps = netUp,
                        NetDownKBps = netDown,
                        GpuUsage = SampleGpu(),
                    };

                    // Broadcast to clients listening on "ReceiveSystemStats"
                    await _hubContext.Clients.All.SendAsync("ReceiveSystemStats", stats, cancellationToken: stoppingToken);

                    // Estado de reprodução de mídia (pra botões play/pause refletirem).
                    try
                    {
                        var playing = await _media.IsAnythingPlayingAsync();
                        await _hubContext.Clients.All.SendAsync("ReceiveMediaStatus",
                            new { playing }, cancellationToken: stoppingToken);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting system stats");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        private (float up, float down) SampleNetwork()
        {
            long sent = 0, recv = 0;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var s = ni.GetIPv4Statistics();
                    sent += s.BytesSent;
                    recv += s.BytesReceived;
                }
            }
            catch { }

            var now = DateTime.UtcNow;
            var elapsed = (now - _lastNetTime).TotalSeconds;
            float up = 0, down = 0;
            if (_lastSent > 0 && elapsed > 0.1)
            {
                up = (float)Math.Max(0, (sent - _lastSent) / elapsed / 1024.0);   // KB/s
                down = (float)Math.Max(0, (recv - _lastRecv) / elapsed / 1024.0); // KB/s
            }
            _lastSent = sent;
            _lastRecv = recv;
            _lastNetTime = now;
            return (up, down);
        }

        private float SampleGpu()
        {
            try
            {
                var cat = new PerformanceCounterCategory("GPU Engine");
                var names = cat.GetInstanceNames()
                    .Where(n => n.EndsWith("engtype_3D"))
                    .ToHashSet();

                foreach (var n in names)
                {
                    if (!_gpuCounters.ContainsKey(n))
                    {
                        var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", n, true);
                        try { c.NextValue(); } catch { } // prime: 1ª leitura é 0
                        _gpuCounters[n] = c;
                    }
                }
                foreach (var key in _gpuCounters.Keys.Where(k => !names.Contains(k)).ToList())
                {
                    try { _gpuCounters[key].Dispose(); } catch { }
                    _gpuCounters.Remove(key);
                }

                float sum = 0;
                foreach (var c in _gpuCounters.Values)
                {
                    try { sum += c.NextValue(); } catch { }
                }
                return Math.Min(sum, 100f);
            }
            catch
            {
                return 0; // GPU Engine indisponível neste sistema
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
