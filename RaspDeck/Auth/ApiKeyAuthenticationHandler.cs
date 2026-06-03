using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DroidDeck.Auth
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private const string ApiKeyHeaderName = "X-API-KEY";
        private const string ApiKeyQueryName = "access_token"; // usado pelo WebSocket do SignalR

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? provided = null;

            if (Request.Headers.TryGetValue(ApiKeyHeaderName, out var header) && header.Count > 0)
            {
                provided = header.ToString();
            }
            else if (Request.Query.TryGetValue(ApiKeyQueryName, out var query) && query.Count > 0)
            {
                // SignalR via WebSocket não envia headers customizados; usa ?access_token=
                provided = query.ToString();
            }
            else if (Request.Headers.TryGetValue("Authorization", out var auth) &&
                     auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                provided = auth.ToString()["Bearer ".Length..].Trim();
            }

            if (string.IsNullOrEmpty(provided))
                return Task.FromResult(AuthenticateResult.Fail("Missing API Key"));

            var expected = ApiKeyProvider.GetKey();
            if (!FixedTimeEquals(provided, expected))
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));

            var claims = new[] { new Claim(ClaimTypes.Name, "ApiKeyUser") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            // Comparação em tempo constante (evita timing attacks). Tamanhos diferentes -> false.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
        }
    }
}
