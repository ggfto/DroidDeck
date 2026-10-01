using System.Collections.Generic;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using DroidDeck.Models;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Controllers
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

            if (!IsLocalRequest)
            {
                var violation = ActionPolicy.FindRemoteViolation(profile, _configService.GetProfile(profile.Id));
                if (violation != null)
                {
                    _logger.LogWarning("SaveProfile remoto recusado: botao {Id} cria/altera acao privilegiada ({Type})",
                        violation.Id, violation.Action?.Type);
                    return PrivilegedOnlyOnPc(violation.Label);
                }
            }

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

        /// <summary>
        /// Aperta um botao salvo: o servidor le a acao do perfil, entao o cliente nao escolhe
        /// o que roda. E o caminho do celular para qualquer tipo de acao.
        /// </summary>
        [HttpPost("press")]
        public async Task<ActionResult> PressButton([FromBody] PressRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ProfileId) || string.IsNullOrEmpty(request.ButtonId))
                return BadRequest();

            var button = _configService.GetProfile(request.ProfileId)?.Buttons?
                .Find(b => b.Id == request.ButtonId);
            if (button == null) return NotFound();

            if (button.Action != null)
                await _executorService.ExecuteActionAsync(button.Action);
            return Ok();
        }

        /// <summary>
        /// Executa uma acao avulsa (API/automacoes). De fora do PC, so tipos que nao executam
        /// codigo; os privilegiados exigem /press de um botao salvo pelo configurador.
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult> ExecuteAction([FromBody] DeckAction action)
        {
            if (action == null) return BadRequest();
            if (!IsLocalRequest && ActionPolicy.IsPrivileged(action))
            {
                _logger.LogWarning("Execute remoto recusado: acao privilegiada {Type}", action.Type);
                return PrivilegedOnlyOnPc(null);
            }
            await _executorService.ExecuteActionAsync(action);
            return Ok();
        }

        private bool IsLocalRequest => ActionPolicy.IsLocal(HttpContext.Connection.RemoteIpAddress);

        private ObjectResult PrivilegedOnlyOnPc(string? label) => StatusCode(403, new
        {
            error = "Botões que abrem programas, enviam atalhos ou ativam janelas só podem ser criados ou alterados no configurador do PC (http://localhost:4787)."
                + (string.IsNullOrWhiteSpace(label) ? "" : $" Botão: \"{label}\".")
        });

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

    public class PressRequest
    {
        public string ProfileId { get; set; } = "";
        public string ButtonId { get; set; } = "";
    }
}
