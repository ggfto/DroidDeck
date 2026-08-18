using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DroidDeck.Services.Tuya
{
    /// <summary>
    /// Primitivas do "device sharing" da Tuya (o mesmo esquema do tuya-device-sharing-sdk).
    /// Diferente da Cloud API classica: aqui a query e o corpo vao CIFRADOS em AES-GCM com um
    /// segredo derivado por requisicao, e a assinatura cobre os headers + o ciphertext.
    ///
    /// Cadeia por requisicao:
    ///   rid     = guid novo
    ///   hashKey = md5(rid + refreshToken)
    ///   secret  = hmacSha256(key: rid, msg: hashKey).hex[..16]   <- ATENCAO: a chave e o rid
    ///   encdata = b64(nonce) + b64(ciphertext||tag)
    ///   X-sign  = hmacSha256(key: hashKey, msg: headers + queryEnc + bodyEnc).hex
    /// </summary>
    internal static class TuyaCrypto
    {
        // Alfabeto do nonce no SDK de origem: o nonce sao 12 CARACTERES (bytes ASCII), nao 12
        // bytes aleatorios. Trocar isso quebra a decifragem do lado da Tuya.
        private const string NonceAlphabet = "ABCDEFGHJKMNPQRSTWXYZabcdefhijkmnprstwxyz2345678";

        private static readonly string[] SignedHeaders =
            { "X-appKey", "X-requestId", "X-sid", "X-time", "X-token" };

        public static string Md5Hex(string input)
        {
            var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static string HmacSha256Hex(string key, string message)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static string HashKey(string rid, string refreshToken) => Md5Hex(rid + refreshToken);

        /// <summary>
        /// Deriva o segredo AES da requisicao. O sid e sempre vazio no nosso fluxo, entao o
        /// bloco de mistura do sid do SDK original nao se aplica; mantido aqui so por paridade.
        /// </summary>
        public static string GenerateSecret(string rid, string sid, string hashKey)
        {
            var message = hashKey;
            if (!string.IsNullOrEmpty(sid))
            {
                const int mod = 16;
                var length = Math.Min(sid.Length, mod);
                var sb = new StringBuilder();
                for (var i = 0; i < length; i++) sb.Append(sid[sid[i] % mod]);
                message += "_" + sb;
            }

            // A chave do HMAC e o rid e a mensagem e o hashKey (invertido em relacao ao intuitivo).
            return HmacSha256Hex(rid, message)[..16];
        }

        public static string RandomNonce(int size = 12)
        {
            var sb = new StringBuilder(size);
            for (var i = 0; i < size; i++)
                sb.Append(NonceAlphabet[RandomNumberGenerator.GetInt32(NonceAlphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>
        /// Sai como b64(nonce) concatenado com b64(ciphertext||tag). Os dois base64 grudados
        /// decodificam como um bloco unico porque b64 de 12 bytes da 16 chars sem padding --
        /// e e exatamente disso que o Decrypt depende.
        /// </summary>
        public static string Encrypt(string plaintext, string secret)
        {
            var nonce = Encoding.UTF8.GetBytes(RandomNonce());
            var key = Encoding.UTF8.GetBytes(secret);
            var data = Encoding.UTF8.GetBytes(plaintext);

            var cipher = new byte[data.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(key, tag.Length))
                aes.Encrypt(nonce, data, cipher, tag);

            var combined = new byte[cipher.Length + tag.Length];
            Buffer.BlockCopy(cipher, 0, combined, 0, cipher.Length);
            Buffer.BlockCopy(tag, 0, combined, cipher.Length, tag.Length);

            return Convert.ToBase64String(nonce) + Convert.ToBase64String(combined);
        }

        public static string Decrypt(string encoded, string secret)
        {
            var raw = Convert.FromBase64String(encoded);
            var nonce = raw.AsSpan(0, 12).ToArray();
            var payload = raw.AsSpan(12).ToArray();

            var cipher = payload.AsSpan(0, payload.Length - 16).ToArray();
            var tag = payload.AsSpan(payload.Length - 16).ToArray();

            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(Encoding.UTF8.GetBytes(secret), tag.Length))
                aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }

        /// <summary>
        /// Headers na ordem fixa, "chave=valor" unidos por "||", pulando os vazios, seguidos do
        /// ciphertext da query e do corpo. Ordem e omissao importam: qualquer desvio da 401.
        /// </summary>
        public static string Sign(string hashKey, string queryEnc, string bodyEnc,
                                  IReadOnlyDictionary<string, string> headers)
        {
            var parts = new List<string>();
            foreach (var name in SignedHeaders)
            {
                if (headers.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
                    parts.Add($"{name}={value}");
            }

            var signStr = string.Join("||", parts);
            if (!string.IsNullOrEmpty(queryEnc)) signStr += queryEnc;
            if (!string.IsNullOrEmpty(bodyEnc)) signStr += bodyEnc;

            return HmacSha256Hex(hashKey, signStr);
        }
    }
}
