using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DroidDeck.Services.Tuya
{
    /// <summary>Sessao de uma conta Tuya, persistida em %LocalAppData%\DroidDeck\tuya.json.</summary>
    public class TuyaConfig
    {
        public string? UserCode { get; set; }
        public TuyaToken? Token { get; set; }

        /// <summary>
        /// Registro de app usado no login por QR. Fica em configuracao (e nao hardcoded) porque
        /// e o unico ponto que muda quando/se a Tuya aprovar um registro proprio do DroidDeck.
        /// O default aponta para o registro publico do Home Assistant, que so e aceito pelos
        /// apps Smart Life e Tuya Smart -- apps de marca (Nova Digital etc) recusam o scan.
        /// </summary>
        public string ClientId { get; set; } = "HA_3y9q4ak7g4ephrvke";
        public string Schema { get; set; } = "haauthorize";

        public bool HasSession => Token != null && !string.IsNullOrEmpty(Token.AccessToken);
    }

    public class TuyaToken
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
        [JsonPropertyName("uid")] public string Uid { get; set; } = "";
        [JsonPropertyName("terminal_id")] public string TerminalId { get; set; } = "";
        [JsonPropertyName("endpoint")] public string Endpoint { get; set; } = "";

        /// <summary>Instante (epoch ms) em que o token foi emitido.</summary>
        [JsonPropertyName("t")] public long IssuedAt { get; set; }

        /// <summary>Validade em SEGUNDOS a partir de IssuedAt (a API devolve assim).</summary>
        [JsonPropertyName("expire_time")] public long ExpiresInSeconds { get; set; }

        [JsonIgnore] public long ExpiresAtMs => IssuedAt + ExpiresInSeconds * 1000;
    }

    public class TuyaHome
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class TuyaDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string? ProductName { get; set; }
        public bool Online { get; set; }
        public string? Icon { get; set; }

        /// <summary>Estado atual por code de DP (ex.: switch_led -> true).</summary>
        public Dictionary<string, object?> Status { get; set; } = new();

        /// <summary>Funcoes comandaveis; alimenta os campos em cascata do editor de botao.</summary>
        public Dictionary<string, TuyaFunction> Functions { get; set; } = new();

        /// <summary>
        /// Mapa dpId -> code usado para traduzir os relatos do MQTT. Dispositivos com
        /// support_local reportam por dpId numerico; sem ele, o push vem inutilizavel.
        /// </summary>
        public Dictionary<string, string> DpIdToCode { get; set; } = new();

        public bool SupportLocal { get; set; }
    }

    public class TuyaFunction
    {
        /// <summary>Boolean | Integer | Enum | Json | String | Raw</summary>
        public string Type { get; set; } = "";

        /// <summary>JSON cru dos limites: {"min":10,"max":1000,...} ou {"range":[...]}.</summary>
        public string? Values { get; set; }
    }
}
