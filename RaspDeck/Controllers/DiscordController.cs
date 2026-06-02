using System.Threading.Tasks;
using AnyDeck.Services;
using Microsoft.AspNetCore.Mvc;

namespace AnyDeck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscordController : ControllerBase
    {
        private readonly DiscordRpcService _discord;

        public DiscordController(DiscordRpcService discord)
        {
            _discord = discord;
        }

        public class CredsDto
        {
            public string? ClientId { get; set; }
            public string? ClientSecret { get; set; }
        }

        public class MuteDto { public bool? Mute { get; set; } }
        public class DeafDto { public bool? Deaf { get; set; } }

        [HttpPost("config")]
        public IActionResult Config([FromBody] CredsDto? dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.ClientId) || string.IsNullOrWhiteSpace(dto?.ClientSecret))
                return BadRequest("clientId e clientSecret obrigatórios");
            _discord.SetCredentials(dto.ClientId!, dto.ClientSecret!);
            return Ok();
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect()
        {
            try
            {
                await _discord.ConnectAsync();
                return Ok(new { connected = _discord.Connected, mute = _discord.SelfMute, deaf = _discord.SelfDeaf });
            }
            catch (System.Exception ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
        }

        [HttpGet("state")]
        public IActionResult State() =>
            Ok(new { connected = _discord.Connected, mute = _discord.SelfMute, deaf = _discord.SelfDeaf });

        [HttpPost("mute")]
        public async Task<IActionResult> Mute([FromBody] MuteDto? dto)
        {
            try
            {
                await _discord.SetMuteAsync(dto?.Mute);
                return Ok(new { mute = _discord.SelfMute });
            }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        [HttpPost("deafen")]
        public async Task<IActionResult> Deafen([FromBody] DeafDto? dto)
        {
            try
            {
                await _discord.SetDeafAsync(dto?.Deaf);
                return Ok(new { deaf = _discord.SelfDeaf });
            }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }
    }
}
