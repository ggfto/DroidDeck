using System.Threading.Tasks;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc;

namespace DroidDeck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ObsController : ControllerBase
    {
        private readonly ObsService _obs;

        public ObsController(ObsService obs)
        {
            _obs = obs;
        }

        public class ObsConfigDto
        {
            public string? Host { get; set; }
            public int? Port { get; set; }
            public string? Password { get; set; }
        }

        [HttpPost("config")]
        public IActionResult Config([FromBody] ObsConfigDto? dto)
        {
            var cfg = _obs.LoadConfig();
            if (!string.IsNullOrWhiteSpace(dto?.Host)) cfg.Host = dto!.Host!;
            if (dto?.Port is int p && p > 0) cfg.Port = p;
            cfg.Password = dto?.Password; // pode ser vazio/null (OBS sem senha)
            _obs.SaveConfig(cfg);
            return Ok();
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect()
        {
            try { await _obs.ConnectAsync(); return Ok(_obs.GetStatePayload()); }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        [HttpGet("state")]
        public IActionResult State() => Ok(_obs.GetStatePayload());

        [HttpGet("scenes")]
        public async Task<IActionResult> Scenes()
        {
            try { return Ok(await _obs.GetScenesAsync()); }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        [HttpGet("audio-inputs")]
        public async Task<IActionResult> AudioInputs()
        {
            try { return Ok(await _obs.GetAudioInputsAsync()); }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }
    }
}
