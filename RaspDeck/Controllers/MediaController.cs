using DroidDeck.Services;
using DroidDeck.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace DroidDeck.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        private readonly MediaControlService _mediaService;
        private readonly ILogger<MediaController> _logger;
        private readonly IHubContext<DeckHub> _hubContext;

        public MediaController(
            MediaControlService mediaService,
            ILogger<MediaController> logger,
            IHubContext<DeckHub> hubContext)
        {
            _mediaService = mediaService;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetAllSessions()
        {
            _logger.LogInformation("GetAllSessions called");
            var sessions = await _mediaService.GetAllSessionsAsync();
            return Ok(sessions);
        }

        [HttpGet("sessions/{id}")]
        public async Task<IActionResult> GetSession(string id)
        {
            _logger.LogInformation("GetSession called for {id}", id);
            var session = await _mediaService.GetSessionByIdAsync(id);
            if (session != null)
                return Ok(session);
            return NotFound();
        }

        [HttpPost("sessions/{id}/command")]
        public async Task<IActionResult> SendCommand(string id, [FromBody] MediaCommandData command)
        {
            _logger.LogInformation("SendCommand called for {id}: {command}", id, command.Command);
            var result = await _mediaService.SendCommandAsync(id, command.Command);

            if (result.Success)
            {
                // O estado ja vem junto do comando. Buscar a sessao de novo aqui custava uma
                // terceira passagem pelo portao do WinRT (e a leitura da capa) so pra
                // preencher um campo que nenhum cliente le -- e, quando essa terceira
                // chamada perdia o portao, o endpoint ainda devolvia 400 para um comando que
                // ja tinha funcionado.
                await _hubContext.Clients.All.SendAsync("ReceiveMediaState", new
                {
                    sessionId = result.SessionId,
                    command = command.Command,
                    playing = result.Playing
                });

                // Corrige na hora o estado otimista dos botoes de play/pause do deck, em vez
                // de esperar ate 3s pelo proximo tique do SystemMonitorService.
                if (result.Playing.HasValue)
                    await _hubContext.Clients.All.SendAsync("ReceiveMediaStatus",
                        new { playing = result.Playing.Value });

                return Ok(new { sessionId = result.SessionId, playing = result.Playing });
            }

            return BadRequest();
        }
    }

    public class MediaCommandData
    {
        public string Command { get; set; } = string.Empty;
    }
}
