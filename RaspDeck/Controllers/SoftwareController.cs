using Microsoft.AspNetCore.Mvc;
using AnyDeck.Software;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AnyDeck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class SoftwareController : ControllerBase
    {
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly Services.IAppActivator? _activator;
        private readonly Services.IAudioControlService? _audioControl;

        public SoftwareController(Microsoft.Extensions.Configuration.IConfiguration configuration, Services.IAppActivator? activator = null, Services.IAudioControlService? audioControl = null)
        {
            _configuration = configuration;
            _activator = activator;
            _audioControl = audioControl;
        }

        [HttpPost("activate")]
        public IActionResult Activate([FromBody] SoftwareData? data)
        {
            if (data == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var enabled = _configuration["EnableSoftwareActivation"];
            if (string.IsNullOrEmpty(enabled) || enabled.ToLowerInvariant() != "true")
            {
                return Forbid();
            }

            // perform activation via injected activator (if available)
            if (!string.IsNullOrEmpty(data.Name) && _activator != null)
            {
                _activator.ActivateWindow(data.Name);
            }

            if (!string.IsNullOrEmpty(data.Action) && _activator != null)
            {
                _activator.SendKeys(data.Action);
                return Ok();
            }

            return BadRequest();
        }

        [HttpPost("mute")]
        public IActionResult Mute([FromBody] AudioTarget? target)
        {
            if (target == null) return BadRequest();
            var processName = target.ProcessName;
            if (string.IsNullOrEmpty(processName)) return BadRequest("processName required");
            if (_audioControl == null) return StatusCode(501, "Audio control not available on this platform");

            var allowed = _configuration["AllowedTargets"];
            if (!string.IsNullOrEmpty(allowed))
            {
                var list = allowed.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                if (!System.Array.Exists(list, s => string.Equals(s, processName, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return Forbid();
                }
            }

            bool mute = target.Mute ?? true;

            var affected = _audioControl.MuteByProcessName(processName, mute);
            return Ok(new { process = processName, muted = mute, affected });
        }

        [HttpPost("toggle-mute")]
        public IActionResult ToggleMute([FromBody] AudioTarget? target)
        {
            if (target == null) return BadRequest();
            var processName = target.ProcessName;
            if (string.IsNullOrEmpty(processName)) return BadRequest("processName required");
            if (_audioControl == null) return StatusCode(501, "Audio control not available on this platform");

            var allowed = _configuration["AllowedTargets"];
            if (!string.IsNullOrEmpty(allowed))
            {
                var list = allowed.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                if (!System.Array.Exists(list, s => string.Equals(s, processName, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return Forbid();
                }
            }

            var newState = _audioControl.ToggleMuteByProcessName(processName);
            return Ok(new { process = processName, muted = newState });
        }
    }
}
