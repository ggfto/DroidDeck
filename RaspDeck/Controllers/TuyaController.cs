using System;
using System.Threading.Tasks;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc;

namespace DroidDeck.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TuyaController : ControllerBase
    {
        private readonly TuyaService _tuya;

        public TuyaController(TuyaService tuya)
        {
            _tuya = tuya;
        }

        public class PairDto
        {
            /// <summary>Codigo obtido no app: Eu -> Config -> Conta e seguranca -> Codigo de usuario.</summary>
            public string? UserCode { get; set; }
        }

        public class CommandDto
        {
            public string? DeviceId { get; set; }
            public string? Code { get; set; }
            public object? Value { get; set; }
        }

        /// <summary>Passo 1 do pareamento: devolve o conteudo do QR para o app renderizar.</summary>
        [HttpPost("pair/start")]
        public async Task<IActionResult> StartPairing([FromBody] PairDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.UserCode))
                return BadRequest(new { error = "userCode obrigatorio" });

            try { return Ok(await _tuya.StartPairingAsync(dto.UserCode!.Trim())); }
            catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        /// <summary>
        /// Passo 2: o configurador chama em laco. scanned=false enquanto o usuario nao escaneou
        /// (nao e erro); 410 quando o QR expirou e outro precisa ser gerado.
        /// </summary>
        [HttpPost("pair/poll")]
        public async Task<IActionResult> PollPairing()
        {
            try { return Ok(new { scanned = await _tuya.PollPairingAsync() }); }
            catch (InvalidOperationException ex) { return StatusCode(410, new { error = ex.Message }); }
            catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        [HttpGet("state")]
        public IActionResult State() => Ok(_tuya.GetStatePayload());

        [HttpPost("connect")]
        public async Task<IActionResult> Connect()
        {
            try { await _tuya.ConnectAsync(); return Ok(_tuya.GetStatePayload()); }
            catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        /// <summary>Reenumera na nuvem. Caro em cota: so a pedido explicito do usuario.</summary>
        [HttpPost("devices/refresh")]
        public async Task<IActionResult> RefreshDevices()
        {
            try { await _tuya.RefreshDevicesAsync(); return Ok(_tuya.GetStatePayload()); }
            catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }

        /// <summary>Usado pelo botao "testar" do editor.</summary>
        [HttpPost("command")]
        public async Task<IActionResult> Command([FromBody] CommandDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.DeviceId) || string.IsNullOrWhiteSpace(dto?.Code))
                return BadRequest(new { error = "deviceId e code obrigatorios" });

            try
            {
                await _tuya.SendCommandAsync(dto.DeviceId!, dto.Code!, dto.Value);
                return Ok();
            }
            catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
        }
    }
}
