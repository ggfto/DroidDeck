using System.Threading.Tasks;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc;

namespace DroidDeck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SoundboardController : ControllerBase
    {
        private readonly SoundboardService _soundboard;

        public SoundboardController(SoundboardService soundboard)
        {
            _soundboard = soundboard;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            var results = await _soundboard.SearchAsync(q ?? "");
            return Ok(results);
        }

        [HttpGet("trending")]
        public async Task<IActionResult> Trending()
        {
            return Ok(await _soundboard.TrendingAsync());
        }

        public class PlayDto
        {
            public string? Id { get; set; }
            public string? Url { get; set; }
            public string? Title { get; set; }
        }

        [HttpPost("play")]
        public async Task<IActionResult> Play([FromBody] PlayDto? dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Url))
                return BadRequest(new { error = "url é obrigatório" });
            await _soundboard.PlayAsync(dto!.Id ?? "", dto.Url!, dto.Title);
            return Ok(_soundboard.GetStatePayload());
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            _soundboard.StopAll();
            return Ok(_soundboard.GetStatePayload());
        }

        [HttpGet("state")]
        public IActionResult State() => Ok(_soundboard.GetStatePayload());

        [HttpGet("devices")]
        public IActionResult Devices() => Ok(_soundboard.GetOutputDevices());

        public class ConfigDto
        {
            public string? CableDeviceId { get; set; }
            public string? MonitorDeviceId { get; set; }
            public bool? MonitorEnabled { get; set; }
            public int? Volume { get; set; }
        }

        [HttpGet("config")]
        public IActionResult GetConfig() => Ok(_soundboard.LoadConfig());

        [HttpPost("config")]
        public IActionResult SetConfig([FromBody] ConfigDto? dto)
        {
            var cfg = _soundboard.LoadConfig();
            if (dto != null)
            {
                cfg.CableDeviceId = dto.CableDeviceId;
                cfg.MonitorDeviceId = dto.MonitorDeviceId;
                if (dto.MonitorEnabled is bool m) cfg.MonitorEnabled = m;
                if (dto.Volume is int v) cfg.Volume = v;
            }
            _soundboard.SaveConfig(cfg);
            return Ok(cfg);
        }
    }
}
