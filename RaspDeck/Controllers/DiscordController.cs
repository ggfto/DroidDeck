using System.Threading.Tasks;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc;

namespace DroidDeck.Controllers
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
        public IActionResult State() => Ok(_discord.GetStatePayload());

        [HttpGet("guilds")]
        public async Task<IActionResult> Guilds()
        {
            try { return Ok(await _discord.GetGuildsAsync()); }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        [HttpGet("channels/{guildId}")]
        public async Task<IActionResult> Channels(string guildId)
        {
            try { return Ok(await _discord.GetChannelsAsync(guildId)); }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        [HttpGet("voice-channel")]
        public async Task<IActionResult> VoiceChannel()
        {
            try { await _discord.RefreshVoiceChannelAsync(); return Ok(_discord.GetStatePayload()); }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

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

        // ---- Soundboard nativa do Discord ----
        [HttpGet("soundboard-sounds")]
        public async Task<IActionResult> SoundboardSounds()
        {
            try { return Ok(await _discord.GetSoundboardSoundsAsync()); }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        public class PlaySoundDto
        {
            public string? SoundId { get; set; }
            public string? GuildId { get; set; }
        }

        [HttpPost("play-soundboard")]
        public async Task<IActionResult> PlaySoundboard([FromBody] PlaySoundDto? dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.SoundId))
                return BadRequest(new { error = "soundId é obrigatório" });
            try
            {
                await _discord.PlaySoundboardSoundAsync(dto!.SoundId!, dto.GuildId);
                return Ok();
            }
            catch (System.Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }
    }
}
