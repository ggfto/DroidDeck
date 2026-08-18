using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services.Tuya
{
    /// <summary>
    /// Pareamento por QR do device-sharing: o usuario pega o "User Code" no app
    /// (Eu -> Config -> Conta e seguranca -> Codigo de usuario) e escaneia um QR. Nenhuma
    /// conta de desenvolvedor, nenhum cloud project, nenhum Access ID/Secret.
    ///
    /// LIMITE CONHECIDO: o QR carrega o schema do registro de app, e so os apps Smart Life e
    /// Tuya Smart aceitam o registro publico usado por padrao. Apps de marca (Nova Digital,
    /// Positivo, RSmart...) respondem "please use the designated app to scan the code to login".
    /// Para esses, o usuario precisa reparear o aparelho no Smart Life -- compartilhar nao
    /// resolve, varios OEMs nem oferecem a opcao.
    /// </summary>
    public class TuyaAuth
    {
        /// <summary>Host fixo do pareamento; nao e o endpoint regional da conta.</summary>
        private const string AuthHost = "https://apigw.iotbing.com";

        private readonly HttpClient _http;
        private readonly ILogger _logger;

        public TuyaAuth(HttpClient http, ILogger logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>Pede um token de QR. O conteudo a codificar no QR e o QrPayload.</summary>
        public async Task<(string Token, string QrPayload)> RequestQrCodeAsync(
            string userCode, string clientId, string schema)
        {
            var url = $"{AuthHost}/v1.0/m/life/home-assistant/qrcode/tokens" +
                      $"?clientid={Uri.EscapeDataString(clientId)}" +
                      $"&usercode={Uri.EscapeDataString(userCode)}" +
                      $"&schema={Uri.EscapeDataString(schema)}";

            using var resp = await _http.PostAsync(url, null);
            var raw = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var ok) || !ok.GetBoolean())
            {
                var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : raw;
                throw new InvalidOperationException($"Tuya recusou o pedido de QR: {msg}");
            }

            var token = root.GetProperty("result").GetProperty("qrcode").GetString() ?? "";
            return (token, $"tuyaSmart--qrLogin?token={token}");
        }

        /// <summary>
        /// Consulta se o usuario ja escaneou. Devolve null enquanto nao houve scan -- o QR
        /// expira em 1-2 minutos, entao quem chama deve desistir e gerar outro.
        /// </summary>
        public async Task<TuyaToken?> TryCompleteLoginAsync(string qrToken, string userCode, string clientId)
        {
            var url = $"{AuthHost}/v1.0/m/life/home-assistant/qrcode/tokens/{Uri.EscapeDataString(qrToken)}" +
                      $"?clientid={Uri.EscapeDataString(clientId)}" +
                      $"&usercode={Uri.EscapeDataString(userCode)}";

            using var resp = await _http.GetAsync(url);
            var raw = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var ok) || !ok.GetBoolean())
                return null; // ainda nao escaneado (ou expirado)

            var result = root.GetProperty("result");
            return new TuyaToken
            {
                AccessToken = result.GetProperty("access_token").GetString() ?? "",
                RefreshToken = result.GetProperty("refresh_token").GetString() ?? "",
                Uid = result.GetProperty("uid").GetString() ?? "",
                TerminalId = result.GetProperty("terminal_id").GetString() ?? "",
                Endpoint = result.GetProperty("endpoint").GetString() ?? "",
                ExpiresInSeconds = result.TryGetProperty("expire_time", out var e) ? e.GetInt64() : 0,
                // O "t" do envelope e o instante do servidor; e dele que sai a validade.
                IssuedAt = root.TryGetProperty("t", out var tt)
                    ? tt.GetInt64()
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }
}
