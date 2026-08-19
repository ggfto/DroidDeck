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
    /// Acesso ao WinRT de mídia (GlobalSystemMediaTransportControls). Regras deste arquivo:
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
    ///    LPC. Com o portão fica no máximo UMA operação em voo.
    ///
    /// 3. Chamada de USUÁRIO nunca é descartada em silêncio. O portão e o disjuntor existem
    ///    para o poller de 3s não martelar um broker travado — mas por muito tempo eles
    ///    também engoliam o toque no botão: se o poll estivesse em voo, o comando esperava
    ///    500ms, desistia e devolvia "falhou" sem nada acima de Debug no log. Era esse o
    ///    "os controles de mídia param de funcionar sem motivo aparente": bastava a leitura
    ///    de sessões (que baixa a thumbnail de cada sessão, fácil passar de 500ms) coincidir
    ///    com o toque. Agora Origin.Interactive espera o portão de verdade e ignora o
    ///    disjuntor; só o poller desiste rápido.
    ///
    /// Como todo acesso passa pelo portão, o manager é sempre usado de forma serializada — é
    /// isso que torna seguro cacheá-lo, sem reintroduzir o RPC_E_WRONG_THREAD.
    /// </summary>
    public class MediaControlService
    {
        /// <summary>De onde veio a chamada — define se ela pode ser descartada.</summary>
        private enum Origin
        {
            /// <summary>Poller de status: ninguém está esperando, desiste na hora.</summary>
            Background,

            /// <summary>Toque do usuário (botão do deck ou REST): espera o portão e tenta
            /// mesmo com o disjuntor aberto.</summary>
            Interactive
        }

        private readonly ILogger<MediaControlService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private GlobalSystemMediaTransportControlsSessionManager? _manager;

        /// <summary>Chamada de fundo: se já há operação em voo, desiste em vez de enfileirar.</summary>
        private static readonly TimeSpan BackgroundGateWait = TimeSpan.FromMilliseconds(500);

        /// <summary>Chamada do usuário: maior que a operação de fundo mais lenta
        /// (GetAllSessions, 8s), pra o comando ficar na fila em vez de ser descartado.</summary>
        private static readonly TimeSpan InteractiveGateWait = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        // Disjuntor. O broker de mídia do Windows (GSMTC) pode simplesmente parar de
        // responder para a máquina inteira — verificado em 2026-08-04 com um console de 20
        // linhas fora deste projeto: RequestAsync devolveu RPC_E_CALL_CANCELED (0x80010002)
        // após ~10s em MTA e nem retornou em STA. Sem disjuntor, cada request de mídia paga
        // o timeout inteiro e o poller de 3s fica martelando um serviço quebrado.
        //
        // O disjuntor cala o POLLER, não o usuário: um toque no botão sempre tenta. Fechar o
        // circuito para todo mundo deixava o deck morto por um minuto inteiro sem explicação,
        // e é o comando do usuário que costuma descobrir que o broker voltou.
        private int _consecutiveFailures;
        private long _circuitOpenUntilTicks;
        private const int FailuresToOpen = 3;
        private static readonly TimeSpan CircuitCooldown = TimeSpan.FromSeconds(60);

        public MediaControlService(ILogger<MediaControlService> logger)
        {
            _logger = logger;
        }

        /// <summary>True se a tarefa terminou dentro do prazo. Cancela o timer quando a
        /// tarefa ganha, pra não acumular um Task.Delay pendente por chamada.</summary>
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
        /// devolvendo o fallback em vez de propagar falha — os chamadores tratam ausência de
        /// mídia como estado normal.
        /// </summary>
        private async Task<T> RunAsync<T>(
            Func<GlobalSystemMediaTransportControlsSessionManager, Task<T>> op,
            T fallback,
            string what,
            Origin origin,
            TimeSpan? timeout = null)
        {
            var limit = timeout ?? DefaultTimeout;
            var interactive = origin == Origin.Interactive;

            // Disjuntor aberto: o poller responde na hora, sem tocar no WinRT. Comando do
            // usuário passa assim mesmo — se o broker voltou, é ele quem vai perceber.
            if (Volatile.Read(ref _circuitOpenUntilTicks) > DateTime.UtcNow.Ticks)
            {
                if (!interactive)
                {
                    _logger.LogDebug("Mídia: '{What}' curto-circuitado — subsistema marcado como indisponível.", what);
                    return fallback;
                }
                _logger.LogInformation("Mídia: '{What}' tentado mesmo com o disjuntor aberto (veio do usuário).", what);
            }

            var gateWait = interactive ? InteractiveGateWait : BackgroundGateWait;
            if (!await _gate.WaitAsync(gateWait).ConfigureAwait(false))
            {
                // Em Warning quando é o usuário: um botão que não faz nada precisa deixar
                // rastro no log, senão vira "parou de funcionar sem motivo".
                if (interactive)
                    _logger.LogWarning(
                        "Mídia: '{What}' descartado — o portão ficou ocupado por {Secs}s. " +
                        "O broker de mídia do Windows provavelmente está travado.",
                        what, gateWait.TotalSeconds);
                else
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
                    // O manager é PRESERVADO aqui de propósito. CompletesWithinAsync só
                    // desiste de esperar — a chamada WinRT continua viva (não dá pra cancelar
                    // RPC em voo). Zerar o campo fazia a próxima chamada ativar um segundo
                    // manager por COM e usá-lo EM PARALELO com a órfã, que é exatamente a
                    // concorrência que o portão existe pra impedir — e o jeito de trazer o
                    // RPC_E_WRONG_THREAD de volta. Timeout é sintoma de broker lento; proxy
                    // podre chega como exceção, e aí sim o manager é recriado.
                    _logger.LogWarning("Mídia: timeout ({Secs}s) em '{What}'; a chamada WinRT ficou pendurada.",
                        limit.TotalSeconds, what);
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
                "Mídia: {N} falhas seguidas ({Reason}); pausando o poller por {Secs}s " +
                "(comandos do usuário continuam passando). " +
                "Normalmente é o broker de mídia do Windows travado, não o DroidDeck — " +
                "confirme com um cliente WinRT qualquer antes de procurar bug aqui.",
                _consecutiveFailures, reason, CircuitCooldown.TotalSeconds);
            _consecutiveFailures = 0;
        }

        /// <param name="includeThumbnails">Ler a capa custa uma abertura de stream por sessão
        /// e é o que mais engorda a operação. Quem só quer id/estado passa false.</param>
        public Task<List<MediaSessionInfo>> GetAllSessionsAsync(bool includeThumbnails = true) =>
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
                    var info = await GetSessionInfoAsync(session, includeThumbnails).ConfigureAwait(false);
                    _logger.LogDebug("[diag] sessao '{Id}' total {Ms}ms", session.SourceAppUserModelId, swS.ElapsedMilliseconds);
                    if (info != null) result.Add(info);
                }
                return result;
            },
            new List<MediaSessionInfo>(),
            nameof(GetAllSessionsAsync),
            Origin.Interactive,
            // Mais folgado quando lê a thumbnail de cada sessão.
            includeThumbnails ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(4));

        public Task<MediaSessionInfo?> GetSessionByIdAsync(string sessionId) =>
            RunAsync<MediaSessionInfo?>(async manager =>
            {
                // Consulta por id é estrita de propósito: 404 quando o id não existe mais é a
                // resposta certa para um GET. O fallback "usa a sessão que está tocando" vale
                // só para COMANDO, onde não fazer nada é pior que agir na sessão ativa.
                var matches = manager.GetSessions()
                    .Where(x => x.SourceAppUserModelId == sessionId)
                    .ToList();
                var session = matches.FirstOrDefault(IsPlaying) ?? matches.FirstOrDefault();
                return session != null ? await GetSessionInfoAsync(session, true).ConfigureAwait(false) : null;
            },
            null,
            nameof(GetSessionByIdAsync),
            Origin.Interactive);

        /// <summary>
        /// Escolhe a sessão alvo. Na ordem:
        ///
        /// 1. Sessões cujo SourceAppUserModelId bate com o id pedido — e, entre elas, a que
        ///    está TOCANDO. O id não é único: duas janelas do Chrome, ou dois players do
        ///    mesmo app, compartilham o mesmo AUMID, e o FirstOrDefault antigo mandava o
        ///    comando pra qualquer uma — às vezes uma aba parada, enquanto a que tocava
        ///    ignorava o botão.
        /// 2. Se o id não existe mais (app fechado e reaberto, AUMID do navegador mudou) ou
        ///    veio vazio: a sessão corrente do sistema, depois qualquer uma tocando, depois a
        ///    primeira. É o comportamento das teclas de mídia do teclado — antes disso o botão
        ///    morria com "Session not found" até alguém reconfigurar.
        /// </summary>
        private GlobalSystemMediaTransportControlsSession? ResolveSession(
            GlobalSystemMediaTransportControlsSessionManager manager, string? sessionId)
        {
            var sessions = manager.GetSessions();

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var matches = sessions.Where(s => s.SourceAppUserModelId == sessionId).ToList();
                if (matches.Count > 0)
                    return matches.FirstOrDefault(IsPlaying) ?? matches[0];

                _logger.LogWarning(
                    "Mídia: sessão '{SessionId}' não existe mais; usando a sessão ativa do sistema.", sessionId);
            }

            return manager.GetCurrentSession()
                ?? sessions.FirstOrDefault(IsPlaying)
                ?? sessions.FirstOrDefault();
        }

        private static bool IsPlaying(GlobalSystemMediaTransportControlsSession s)
        {
            try
            {
                return s.GetPlaybackInfo().PlaybackStatus ==
                       GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            }
            catch
            {
                // A sessão pode morrer entre o GetSessions() e a leitura.
                return false;
            }
        }

        private async Task<MediaSessionInfo?> GetSessionInfoAsync(
            GlobalSystemMediaTransportControlsSession session, bool includeThumbnail)
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
                if (includeThumbnail && mediaProperties.Thumbnail != null)
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
                if (current != null && IsPlaying(current))
                    return Task.FromResult(true);

                foreach (var s in manager.GetSessions())
                {
                    if (IsPlaying(s)) return Task.FromResult(true);
                }
                return Task.FromResult(false);
            },
            false,
            nameof(IsAnythingPlayingAsync),
            Origin.Background,
            // Chamado a cada 3s pelo SystemMonitorService: não pode segurar o loop.
            TimeSpan.FromSeconds(3));

        /// <summary>
        /// Manda um comando de transporte. sessionId vazio = "a sessão que está tocando",
        /// igual às teclas de mídia do teclado.
        ///
        /// Resolver a sessão acontece DENTRO da mesma operação do portão. Antes, um botão sem
        /// sessionId fazia GetAllSessionsAsync (portão + até 8s + thumbnails) e só então
        /// SendCommandAsync (portão de novo): duas disputas pelo mesmo semáforo por toque, e
        /// perder qualquer uma delas fazia o botão não fazer nada, em silêncio.
        /// </summary>
        public Task<MediaCommandResult> SendCommandAsync(string? sessionId, string command) =>
            RunAsync(async manager =>
            {
                var session = ResolveSession(manager, sessionId);
                if (session == null)
                {
                    _logger.LogWarning("Mídia: comando '{Command}' ignorado — nenhuma sessão de mídia ativa.", command);
                    return new MediaCommandResult { Success = false };
                }

                var resolvedId = session.SourceAppUserModelId;

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
                        return new MediaCommandResult { Success = false, SessionId = resolvedId };
                }

                _logger.LogInformation("Sent command {command} to session {sessionId}", command, resolvedId);
                return new MediaCommandResult
                {
                    Success = true,
                    SessionId = resolvedId,
                    // Estado logo depois do comando, pra o cliente corrigir o palpite otimista
                    // sem esperar o próximo tique de 3s do poller.
                    Playing = IsPlaying(session)
                };
            },
            new MediaCommandResult { Success = false },
            nameof(SendCommandAsync),
            Origin.Interactive);
    }

    /// <summary>Resultado de um comando de transporte: qual sessão recebeu e como ela ficou.</summary>
    public class MediaCommandResult
    {
        public bool Success { get; set; }

        /// <summary>Sessão que de fato recebeu o comando — pode não ser a pedida, quando o id
        /// configurado no botão já não existe.</summary>
        public string? SessionId { get; set; }

        /// <summary>Estado de reprodução logo após o comando; null quando não deu pra ler.</summary>
        public bool? Playing { get; set; }
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
