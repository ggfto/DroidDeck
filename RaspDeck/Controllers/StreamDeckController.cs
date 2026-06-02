using System.Collections.Generic;
using System.Threading.Tasks;
using AnyDeck.Hubs;
using AnyDeck.Models;
using AnyDeck.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AnyDeck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StreamDeckController : ControllerBase
    {
        private readonly StreamDeckConfigService _configService;
        private readonly ActionExecutorService _executorService;
        private readonly ILogger<StreamDeckController> _logger;
        private readonly IHubContext<DeckHub> _hubContext;

        public StreamDeckController(
            StreamDeckConfigService configService,
            ActionExecutorService executorService,
            ILogger<StreamDeckController> logger,
            IHubContext<DeckHub> hubContext)
        {
            _configService = configService;
            _executorService = executorService;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpGet("profiles")]
        public ActionResult<List<DeckProfile>> GetProfiles()
        {
            return Ok(_configService.GetProfiles());
        }

        [HttpGet("profiles/{id}")]
        public ActionResult<DeckProfile> GetProfile(string id)
        {
            var profile = _configService.GetProfile(id);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        [HttpPost("profiles")]
        public async Task<ActionResult> SaveProfile([FromBody] DeckProfile profile)
        {
            if (profile == null) return BadRequest();
            _configService.SaveProfile(profile);
            // Notifica clientes (ex.: celular) que o deck mudou, para recarregarem na hora.
            await _hubContext.Clients.All.SendAsync("ReceiveDeckUpdate", new { profileId = profile.Id });
            return Ok();
        }

        [HttpDelete("profiles/{id}")]
        public async Task<ActionResult> DeleteProfile(string id)
        {
            _configService.DeleteProfile(id);
            await _hubContext.Clients.All.SendAsync("ReceiveDeckUpdate", new { profileId = id });
            return Ok();
        }

        [HttpPost("execute")]
        public async Task<ActionResult> ExecuteAction([FromBody] DeckAction action)
        {
            if (action == null) return BadRequest();
            await _executorService.ExecuteActionAsync(action);
            return Ok();
        }

        // ---- Grade física do deck (o celular envia quantos botões cabem na tela) ----
        [HttpGet("layout")]
        public ActionResult<DeviceLayout> GetLayout()
        {
            return Ok(_configService.GetLayout());
        }

        [HttpPost("layout")]
        public async Task<ActionResult> SaveLayout([FromBody] DeviceLayout layout)
        {
            if (layout == null || layout.Rows < 1 || layout.Columns < 1) return BadRequest();
            _configService.SaveLayout(layout);
            var saved = _configService.GetLayout();
            // Avisa o configurador web (e outros clientes) da nova grade, ao vivo.
            await _hubContext.Clients.All.SendAsync("ReceiveLayoutUpdate",
                new { rows = saved.Rows, columns = saved.Columns });
            return Ok(saved);
        }
    }
}
