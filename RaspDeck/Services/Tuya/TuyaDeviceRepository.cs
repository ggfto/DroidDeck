using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services.Tuya
{
    /// <summary>
    /// Leitura/escrita de dispositivos na nuvem Tuya. Sem dependencia do resto do DroidDeck
    /// de proposito: e o que permite testar contra a conta real fora do app.
    ///
    /// Cuidado com cota: enumerar UM dispositivo custa 4 chamadas (lista + specifications +
    /// status/strategy + custom-type). O tier gratuito da 26k/mes, entao a lista e para ser
    /// cacheada em disco e reconstruida so a pedido do usuario -- nunca em laco.
    /// </summary>
    public class TuyaDeviceRepository
    {
        private readonly TuyaApiClient _api;
        private readonly ILogger _logger;

        public TuyaDeviceRepository(TuyaApiClient api, ILogger logger)
        {
            _api = api;
            _logger = logger;
        }

        public async Task<List<TuyaHome>> QueryHomesAsync()
        {
            var result = await _api.GetAsync("/v1.0/m/life/users/homes");
            var homes = new List<TuyaHome>();
            if (result.ValueKind != JsonValueKind.Array) return homes;

            foreach (var h in result.EnumerateArray())
            {
                homes.Add(new TuyaHome
                {
                    Id = Str(h, "ownerId"),
                    Name = Str(h, "name"),
                });
            }
            return homes;
        }

        public async Task<List<TuyaDevice>> QueryDevicesAsync(string homeId)
        {
            var result = await _api.GetAsync("/v1.0/m/life/ha/home/devices",
                new Dictionary<string, object?> { ["homeId"] = homeId });

            var devices = new List<TuyaDevice>();
            if (result.ValueKind != JsonValueKind.Array) return devices;

            foreach (var item in result.EnumerateArray())
            {
                var device = new TuyaDevice
                {
                    Id = Str(item, "id"),
                    Name = Str(item, "name"),
                    Category = Str(item, "category"),
                    ProductName = Str(item, "product_name"),
                    Icon = Str(item, "icon"),
                    Online = item.TryGetProperty("online", out var on) && on.ValueKind == JsonValueKind.True,
                };

                // status vem como lista [{code,value}]; achatamos para um mapa code->valor.
                if (item.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in status.EnumerateArray())
                    {
                        if (s.TryGetProperty("code", out var c) && s.TryGetProperty("value", out var v))
                            device.Status[c.GetString() ?? ""] = ToClr(v);
                    }
                }

                // Falhas aqui sao degradacao, nao erro fatal: sem specification o botao ainda
                // dispara, so perde os campos guiados no editor.
                try { await LoadSpecificationsAsync(device); }
                catch (Exception ex) { _logger.LogWarning("Tuya: specifications de {Id} falhou: {Msg}", device.Id, ex.Message); }

                try { await LoadStrategyAsync(device); }
                catch (Exception ex) { _logger.LogWarning("Tuya: strategy de {Id} falhou: {Msg}", device.Id, ex.Message); }

                devices.Add(device);
            }
            return devices;
        }

        /// <summary>Funcoes comandaveis + faixas de valor. E o que alimenta o editor de botao.</summary>
        private async Task LoadSpecificationsAsync(TuyaDevice device)
        {
            var spec = await _api.GetAsync($"/v1.1/m/life/{device.Id}/specifications");
            if (spec.ValueKind != JsonValueKind.Object) return;

            if (spec.TryGetProperty("functions", out var functions) && functions.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in functions.EnumerateArray())
                {
                    var code = Str(f, "code");
                    if (string.IsNullOrEmpty(code)) continue;
                    device.Functions[code] = new TuyaFunction
                    {
                        Type = Str(f, "type"),
                        Values = Str(f, "values"),
                    };
                }
            }
        }

        /// <summary>
        /// Monta o mapa dpId -> code. Sem isso os relatos do MQTT de aparelho com suporte local
        /// chegam como dpId numerico e nao ha como saber que 20 significa switch_1.
        /// </summary>
        private async Task LoadStrategyAsync(TuyaDevice device)
        {
            var result = await _api.GetAsync($"/v1.0/m/life/devices/{device.Id}/status");
            if (result.ValueKind != JsonValueKind.Object) return;

            var supportLocal = true;
            if (result.TryGetProperty("dpStatusRelationDTOS", out var rels) && rels.ValueKind == JsonValueKind.Array)
            {
                foreach (var rel in rels.EnumerateArray())
                {
                    if (!(rel.TryGetProperty("supportLocal", out var sl) && sl.ValueKind == JsonValueKind.True))
                    {
                        // Um unico DP sem suporte local rebaixa o aparelho inteiro: e assim que
                        // o SDK de origem decide, e e o que define o topico MQTT la na frente.
                        supportLocal = false;
                        break;
                    }

                    var dpId = rel.TryGetProperty("dpId", out var d) ? d.ToString() : null;
                    var statusCode = Str(rel, "statusCode");
                    if (!string.IsNullOrEmpty(dpId) && !string.IsNullOrEmpty(statusCode))
                        device.DpIdToCode[dpId] = statusCode;
                }
            }

            if (supportLocal)
            {
                // custom-type ligado significa relato por code, nao por dpId.
                try
                {
                    var custom = await _api.GetAsync($"/v1.0/m/life/ha/{device.Id}/code/custom-type");
                    if (custom.ValueKind == JsonValueKind.True) supportLocal = false;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Tuya: custom-type de {Id}: {Msg}", device.Id, ex.Message);
                }
            }

            device.SupportLocal = supportLocal;
            if (!supportLocal) device.DpIdToCode.Clear();
        }

        public Task SendCommandAsync(string deviceId, string code, object? value)
        {
            var command = new Dictionary<string, object?> { ["code"] = code, ["value"] = value };
            return SendCommandsAsync(deviceId, new List<Dictionary<string, object?>> { command });
        }

        public async Task SendCommandsAsync(string deviceId, IReadOnlyList<Dictionary<string, object?>> commands)
        {
            await _api.PostAsync($"/v1.1/m/thing/{deviceId}/commands", null,
                new Dictionary<string, object?> { ["commands"] = commands });
        }

        private static string Str(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var v)) return "";
            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        }

        internal static object? ToClr(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.TryGetInt64(out var l) ? l : v.GetDouble(),
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => v.GetRawText(),
        };
    }
}
