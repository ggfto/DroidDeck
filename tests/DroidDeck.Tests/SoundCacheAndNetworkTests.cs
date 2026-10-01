using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using DroidDeck.Lib;
using DroidDeck.Services;
using Xunit;

namespace DroidDeck.Tests
{
    public class SoundCacheTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dd-cache-" + Guid.NewGuid().ToString("N"));

        public SoundCacheTests() => Directory.CreateDirectory(_dir);
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        [Fact]
        public void NomeDoCache_EEstavelEntreChamadas()
        {
            // O hash antigo (GetHashCode) mudava a cada processo; o SHA-256 nao muda nunca.
            var a = SoundboardService.CacheFileFor("", "https://www.myinstants.com/media/sounds/x.mp3");
            var b = SoundboardService.CacheFileFor("", "https://www.myinstants.com/media/sounds/x.mp3");

            Assert.Equal(a, b);
            Assert.Equal("u-", Path.GetFileName(a)[..2]);
        }

        [Fact]
        public void NomeDoCache_NaoUsaIdInseguro()
        {
            var path = SoundboardService.CacheFileFor("../../evil", "https://exemplo/som.mp3");

            Assert.DoesNotContain("..", Path.GetFileName(path));
            Assert.StartsWith("u-", Path.GetFileName(path));
        }

        [Fact]
        public void NomeDoCache_UsaIdSeguroDireto() =>
            Assert.Equal("vine-boom.mp3", Path.GetFileName(SoundboardService.CacheFileFor("vine-boom", "https://x/y.mp3")));

        private string Sound(string name, int kb, int minutesAgo)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, new byte[kb * 1024]);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
            return path;
        }

        [Fact]
        public void Limpeza_ApagaOsMaisAntigosAteCaberNoTeto()
        {
            var velho = Sound("velho.mp3", 100, 30);
            var medio = Sound("medio.mp3", 100, 20);
            var novo = Sound("novo.mp3", 100, 10);

            SoundboardService.PruneCache(_dir, maxBytes: 200 * 1024);

            Assert.False(File.Exists(velho));
            Assert.True(File.Exists(medio));
            Assert.True(File.Exists(novo));
        }

        [Fact]
        public void Limpeza_NuncaApagaOSomQueAcabouDeSerBaixado()
        {
            var antigoMasAtual = Sound("atual.mp3", 300, 60);
            Sound("outro.mp3", 100, 5);

            SoundboardService.PruneCache(_dir, maxBytes: 350 * 1024, keep: antigoMasAtual);

            Assert.True(File.Exists(antigoMasAtual));
        }
    }

    public class NetworkInfoTests
    {
        [Fact]
        public void IpParaLoopback_ELoopback() =>
            Assert.Equal("127.0.0.1", NetworkInfo.GetLocalIpFor(IPAddress.Loopback));

        [Fact]
        public void IpDaLan_SempreDevolveUmIPv4Valido()
        {
            Assert.True(IPAddress.TryParse(NetworkInfo.GetLanIp(), out var ip));
            Assert.Equal(AddressFamily.InterNetwork, ip!.AddressFamily);
        }

        [Fact]
        public void IpParaRemetenteDaLan_EUmIPv4Local()
        {
            // So consulta a tabela de rotas; nada e enviado.
            var local = NetworkInfo.GetLocalIpFor(IPAddress.Parse("192.168.0.123"));

            Assert.True(IPAddress.TryParse(local, out var ip));
            Assert.Equal(AddressFamily.InterNetwork, ip!.AddressFamily);
        }
    }
}
