using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DroidDeck.Actions;
using DroidDeck.Auth;
using DroidDeck.Lib;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DroidDeck.Tests
{
    public class LocalizationTests
    {
        private static T InEnglish<T>(System.Func<T> f)
        {
            L.English = true;
            try { return f(); }
            finally { L.English = false; }
        }

        [Fact]
        public void EmPortugues_DevolveAChave()
        {
            L.Language = "pt";
            Assert.Equal("Abrir programa", L.T("Abrir programa"));
        }

        [Fact]
        public void SemIdioma_SaiEmIngles() =>
            Assert.Equal("Open program", L.T("Abrir programa"));

        [Fact]
        public void ComIngles_DevolveATraducao() =>
            Assert.Equal("Open program", InEnglish(() => L.T("Abrir programa")));

        [Fact]
        public void SemTraducao_FicaOPortugues() =>
            Assert.Equal("Texto que ninguém traduziu", InEnglish(() => L.T("Texto que ninguém traduziu")));

        [Fact]
        public void Placeholders_SaoTrocadosDepoisDeTraduzir()
        {
            Assert.Equal(" Button: \"Live\".", InEnglish(() => L.T(" Botão: \"{label}\".", ("label", "Live"))));
            Assert.Equal(" Botão: \"Live\".", L.T(" Botão: \"{label}\".", ("label", "Live")));
        }

        [Fact]
        public void ComEspanhol_DevolveATraducao()
        {
            L.Language = "es";
            try { Assert.Equal("Atajo de teclado", L.T("Atalho de teclado")); }
            finally { L.Language = "pt"; }
        }

        [Fact]
        public void Valores_SaemNoFormatoDoIdioma()
        {
            Assert.Equal("1,5", new L.Text("{n}", ("n", 1.5)).In("pt"));
            Assert.Equal("1.5", new L.Text("{n}", ("n", 1.5)).In("en"));
            Assert.Equal("1,5", new L.Text("{n}", ("n", 1.5)).In("es"));
        }

        [Fact]
        public void Texto_GuardaOPortuguesETraduzNaHora()
        {
            var text = new L.Text("A Anthropic não respondeu ({code}).", ("code", 503));
            Assert.Equal("A Anthropic não respondeu (503).", text.Plain);
            Assert.Equal("Anthropic didn't respond (503).", text.In("en"));
            L.Language = "en";
            try { Assert.Equal(text.In("en"), L.Message(new LocalizedException(text))); }
            finally { L.Language = "pt"; }
            // Erro comum: traduz a mensagem inteira, como antes.
            Assert.Equal("OBS recusou o pedido.", L.Message(new System.Exception("OBS recusou o pedido.")));
        }

        [Fact]
        public void BotaoDeInformacao_ErroSaiNoIdiomaDeQuemRecebe()
        {
            var v = new DroidDeck.Services.InfoService.InfoValue { Text = "—", SubText = new L.Text("Sem permissão"), Error = true };
            L.Language = "pt";
            Assert.Equal("Sem permissão", v.Localized().Sub);
            Assert.Equal(L.T("Sem permissão"), v.Localized().Sub);
            L.Language = "en";
            try { Assert.Equal(L.T("Sem permissão"), v.Localized().Sub); Assert.NotEqual("Sem permissão", v.Localized().Sub); }
            finally { L.Language = "pt"; }
        }

        [Fact]
        public void Idiomas_VemDosArquivos()
        {
            Assert.Contains("pt", L.Languages);
            Assert.Contains("en", L.Languages);
            Assert.Contains("es", L.Languages);
            Assert.Contains("hi", L.Languages);
            Assert.Equal(L.Languages, DroidDeck.Hubs.DeckHub.Languages);
        }

        [Fact]
        public void IdiomaIncompleto_CaiNoIngles()
        {
            var en = L.StringsOf("en");
            var hi = L.StringsOf("hi");
            var untranslated = en.Keys.First(k => !hi.ContainsKey(k));
            Assert.Equal(en[untranslated], new L.Text(untranslated).In("hi"));
            Assert.Equal(hi["Salvar"], new L.Text("Salvar").In("hi"));
        }

        [Fact]
        public void Espanhol_TemTodaChaveDoIngles_ComOsMesmosPlaceholders()
        {
            var en = L.StringsOf("en");
            var es = L.StringsOf("es");
            Assert.Empty(en.Keys.Except(es.Keys));
            Assert.Empty(es.Keys.Except(en.Keys));
            var placeholder = new Regex(@"\{(\w+)\}");
            foreach (var language in L.Languages)
                foreach (var (pt, text) in L.StringsOf(language))
                {
                    var expected = placeholder.Matches(pt).Select(m => m.Value).OrderBy(x => x);
                    Assert.Equal(expected, placeholder.Matches(text).Select(m => m.Value).OrderBy(x => x));
                }
        }

        [Theory]
        [InlineData("es", "es")]
        [InlineData("es-MX,en;q=0.8", "es")]
        [InlineData("en-US,es;q=0.8", "en")]
        [InlineData("pt-BR,es;q=0.9", "pt")]
        [InlineData("hi-IN", "hi")]
        [InlineData("fr-FR,es;q=0.8", "es")]
        [InlineData("fr-FR", "en")]
        [InlineData(null, "en")]
        public void AcceptLanguage_EscolheOIdioma(string? header, string language) =>
            Assert.Equal(language, L.LanguageOf(header));

        [Theory]
        [InlineData("en", true)]
        [InlineData("en-US,pt;q=0.8", true)]
        [InlineData("EN-gb", true)]
        [InlineData("pt-BR", false)]
        [InlineData("pt-BR,en;q=0.9", false)]
        [InlineData("", true)]
        [InlineData(null, true)]
        public void AcceptLanguage_UsaAPrimeiraPreferencia(string? header, bool english) =>
            Assert.Equal(english, L.IsEnglish(header));
    }

    public class LocalizedSchemaEndpointTests : IClassFixture<WebApplicationFactory<DroidDeck.Program>>
    {
        // Nomes proprios: iguais nos dois idiomas.
        private static readonly HashSet<string> SameInEnglish = new()
        {
            "Discord", "OBS Studio", "Soundboard", "Windows", "Home Assistant", "Spotify", "Twitch", "YouTube", "Streaming",
            "Script", "Voicemeeter", "Elgato Key Light", "Philips Hue", "Govee", "MQTT", "Streamer.bot", "VTube Studio",
        };

        // Exemplos, unidades e termos tecnicos: ficam iguais em qualquer idioma.
        private static readonly HashSet<string> Technical = new()
        {
            "{\"brightness_pct\": 40}", "#FF8000", "%", "^C", "1 min", "1 min 30", "2 min", "3 min", "30 s", "192.168.0.50",
            "Authorization: Bearer …", @"C:\scripts\backup.ps1", @"C:\Windows\System32\notepad.exe", "dB", "DELETE", "GET",
            "JSON", "K", "light.turn_on", "min", " min", "ON", "PATCH", "Pause", "Play", "Playlist", "POST", "PowerShell", "PUT",
            "Python", "s", "Strip[0].A1", "URL",
            // Nomes das teclas (o que esta impresso no teclado).
            "Alt", "Backspace", "Ctrl", "Delete", "End", "Enter", "Esc", "Home", "Insert", "Page Down", "Page Up", "Shift", "Tab",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        };

        private readonly WebApplicationFactory<DroidDeck.Program> _factory;

        public LocalizedSchemaEndpointTests(WebApplicationFactory<DroidDeck.Program> factory) => _factory = factory;

        private async Task<List<ActionSchema>> Schema(string language)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", ApiKeyProvider.GetKey());
            client.DefaultRequestHeaders.Add("Accept-Language", language);
            return (await client.GetFromJsonAsync<List<ActionSchema>>("/api/actions/schema"))!;
        }

        [Fact]
        public async Task SchemaEmIngles_TraduzTitulosCategoriasEDescricoes()
        {
            var pt = await Schema("pt-BR");
            var en = (await Schema("en")).ToDictionary(s => s.Type);

            foreach (var s in pt)
            {
                var t = en[s.Type];
                if (!SameInEnglish.Contains(s.Title)) Assert.NotEqual(s.Title, t.Title);
                if (!SameInEnglish.Contains(s.Category)) Assert.NotEqual(s.Category, t.Category);
                if (s.Description != null) Assert.NotEqual(s.Description, t.Description);
                // Valores e chaves nunca mudam.
                Assert.Equal(s.Fields.Select(f => f.Key), t.Fields.Select(f => f.Key));
                Assert.Equal(s.Fields.Select(f => f.Default), t.Fields.Select(f => f.Default));
            }
        }

        [Fact]
        public async Task TodoTextoDoSchema_TemTraducao()
        {
            var texts = new List<string>();
            foreach (var s in await Schema("pt-BR"))
            {
                texts.AddRange(new[] { s.Title, s.Category, s.Description! });
                foreach (var f in s.Fields)
                {
                    texts.AddRange(new[] { f.Label, f.Help!, f.Hint!, f.Unit! });
                    texts.AddRange((f.Options ?? new()).Select(o => o.Label));
                }
            }
            var missing = texts
                .Where(t => !string.IsNullOrWhiteSpace(t) && !SameInEnglish.Contains(t!) && !Technical.Contains(t!) && !L.Has(t!, "en"))
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            Assert.True(missing.Count == 0, "Texto do schema sem tradução:\n" + string.Join("\n", missing));
        }

        [Fact]
        public async Task SchemaEmPortugues_NaoMuda()
        {
            var pt = await Schema("pt-BR");
            Assert.Contains(pt, s => s.Title == "Abrir programa");
        }
    }
}
