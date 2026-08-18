using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services.Tuya
{
    /// <summary>Erro devolvido pela propria Tuya (success=false), com o code dela.</summary>
    public class TuyaApiException : Exception
    {
        public int Code { get; }
        public TuyaApiException(int code, string message) : base($"Tuya {code}: {message}")
            => Code = code;
    }

    /// <summary>
    /// Pipeline de requisicao assinada + cifrada do device-sharing. Toda chamada de negocio
    /// passa por aqui; ninguem mais deve montar header ou cifrar nada.
    /// </summary>
    public class TuyaApiClient
    {
        private static readonly JsonSerializerOptions Compact = new()
        {
            // O SDK de origem serializa sem espacos (separators=(",", ":")). O JSON entra no
            // ciphertext, entao formatacao diferente nao quebra a assinatura -- mas mantemos
            // compacto por paridade e por economia de payload.
            WriteIndented = false
        };

        private readonly HttpClient _http;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private TuyaToken _token;
        private readonly string _clientId;

        public TuyaApiClient(HttpClient http, ILogger logger, TuyaToken token, string clientId)
        {
            _http = http;
            _logger = logger;
            _token = token;
            _clientId = clientId;
        }

        public TuyaToken Token => _token;

        /// <summary>Disparado quando o token e renovado, para o chamador persistir a sessao.</summary>
        public event Action<TuyaToken>? TokenRefreshed;

        public Task<JsonElement> GetAsync(string path, IDictionary<string, object?>? query = null)
            => RequestAsync(HttpMethod.Get, path, query, null);

        public Task<JsonElement> PostAsync(string path, IDictionary<string, object?>? query = null,
                                           IDictionary<string, object?>? body = null)
            => RequestAsync(HttpMethod.Post, path, query, body);

        private async Task<JsonElement> RequestAsync(
            HttpMethod method, string path,
            IDictionary<string, object?>? query, IDictionary<string, object?>? body,
            bool allowRefresh = true)
        {
            if (allowRefresh) await RefreshIfNeededAsync();

            var rid = Guid.NewGuid().ToString();
            const string sid = "";
            var hashKey = TuyaCrypto.HashKey(rid, _token.RefreshToken);
            var secret = TuyaCrypto.GenerateSecret(rid, sid, hashKey);

            var queryEnc = "";
            if (query is { Count: > 0 })
                queryEnc = TuyaCrypto.Encrypt(JsonSerializer.Serialize(query, Compact), secret);

            var bodyEnc = "";
            if (body is { Count: > 0 })
                bodyEnc = TuyaCrypto.Encrypt(JsonSerializer.Serialize(body, Compact), secret);

            var headers = new Dictionary<string, string>
            {
                ["X-appKey"] = _clientId,
                ["X-requestId"] = rid,
                ["X-sid"] = sid,
                ["X-time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            };
            if (!string.IsNullOrEmpty(_token.AccessToken))
                headers["X-token"] = _token.AccessToken;

            headers["X-sign"] = TuyaCrypto.Sign(hashKey, queryEnc, bodyEnc, headers);

            // O encdata vai percent-encoded na URL, mas a assinatura usa a forma CRUA.
            var url = _token.Endpoint + path;
            if (!string.IsNullOrEmpty(queryEnc))
                url += "?encdata=" + Uri.EscapeDataString(queryEnc);

            using var req = new HttpRequestMessage(method, url);
            foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);

            if (!string.IsNullOrEmpty(bodyEnc))
            {
                var payload = JsonSerializer.Serialize(new Dictionary<string, string> { ["encdata"] = bodyEnc });
                req.Content = new StringContent(payload, Encoding.UTF8);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            using var resp = await _http.SendAsync(req);
            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new TuyaApiException((int)resp.StatusCode, $"HTTP {(int)resp.StatusCode}: {raw}");

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var ok) || !ok.GetBoolean())
            {
                var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
                var msg = root.TryGetProperty("msg", out var m) ? m.GetString() ?? "" : "";
                throw new TuyaApiException(code, msg);
            }

            if (!root.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
                return default;

            // O result vem cifrado com o mesmo secret da requisicao. Nem sempre e JSON:
            // alguns endpoints devolvem string crua.
            var decrypted = TuyaCrypto.Decrypt(result.GetString() ?? "", secret);
            try
            {
                return JsonDocument.Parse(decrypted).RootElement.Clone();
            }
            catch (JsonException)
            {
                return JsonDocument.Parse(JsonSerializer.Serialize(decrypted)).RootElement.Clone();
            }
        }

        private async Task RefreshIfNeededAsync()
        {
            // 60s de folga, igual ao SDK de origem: evita corrida com um token no limite.
            if (_token.ExpiresAtMs - 60_000 > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                return;

            await _refreshLock.WaitAsync();
            try
            {
                if (_token.ExpiresAtMs - 60_000 > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    return;

                _logger.LogInformation("Tuya: renovando access token");

                // allowRefresh:false corta a recursao -- esta chamada ainda assina com o
                // refresh token atual, que e o que a Tuya espera aqui.
                var result = await RequestAsync(
                    HttpMethod.Get, "/v1.0/m/token/" + _token.RefreshToken, null, null, allowRefresh: false);

                var refreshed = new TuyaToken
                {
                    AccessToken = result.GetProperty("accessToken").GetString() ?? "",
                    RefreshToken = result.GetProperty("refreshToken").GetString() ?? "",
                    Uid = result.TryGetProperty("uid", out var uid) ? uid.GetString() ?? _token.Uid : _token.Uid,
                    ExpiresInSeconds = result.GetProperty("expireTime").GetInt64(),
                    IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    TerminalId = _token.TerminalId,
                    Endpoint = _token.Endpoint,
                };

                _token = refreshed;
                TokenRefreshed?.Invoke(refreshed);
            }
            finally
            {
                _refreshLock.Release();
            }
        }
    }
}
