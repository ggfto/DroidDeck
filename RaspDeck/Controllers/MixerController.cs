using DroidDeck.Services;
using DroidDeck.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class MixerController : ControllerBase
    {
        private readonly MixerService _mixerService;
        private readonly Microsoft.Extensions.Logging.ILogger<MixerController> _logger;
        private readonly IHubContext<DeckHub> _hubContext;

        public MixerController(
            MixerService mixerService,
            Microsoft.Extensions.Logging.ILogger<MixerController> logger,
            IHubContext<DeckHub> hubContext)
        {
            _mixerService = mixerService;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpGet("out")]
        public IActionResult GetAllOutputs()
        {
            _logger.LogInformation("GetAllOutputs called");
            return Ok(_mixerService.FindAllOutputs());
        }

        [HttpGet("out/{id}")]
        public IActionResult GetOutput(string id)
        {
            var mixer = _mixerService.FindOne(id);
            if (mixer != null)
                return Ok(mixer);
            return BadRequest();
        }

        [HttpPut("out/{id}")]
        public async Task<IActionResult> SetOutput(string id, [FromBody] MixerData data)
        {
            _logger.LogInformation("SetOutput called for {id}", id);
            var device = new MixerMaster(id);
            if (device == null) return BadRequest();

            var result = device.SetOptions(id, data);

            // Broadcast volume change to all connected clients
            await _hubContext.Clients.All.SendAsync("ReceiveVolumeChange", new
            {
                deviceId = id,
                type = "output",
                data = result
            });

            return Ok(result);
        }

        [HttpGet("in")]
        public IActionResult GetAllInputs()
        {
            _logger.LogInformation("GetAllInputs called");
            return Ok(_mixerService.FindAllInputs());
        }

        [HttpGet("in/{id}")]
        public IActionResult GetInput(string id)
        {
            var device = _mixerService.FindOne(id);
            if (device == null)
                return BadRequest();
            return Ok(device);
        }

        [HttpPut("in/{id}")]
        public async Task<IActionResult> SetInput(string id, [FromBody] MixerData data)
        {
            _logger.LogInformation("SetInput called for {id}", id);
            var device = new MixerMaster(id);
            if (device == null) return BadRequest();

            var result = device.SetOptions(id, data);

            // Broadcast volume change to all connected clients
            await _hubContext.Clients.All.SendAsync("ReceiveVolumeChange", new
            {
                deviceId = id,
                type = "input",
                data = result
            });

            return Ok(result);
        }
    }
}
