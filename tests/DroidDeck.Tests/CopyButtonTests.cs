using System;
using System.Collections.Generic;
using System.Linq;
using DroidDeck.Models;
using DroidDeck.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Result = DroidDeck.Services.StreamDeckConfigService.CopyButtonResult;

namespace DroidDeck.Tests
{
    public class CopyButtonTests
    {
        private readonly StreamDeckConfigService _config = new(NullLogger<StreamDeckConfigService>.Instance);
        private readonly string _a = "copy-a-" + Guid.NewGuid().ToString("N");
        private readonly string _b = "copy-b-" + Guid.NewGuid().ToString("N");

        public CopyButtonTests()
        {
            _config.SaveProfile(new DeckProfile
            {
                Id = _a,
                Name = "A",
                Buttons = new List<DeckButton>
                {
                    new()
                    {
                        Id = "atalho", Row = 0, Column = 0, Label = "Copiar",
                        Action = new DeckAction { Type = "hotkey", Parameters = new() { ["keys"] = "^c" } },
                    },
                },
            });
            _config.SaveProfile(new DeckProfile
            {
                Id = _b,
                Name = "B",
                Buttons = new List<DeckButton> { new() { Id = "outro", Row = 0, Column = 0, Label = "X" } },
            });
        }

        [Fact]
        public void Copiar_ParaOutroPerfil_IdNovoEOriginalFica()
        {
            Assert.Equal(Result.Ok, _config.CopyButton(_a, "atalho", _b, 0, 1, move: false));

            var copy = _config.GetProfile(_b)!.Buttons.Single(b => b.Row == 0 && b.Column == 1);
            Assert.NotEqual("atalho", copy.Id);
            Assert.Equal("Copiar", copy.Label);
            Assert.Equal("^c", copy.Action!.Parameters["keys"]);
            Assert.Single(_config.GetProfile(_a)!.Buttons);
        }

        [Fact]
        public void Mover_TiraDaOrigemEMantemOId()
        {
            Assert.Equal(Result.Ok, _config.CopyButton(_a, "atalho", _b, 1, 2, move: true));

            Assert.Empty(_config.GetProfile(_a)!.Buttons);
            var moved = _config.GetProfile(_b)!.Buttons.Single(b => b.Id == "atalho");
            Assert.Equal((1, 2), (moved.Row, moved.Column));
        }

        [Fact]
        public void Duplicar_NoMesmoPerfil()
        {
            Assert.Equal(Result.Ok, _config.CopyButton(_a, "atalho", _a, 0, 1, move: false));
            Assert.Equal(2, _config.GetProfile(_a)!.Buttons.Count);
            Assert.Equal(Result.SamePlace, _config.CopyButton(_a, "atalho", _a, 0, 2, move: true));
        }

        [Fact]
        public void CasaOcupadaOuBotaoInexistente_Recusa()
        {
            Assert.Equal(Result.Occupied, _config.CopyButton(_a, "atalho", _b, 0, 0, move: true));
            Assert.Single(_config.GetProfile(_a)!.Buttons);
            Assert.Equal(Result.NotFound, _config.CopyButton(_a, "nao-existe", _b, 0, 1, move: false));
            Assert.Equal(Result.NotFound, _config.CopyButton(_a, "atalho", "perfil-que-nao-existe", 0, 1, move: false));
        }
    }
}
