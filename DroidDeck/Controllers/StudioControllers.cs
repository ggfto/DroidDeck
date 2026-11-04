using DroidDeck.Integrations;
using DroidDeck.Services;
using Microsoft.AspNetCore.Mvc;

namespace DroidDeck.Controllers
{
    /// <summary>Teclas atuais da superficie do Companion, para a tela que acabou de abrir.</summary>
    [Route("api/companion")]
    [ApiController]
    public class CompanionController : ControllerBase
    {
        private readonly CompanionIntegration _companion;

        public CompanionController(CompanionIntegration companion) => _companion = companion;

        [HttpGet("keys")]
        public ActionResult Keys() => Ok(_companion.Snapshot());
    }

    /// <summary>O que a tela de tally pode acompanhar agora (lista do editor).</summary>
    [Route("api/tally")]
    [ApiController]
    public class TallyController : ControllerBase
    {
        private readonly TallyService _tally;

        public TallyController(TallyService tally) => _tally = tally;

        [HttpGet("targets")]
        public ActionResult Targets() => Ok(_tally.Options());
    }
}
