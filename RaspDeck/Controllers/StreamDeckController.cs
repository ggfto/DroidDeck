using System.Collections.Generic;
using System.Threading.Tasks;
using AnyDeck.Models;
using AnyDeck.Services;
using Microsoft.AspNetCore.Mvc;
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

        public StreamDeckController(
            StreamDeckConfigService configService,
            ActionExecutorService executorService,
            ILogger<StreamDeckController> logger)
        {
            _configService = configService;
            _executorService = executorService;
            _logger = logger;
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
        public ActionResult SaveProfile([FromBody] DeckProfile profile)
        {
            if (profile == null) return BadRequest();
            _configService.SaveProfile(profile);
            return Ok();
        }

        [HttpDelete("profiles/{id}")]
        public ActionResult DeleteProfile(string id)
        {
            _configService.DeleteProfile(id);
            return Ok();
        }

        [HttpPost("execute")]
        public async Task<ActionResult> ExecuteAction([FromBody] DeckAction action)
        {
            if (action == null) return BadRequest();
            await _executorService.ExecuteActionAsync(action);
            return Ok();
        }
    }
}
