using Windows.Media.Control;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    /// <summary>
    /// Acesso ao WinRT de mídia (GlobalSystemMediaTransportControls). Duas regras aqui:
    ///
    /// 1. O session manager é criado UMA vez e reusado. Pedir um novo a cada chamada — que era
    ///    o jeito de fugir do RPC_E_WRONG_THREAD (0x8001010E) ao cachear e usar de threads
    ///    diferentes — faz uma ativação COM entre processos por chamada, a cada 3s enquanto
    ///    houver cliente conectado, e nenhuma delas é liberada de forma determinística.
    ///
    /// 2. Todo acesso é serializado por um portão e tem teto de tempo. Quando o serviço de
    ///    mídia do Windows engasga, a chamada RPC fica pendurada (o log de produção mostrou
    ///    /api/v1/Media/sessions levando 6-10s e estourando COMException 0x80010002). Sem
    ///    serialização, cada tick empilhava mais uma thread bloqueada esperando resposta de
    ///    LPC. Com o portão fica no máximo UMA operação em voo, e quem não consegue entrar
    ///    desiste na hora em vez de acumular.
    ///
    /// Como todo acesso passa pelo portão, o manager é sempre usado de forma serializada — é
    /// isso que torna seguro cacheá-lo, sem reintroduzir o RPC_E_WRONG_THREAD.
    /// </summary>
    public class MediaControlService
    {
        private readonly ILogger<MediaControlService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private GlobalSystemMediaTransportControlsSessionManager? _manager;

        /// <summary>Se já há operação em voo, desiste rápido em vez de enfileirar.</summary>
        private static readonly TimeSpan GateWait = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        // Disjuntor. O broker de mídia do Windows (GSMTC) pode simplesmente parar de
        // responder para a máquina inteira — verificado em 2026-08-04 com um console de 20
        // linhas fora deste projeto: RequestAsync devolveu RPC_E_CALL_CANCELED (0x80010002)
        // após ~10s em MTA e nem retornou em STA. Sem disjuntor, cada request de mídia paga
        // o timeout inteiro e o poller de 3s fica martelando um serviço quebrado.
        private int _consecutiveFailures;
        private long _circuitOpenUntilTicks;
        private const int FailuresToOpen = 3;
        private static readonly TimeSpan CircuitCooldown = TimeSpan.FromSeconds(60);

        public MediaControlService(ILogger<MediaControlService> logger)
        {
            _logger = logger;
        }

        /// <summary>True se <paramref name="task"/> terminou dentro do prazo. Cancela o timer
        /// quando a tarefa ganha, pra não acumular um Task.Delay pendente por chamada.</summary>
        private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource();
            var winner = await Task.WhenAny(task, Task.Delay(timeout, cts.Token)).ConfigureAwait(false);
            if (winner != task) return false;
            cts.Cancel();
            return true;
        }

        /// <summary>
        /// Executa uma operação sobre o session manager de forma serializada e com timeout,
        /// devolvendo <paramref name="fallback"/> em vez de propagar falha — os chamadores
        /// (poller de 3s e endpoints REST) tratam ausência de mídia como estado normal.
        /// </summary>
        private async Task<T> RunAsync<T>(
            Func<GlobalSystemMediaTransportControlsSessionManager, Task<T>> op,
            T fallback,
            string what,
            TimeSpan? timeout = null)
        {
            var limit = timeout ?? DefaultTimeout;

            // Disjuntor aberto: responde na hora, sem tocar no WinRT.
            if (Volatile.Read(ref _circuitOpenUntilTicks) > DateTime.UtcNow.Ticks)
            {
                _logger.LogDebug("Mídia: '{What}' curto-circuitado — subsistema marcado como indisponível.", what);
                return fallback;
            }

            if (!await _gate.WaitAsync(GateWait).ConfigureAwait(false))
            {
                _logger.LogDebug("Mídia: '{What}' descartado — outra operação ainda em voo.", what);
                return fallback;
            }

            try
            {
                var manager = _manager;
                if (manager == null)
                {
                    var request = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
                    if (!await CompletesWithinAsync(request, limit).ConfigureAwait(false))
                    {
                        _logger.LogWarning("Mídia: timeout ({Secs}s) obtendo o session manager.", limit.TotalSeconds);
                        NoteFailure("timeout no RequestAsync");
                        return fallback;
                    }
                    _manager = manager = await request.ConfigureAwait(false);
                }

                var task = op(manager);
                if (!await CompletesWithinAsync(task, limit).ConfigureAwait(false))
                {
                    _logger.LogWarning("Mídia: timeout ({Secs}s) em '{What}'; o manager será recriado.", limit.TotalSeconds, what);
                    _manager = null;
                    NoteFailure($"timeout em {what}");
                    return fallback;
                }

                var result = await task.ConfigureAwait(false);
                NoteSuccess();
                return result;
            }
            catch (Exception ex)
            {
                // Proxy possivelmente obsoleto (serviço de mídia reiniciado, sessão sumiu):
                // força recriação na próxima chamada em vez de repetir o erro para sempre.
                _manager = null;
                _logger.LogError(ex, "Mídia: falha em '{What}'", what);
                NoteFailure($"{ex.GetType().Name} 0x{ex.HResult:X8}");
                return fallback;
            }
            finally
            {
                _gate.Release();
            }
        }

        // NoteSuccess/NoteFailure só são chamados de dentro do portão, então _consecutiveFailures
        // não precisa de sincronização; o tique de reabertura é lido fora e usa Volatile.
        private void NoteSuccess()
        {
            if (_consecutiveFailures != 0 || Volatile.Read(ref _circuitOpenUntilTicks) != 0)
                _logger.LogInformation("Mídia: subsistema respondendo de novo.");
            _consecutiveFailures = 0;
            Volatile.Write(ref _circuitOpenUntilTicks, 0);
        }

        private void NoteFailure(string reason)
        {
            if (++_consecutiveFailures < FailuresToOpen) return;

            Volatile.Write(ref _circuitOpenUntilTicks, DateTime.UtcNow.Add(CircuitCooldown).Ticks);
            _logger.LogWarning(
                "Mídia: {N} falhas seguidas ({Reason}); pausando as chamadas por {Secs}s. " +
                "Normalmente é o broker de mídia do Windows travado, não o DroidDeck — " +
                "confirme com um cliente WinRT qualquer antes de procurar bug aqui.",
                _consecutiveFailures, reason, CircuitCooldown.TotalSeconds);
            _consecutiveFailures = 0;
        }

        public Task<List<MediaSessionInfo>> GetAllSessionsAsync() =>
            RunAsync(async manager =>
            {
                // Timings por passo ficam em Debug: o NLog roda em minlevel=Info, então não
                // aparecem na operação normal, mas bastam pra achar qual chamada WinRT trava.
                var sw = Stopwatch.StartNew();
                var sessions = manager.GetSessions();
                _logger.LogDebug("[diag] GetSessions() -> {N} sessoes em {Ms}ms", sessions.Count, sw.ElapsedMilliseconds);

                var result = new List<MediaSessionInfo>();
                foreach (var session in sessions)
                {
                    var swS = Stopwatch.StartNew();
                    var info = await GetSessionInfoAsync(session).ConfigureAwait(false);
                    _logger.LogDebug("[diag] sessao '{Id}' total {Ms}ms", session.SourceAppUserModelId, swS.ElapsedMilliseconds);
                    if (info != null) result.Add(info);
                }
                return result;
            },
            new List<MediaSessionInfo>(),
            nameof(GetAllSessionsAsync),
            // Mais folgado: lê thumbnail de cada sessão.
            TimeSpan.FromSeconds(8));

        public Task<MediaSessionInfo?> GetSessionByIdAsync(string sessionId) =>
            RunAsync<MediaSessionInfo?>(async manager =>
            {
                var session = manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == sessionId);
                return session != null ? await GetSessionInfoAsync(session).ConfigureAwait(false) : null;
            },
            null,
            nameof(GetSessionByIdAsync));

        private async Task<MediaSessionInfo?> GetSessionInfoAsync(GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var mediaProperties = await session.TryGetMediaPropertiesAsync();
                _logger.LogDebug("[diag]   TryGetMediaPropertiesAsync {Ms}ms", sw.ElapsedMilliseconds);

                sw.Restart();
                var playbackInfo = session.GetPlaybackInfo();
                var timelineProperties = session.GetTimelineProperties();
                _logger.LogDebug("[diag]   GetPlaybackInfo+Timeline {Ms}ms", sw.ElapsedMilliseconds);

                string? thumbnailBase64 = null;
                if (mediaProperties.Thumbnail != null)
                {
                    var streamRef = mediaProperties.Thumbnail;
                    sw.Restart();
                    using var stream = await streamRef.OpenReadAsync();
                    _logger.LogDebug("[diag]   thumb OpenReadAsync {Ms}ms", sw.ElapsedMilliseconds);

                    sw.Restart();
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    var bytes = new byte[stream.Size];
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(bytes);
                    thumbnailBase64 = "data:image/png;base64," + Convert.ToBase64String(bytes);
                    _logger.LogDebug("[diag]   thumb LoadAsync+Read {Bytes}B em {Ms}ms", bytes.Length, sw.ElapsedMilliseconds);
                }

                return new MediaSessionInfo
                {
                    Id = session.SourceAppUserModelId,
                    Title = mediaProperties.Title,
                    Artist = mediaProperties.Artist,
                    AlbumTitle = mediaProperties.AlbumTitle,
                    ThumbnailBase64 = thumbnailBase64,
                    PlaybackStatus = playbackInfo.PlaybackStatus.ToString(),
                    CanPlayPause = playbackInfo.Controls.IsPlayPauseToggleEnabled,
                    CanGoNext = playbackInfo.Controls.IsNextEnabled,
                    CanGoPrevious = playbackInfo.Controls.IsPreviousEnabled,
                    Position = timelineProperties.Position.TotalSeconds,
                    Duration = timelineProperties.EndTime.TotalSeconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session info");
                return null;
            }
        }

        /// <summary>True se há mídia TOCANDO (sessão atual ou qualquer uma). Leve — sem thumbnail/props.</summary>
        public Task<bool> IsAnythingPlayingAsync() =>
            RunAsync(manager =>
            {
                var current = manager.GetCurrentSession();
                if (current != null &&
                    current.GetPlaybackInfo().PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return Task.FromResult(true);

                foreach (var s in manager.GetSessions())
                {
                    if (s.GetPlaybackInfo().PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        return Task.FromResult(true);
                }
                return Task.FromResult(false);
            },
            false,
            nameof(IsAnythingPlayingAsync),
            // Chamado a cada 3s pelo SystemMonitorService: não pode segurar o loop.
            TimeSpan.FromSeconds(3));

        public Task<bool> SendCommandAsync(string sessionId, string command) =>
            RunAsync(async manager =>
            {
                var session = manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == sessionId);
                if (session == null)
                {
                    _logger.LogWarning("Session not found: {sessionId}", sessionId);
                    return false;
                }

                switch (command.ToLower())
                {
                    case "play":
                        await session.TryPlayAsync();
                        break;
                    case "pause":
                        await session.TryPauseAsync();
                        break;
                    case "playpause":
                    case "toggle":
                        await session.TryTogglePlayPauseAsync();
                        break;
                    case "next":
                        await session.TrySkipNextAsync();
                        break;
                    case "previous":
                        await session.TrySkipPreviousAsync();
                        break;
                    case "stop":
                        await session.TryStopAsync();
                        break;
                    default:
                        _logger.LogWarning("Unknown command: {command}", command);
                        return false;
                }

                _logger.LogInformation("Sent command {command} to session {sessionId}", command, sessionId);
                return true;
            },
            false,
            nameof(SendCommandAsync));
    }

    public class MediaSessionInfo
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? AlbumTitle { get; set; }
        public string? ThumbnailBase64 { get; set; }
        public string? PlaybackStatus { get; set; }
        public bool CanPlayPause { get; set; }
        public bool CanGoNext { get; set; }
        public bool CanGoPrevious { get; set; }
        public double Position { get; set; }
        public double Duration { get; set; }
    }
}
