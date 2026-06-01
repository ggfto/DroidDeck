using AnyDeck.Services;
using AnyDeck.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AnyDeck.Controllers
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
            var success = await _mediaService.SendCommandAsync(id, command.Command);

            if (success)
            {
                // Fetch updated session state after command
                var updatedSession = await _mediaService.GetSessionByIdAsync(id);

                // Broadcast media state change to all connected clients
                await _hubContext.Clients.All.SendAsync("ReceiveMediaState", new
                {
                    sessionId = id,
                    command = command.Command,
                    session = updatedSession
                });

                return Ok();
            }

            return BadRequest();
        }
    }

    public class MediaCommandData
    {
        public string Command { get; set; } = string.Empty;
    }
}
