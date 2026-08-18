using System.Collections.Generic;
using DroidDeck.Services.Tuya;
using Xunit;

namespace DroidDeck.Tests
{
    /// <summary>
    /// Trava o formato de fio do device-sharing da Tuya.
    ///
    /// Os vetores abaixo NÃO foram inventados: saíram do tuya-device-sharing-sdk (o SDK
    /// oficial em Python, que é a implementação de referência) rodando com entradas fixas.
    /// Se um refactor mudar a derivação do segredo, a ordem dos headers ou o empacotamento
    /// do AES-GCM, estes testes quebram aqui — em vez de virar um 401 opaco em produção,
    /// que foi exatamente o risco ao portar isto para C#.
    /// </summary>
    public class TuyaCryptoTests
    {
        private const string Rid = "3f2b1a4c-0000-4d5e-8f90-abcdef123456";
        private const string RefreshToken = "REFRESH_TOKEN_FIXO_PARA_TESTE";
        private const string ExpectedHashKey = "88dbef1587b3290076f84e81e415a0d8";
        private const string ExpectedSecret = "f7c97b12c2beb4f1";
        private const string Plaintext = "{\"homeId\":\"158770227\"}";

        /// <summary>Cifrado pelo SDK Python com o mesmo segredo — o C# tem que decifrar.</summary>
        private const string PythonCiphertext =
            "c01mTTRQaXp4OFN3aoNlUIF2Zz88FfnLQS5bmK90lpT1QFOrPio5rTl/dYcOtVK5yv4=";

        private const string ExpectedSign =
            "b415ce5df9e0cc41b6779c5e991cf4727372feb2f2b9fcc19b3964466f2d5ac7";

        private static Dictionary<string, string> Headers() => new()
        {
            ["X-appKey"] = "HA_test",
            ["X-requestId"] = Rid,
            ["X-sid"] = "",
            ["X-time"] = "1787000000000",
            ["X-token"] = "TOKEN_TESTE",
        };

        [Fact]
        public void HashKey_MatchesReferenceImplementation()
        {
            Assert.Equal(ExpectedHashKey, TuyaCrypto.HashKey(Rid, RefreshToken));
        }

        [Fact]
        public void GenerateSecret_MatchesReferenceImplementation()
        {
            // A chave do HMAC é o rid e a mensagem é o hashKey — invertido em relação ao
            // intuitivo. Trocar a ordem gera um segredo plausível que só falha na Tuya.
            Assert.Equal(ExpectedSecret, TuyaCrypto.GenerateSecret(Rid, "", ExpectedHashKey));
        }

        [Fact]
        public void GenerateSecret_IsSixteenChars()
        {
            // O segredo é a chave AES-128: 16 bytes. Qualquer outro tamanho estoura no AesGcm.
            Assert.Equal(16, TuyaCrypto.GenerateSecret(Rid, "", ExpectedHashKey).Length);
        }

        [Fact]
        public void Decrypt_ReadsCiphertextProducedByPythonSdk()
        {
            Assert.Equal(Plaintext, TuyaCrypto.Decrypt(PythonCiphertext, ExpectedSecret));
        }

        [Fact]
        public void EncryptDecrypt_RoundTrips()
        {
            var encrypted = TuyaCrypto.Encrypt(Plaintext, ExpectedSecret);

            // Nonce diferente a cada chamada: o mesmo texto nunca sai igual duas vezes.
            Assert.NotEqual(PythonCiphertext, encrypted);
            Assert.Equal(Plaintext, TuyaCrypto.Decrypt(encrypted, ExpectedSecret));
        }

        [Fact]
        public void Encrypt_PrefixIsSixteenCharBase64Nonce()
        {
            // O empacotamento depende de b64(nonce de 12 bytes) dar 16 chars sem padding:
            // é o que permite concatenar os dois base64 e decodificar como um bloco só.
            var encrypted = TuyaCrypto.Encrypt(Plaintext, ExpectedSecret);
            Assert.DoesNotContain("=", encrypted[..16]);
        }

        [Fact]
        public void Sign_MatchesReferenceImplementation()
        {
            var sign = TuyaCrypto.Sign(ExpectedHashKey, PythonCiphertext, "", Headers());
            Assert.Equal(ExpectedSign, sign);
        }

        [Fact]
        public void Sign_SkipsEmptyHeaders()
        {
            // X-sid é sempre vazio no nosso fluxo e precisa ficar FORA da string assinada.
            // Incluí-lo como "X-sid=" muda a assinatura e a Tuya recusa com 401.
            var headers = Headers();
            var withEmptySid = TuyaCrypto.Sign(ExpectedHashKey, PythonCiphertext, "", headers);

            headers.Remove("X-sid");
            var withoutSid = TuyaCrypto.Sign(ExpectedHashKey, PythonCiphertext, "", headers);

            Assert.Equal(withEmptySid, withoutSid);
        }

        [Fact]
        public void Sign_ChangesWhenBodyChanges()
        {
            var a = TuyaCrypto.Sign(ExpectedHashKey, "", "corpo-a", Headers());
            var b = TuyaCrypto.Sign(ExpectedHashKey, "", "corpo-b", Headers());
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Sign_ConcatenatesQueryThenBodyWithoutSeparator()
        {
            // Documenta uma ambiguidade do protocolo de origem, não uma escolha nossa:
            // query e corpo são apenas grudados na string assinada, sem separador. Logo
            // ("XYZ", "") e ("", "XYZ") produzem a MESMA assinatura. Fica registrado para
            // que ninguém "conserte" isso e quebre a compatibilidade com a Tuya.
            var queryOnly = TuyaCrypto.Sign(ExpectedHashKey, "XYZ", "", Headers());
            var bodyOnly = TuyaCrypto.Sign(ExpectedHashKey, "", "XYZ", Headers());
            var split = TuyaCrypto.Sign(ExpectedHashKey, "XY", "Z", Headers());

            Assert.Equal(queryOnly, bodyOnly);
            Assert.Equal(queryOnly, split);
        }
    }
}
