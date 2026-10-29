using System;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Actions;
using DroidDeck.Hubs;
using DroidDeck.Models;
using DroidDeck.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DroidDeck.Tests
{
    /// <summary>
    /// O app inverte o botao na hora do toque. Se a acao falha no PC, o executor reenvia o
    /// estado real para o botao voltar ao que e verdade.
    /// </summary>
    public class ActionExecutorStateTests
    {
        private class Stateful : IStatefulActionProvider
        {
            public bool Fail;
            public string Type => "fake";
            public string StateEvent => "ReceiveFakeState";
            public object GetState() => new { on = false };
            public ActionSchema Describe() => new() { Type = Type, Title = "fake" };
            public Task ExecuteAsync(ActionContext c) =>
                Fail ? throw new InvalidOperationException("desconectado") : Task.CompletedTask;
        }

        private static (ActionExecutorService executor, Mock<IClientProxy> all) Make(Stateful provider)
        {
            var all = new Mock<IClientProxy>();
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.All).Returns(all.Object);
            var hub = new Mock<IHubContext<DeckHub>>();
            hub.Setup(h => h.Clients).Returns(clients.Object);
            var executor = new ActionExecutorService(NullLogger<ActionExecutorService>.Instance,
                new IActionProvider[] { provider }, hub.Object);
            return (executor, all);
        }

        [Fact]
        public async Task Falha_reenvia_o_estado_do_provedor()
        {
            var (executor, all) = Make(new Stateful { Fail = true });

            await executor.ExecuteActionAsync(new DeckAction { Type = "fake" }, "b1");

            all.Verify(c => c.SendCoreAsync("ReceiveFakeState", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Sucesso_nao_reenvia_nada()
        {
            var (executor, all) = Make(new Stateful());

            await executor.ExecuteActionAsync(new DeckAction { Type = "fake" }, "b1");

            all.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
