using System;
using System.IO;
using DroidDeck.Lib;
using Xunit;

namespace DroidDeck.Tests
{
    public class SecretFileTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dd-secret-" + Guid.NewGuid().ToString("N"));
        private string PathFor(string name) => Path.Combine(_dir, name);

        public SecretFileTests() => Directory.CreateDirectory(_dir);
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        [Fact]
        public void RoundTrip_DevolveOTextoOriginal()
        {
            var path = PathFor("discord.json");
            const string json = "{\"ClientSecret\":\"s3cr3t\"}";

            SecretFile.WriteAllText(path, json);

            Assert.Equal(json, SecretFile.ReadAllText(path));
        }

        [Fact]
        public void Gravacao_NaoDeixaOSegredoEmTextoPuroNoDisco()
        {
            var path = PathFor("obs.json");

            SecretFile.WriteAllText(path, "{\"Password\":\"hunter2\"}");

            var onDisk = File.ReadAllText(path);
            Assert.StartsWith("DDSECRET1", onDisk);
            Assert.DoesNotContain("hunter2", onDisk);
            Assert.False(File.Exists(path + ".tmp"));
        }

        [Fact]
        public void ArquivoLegadoEmTextoPuro_ELidoEMigradoParaCifrado()
        {
            var path = PathFor("apikey");
            File.WriteAllText(path, "chave-antiga");

            Assert.Equal("chave-antiga", SecretFile.ReadAllText(path));

            var onDisk = File.ReadAllText(path);
            Assert.StartsWith("DDSECRET1", onDisk);
            Assert.DoesNotContain("chave-antiga", onDisk);
            Assert.Equal("chave-antiga", SecretFile.ReadAllText(path));
        }

        [Fact]
        public void Regravacao_SubstituiOArquivoExistente()
        {
            var path = PathFor("tuya.json");

            SecretFile.WriteAllText(path, "v1");
            SecretFile.WriteAllText(path, "v2");

            Assert.Equal("v2", SecretFile.ReadAllText(path));
        }
    }
}
