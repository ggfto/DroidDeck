using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using DroidDeck.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    public class ActionExecutorService
    {
        private readonly ILogger<ActionExecutorService> _logger;
        private readonly IAppActivator _appActivator;
        private readonly MediaControlService _mediaService;
        private readonly MixerService _mixerService;
        private readonly IAudioControlService _audioControl;
        private readonly IHubContext<DeckHub> _hubContext;
        private readonly DiscordRpcService _discord;
        private readonly ObsService _obs;
        private readonly SoundboardService _soundboard;
        private readonly TuyaService _tuya;

        public ActionExecutorService(
            ILogger<ActionExecutorService> logger,
            IAppActivator appActivator,
            MediaControlService mediaService,
            MixerService mixerService,
            IAudioControlService audioControl,
            IHubContext<DeckHub> hubContext,
            DiscordRpcService discord,
            ObsService obs,
            SoundboardService soundboard,
            TuyaService tuya)
        {
            _logger = logger;
            _appActivator = appActivator;
            _mediaService = mediaService;
            _mixerService = mixerService;
            _audioControl = audioControl;
            _hubContext = hubContext;
            _discord = discord;
            _obs = obs;
            _soundboard = soundboard;
            _tuya = tuya;
        }

        public async Task ExecuteActionAsync(DeckAction action)
        {
            try
            {
                _logger.LogInformation("Executing action type: {Type}", action.Type);

                switch ((action.Type ?? "").ToLowerInvariant())
                {
                    case "launchapp":
                    case "launch_app":
                        ExecuteLaunchApp(action);
                        break;

                    case "activatewindow":
                        ExecuteActivateWindow(action);
                        break;

                    case "media":
                        await ExecuteMediaAction(action);
                        break;

                    case "hotkey":
                        ExecuteHotkey(action);
                        break;

                    case "mixer":
                        await ExecuteMixerAction(action);
                        break;

                    case "multi":
                        await ExecuteMultiAction(action);
                        break;

                    case "discord":
                        await ExecuteDiscordAction(action);
                        break;

                    case "obs":
                        await ExecuteObsAction(action);
                        break;

                    case "soundboard":
                        await ExecuteSoundboardAction(action);
                        break;

                    case "tuya":
                        await ExecuteTuyaAction(action);
                        break;

                    default:
                        _logger.LogWarning("Unknown action type: {Type}", action.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing action {Type}", action.Type);
            }
        }

        private void ExecuteLaunchApp(DeckAction action)
        {
            if (action.Parameters.TryGetValue("path", out var path))
            {
                action.Parameters.TryGetValue("arguments", out var args);
                _appActivator.LaunchApp(path, args);
            }
        }

        private void ExecuteActivateWindow(DeckAction action)
        {
            if (action.Parameters.TryGetValue("windowName", out var name))
            {
                _appActivator.ActivateWindow(name);
            }
        }

        private void ExecuteHotkey(DeckAction action)
        {
            if (action.Parameters.TryGetValue("keys", out var keys))
            {
                _appActivator.SendKeys(keys);
            }
        }

        private async Task ExecuteSoundboardAction(DeckAction action)
        {
            var operation = action.Parameters.TryGetValue("operation", out var op) ? op.ToLowerInvariant() : "play";
            var source = action.Parameters.TryGetValue("source", out var src) ? src.ToLowerInvariant() : "myinstants";

            if (operation == "stop")
            {
                _soundboard.StopAll();
                return;
            }

            if (source == "discord")
            {
                // Toca um som da soundboard NATIVA do Discord no canal de voz atual (sem cabo).
                action.Parameters.TryGetValue("soundId", out var soundId);
                action.Parameters.TryGetValue("guildId", out var guildId);
                await _discord.PlaySoundboardSoundAsync(soundId ?? "", guildId);
                return;
            }

            // MyInstants: toca o mp3 no dispositivo de saída configurado.
            action.Parameters.TryGetValue("url", out var url);
            action.Parameters.TryGetValue("id", out var id);
            action.Parameters.TryGetValue("title", out var title);
            await _soundboard.PlayAsync(id ?? "", url ?? "", title);
        }

        private async Task ExecuteMediaAction(DeckAction action)
        {
            if (!action.Parameters.TryGetValue("command", out var command)) return;

            // sessionId vazio = "a sessao que esta tocando" (como as teclas de midia). Quem
            // resolve isso agora e o MediaControlService, dentro da MESMA operacao serializada
            // que envia o comando: antes eram duas idas ao WinRT por toque (listar sessoes,
            // com capa de album e tudo, e so depois mandar o comando) e perder a disputa pelo
            // semaforo em qualquer uma delas fazia o botao nao fazer nada, sem log.
            var sessionId = action.Parameters.TryGetValue("sessionId", out var sess) ? sess : null;

            var result = await _mediaService.SendCommandAsync(sessionId, command);
            if (!result.Success)
            {
                _logger.LogWarning("Midia: comando '{Command}' do deck nao foi aplicado.", command);
                return;
            }

            // Empurra o estado real pros clientes: o botao pinta otimista no toque e precisa
            // ser corrigido se o comando pegou uma sessao diferente da esperada.
            if (result.Playing.HasValue)
                await _hubContext.Clients.All.SendAsync("ReceiveMediaStatus",
                    new { playing = result.Playing.Value });
        }

        /// <summary>
        /// Executa ações de mixer disparadas por um botão do deck.
        /// Parâmetros aceitos:
        ///   operation   : "toggleMute" (padrão) | "mute" | "unmute" | "setVolume"
        ///                 | "volumeUp" | "volumeDown"
        ///   processName : nome do processo (ex.: "Spotify") -> controla o áudio daquele app
        ///                 (só as operações de mudo)
        ///   deviceId    : id do dispositivo -> controla o dispositivo inteiro.
        ///                 "default" = dispositivo padrão do Windows (preferir isto a um id
        ///                 fixo: o id muda quando o usuário troca de fone/monitor)
        ///   deviceKind  : "output" (padrão) | "input" — só importa para deviceId="default"
        ///   volume      : 0-100 (usado com setVolume)
        ///   step        : passo em pontos percentuais do volumeUp/volumeDown (padrão 5)
        /// </summary>
        private async Task ExecuteMixerAction(DeckAction action)
        {
            var p = action.Parameters;

            p.TryGetValue("operation", out var operationRaw);
            var operation = (operationRaw ?? "toggleMute").Trim().ToLowerInvariant();

            p.TryGetValue("processName", out var processName);
            p.TryGetValue("deviceId", out var deviceId);
            p.TryGetValue("deviceKind", out var deviceKindRaw);

            int? volume = null;
            if (p.TryGetValue("volume", out var volStr) && int.TryParse(volStr, out var v))
                volume = Math.Clamp(v, 0, 100);

            // Passo do volumeUp/volumeDown, em pontos percentuais.
            var step = 5;
            if (p.TryGetValue("step", out var stepStr) && int.TryParse(stepStr, out var st))
                step = Math.Clamp(st, 1, 50);

            var isVolumeOp = operation is "setvolume" or "volumeup" or "volumedown";

            // Alvo = aplicativo (por nome de processo): mute/toggle por sessao de audio.
            if (!string.IsNullOrWhiteSpace(processName))
            {
                if (isVolumeOp)
                {
                    // O IAudioControlService so expoe mudo por processo; volume por app ainda
                    // nao existe. Avisa em vez de silenciosamente nao fazer nada.
                    _logger.LogWarning(
                        "Mixer: '{Op}' nao e suportado por processo ('{Proc}'). " +
                        "Use um dispositivo como alvo para controlar volume.", operation, processName);
                    return;
                }

                switch (operation)
                {
                    case "mute":
                        _audioControl.MuteByProcessName(processName, true);
                        break;
                    case "unmute":
                        _audioControl.MuteByProcessName(processName, false);
                        break;
                    default: // toggleMute
                        _audioControl.ToggleMuteByProcessName(processName);
                        break;
                }
                var muted = _audioControl.GetMuteStateByProcessName(processName);
                await _hubContext.Clients.All.SendAsync("ReceiveMuteState",
                    new { processName, muted });
                _logger.LogInformation("Mixer: '{Op}' no processo '{Proc}' (muted={Muted})", operation, processName, muted);
                return;
            }

            // Alvo = dispositivo (por id): volume/mudo do dispositivo inteiro.
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var isInput = string.Equals(deviceKindRaw, "input", StringComparison.OrdinalIgnoreCase);

                // "default" em vez de um id fixo: o id do endpoint muda quando o usuario troca
                // de fone/monitor, e ai o botao apontaria pra um dispositivo que nao existe mais.
                var resolvedId = deviceId;
                if (string.Equals(deviceId, "default", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedId = _mixerService.GetDefaultDeviceId(
                        isInput ? NAudio.CoreAudioApi.DataFlow.Capture : NAudio.CoreAudioApi.DataFlow.Render) ?? "";
                    if (string.IsNullOrEmpty(resolvedId))
                    {
                        _logger.LogWarning("Mixer: nao ha dispositivo padrao de {Kind}.", isInput ? "entrada" : "saida");
                        return;
                    }
                }

                var state = operation switch
                {
                    "mute" => _mixerService.SetDeviceAudio(resolvedId, mute: true),
                    "unmute" => _mixerService.SetDeviceAudio(resolvedId, mute: false),
                    "setvolume" => _mixerService.SetDeviceAudio(resolvedId, volume: volume ?? 0),
                    "volumeup" => _mixerService.SetDeviceAudio(resolvedId, delta: step),
                    "volumedown" => _mixerService.SetDeviceAudio(resolvedId, delta: -step),
                    _ => _mixerService.SetDeviceAudio(resolvedId, toggleMute: true),
                };

                if (state == null)
                {
                    _logger.LogWarning("Mixer: '{Op}' falhou no dispositivo '{Dev}'", operation, resolvedId);
                    return;
                }

                await _hubContext.Clients.All.SendAsync("ReceiveVolumeChange", new
                {
                    deviceId = state.Id,
                    type = isInput ? "input" : "output",
                    data = state
                });

                _logger.LogInformation("Mixer: '{Op}' em '{Title}' -> volume={Vol} mudo={Mute}",
                    operation, state.Title, state.Volume, state.Mute);
                return;
            }

            _logger.LogWarning("Mixer action ignorada: faltou 'processName' ou 'deviceId'.");
        }

        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Executa uma sequência de ações. parameters["steps"] = JSON de
        /// [{type, parameters, delayMs}]; delayMs é aguardado ANTES de cada passo.
        /// </summary>
        private async Task ExecuteMultiAction(DeckAction action)
        {
            if (!action.Parameters.TryGetValue("steps", out var stepsJson) ||
                string.IsNullOrWhiteSpace(stepsJson))
                return;

            List<MultiStep>? steps;
            try
            {
                steps = JsonSerializer.Deserialize<List<MultiStep>>(stepsJson, _jsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multi-ação: JSON de passos inválido");
                return;
            }
            if (steps == null) return;

            foreach (var step in steps)
            {
                if (string.IsNullOrWhiteSpace(step.Type)) continue;
                if (string.Equals(step.Type, "multi", StringComparison.OrdinalIgnoreCase))
                    continue; // não aninha multi
                if (step.DelayMs > 0)
                    await Task.Delay(step.DelayMs);
                await ExecuteActionAsync(new DeckAction
                {
                    Type = step.Type!,
                    Parameters = step.Parameters ?? new Dictionary<string, string>()
                });
            }
        }

        private async Task ExecuteDiscordAction(DeckAction action)
        {
            var p = action.Parameters;
            p.TryGetValue("operation", out var op);
            string Get(string k) => p.TryGetValue(k, out var v) ? v : "";
            double Num(string k, double def) => double.TryParse(Get(k), out var v) ? v : def;

            switch ((op ?? "toggleMute").Trim().ToLowerInvariant())
            {
                case "toggledeafen":
                case "toggledeaf":
                    await _discord.SetDeafAsync(null);
                    break;
                case "mute":
                    await _discord.SetMuteAsync(true);
                    break;
                case "unmute":
                    await _discord.SetMuteAsync(false);
                    break;

                // Canal de voz
                case "joinchannel":
                    if (!string.IsNullOrEmpty(Get("channelId")))
                        await _discord.SelectVoiceChannelAsync(Get("channelId"));
                    break;
                case "leavechannel":
                case "disconnect":
                    await _discord.SelectVoiceChannelAsync(null);
                    break;

                // Volume de mic / saída
                case "inputvolumeup":
                    await _discord.NudgeInputVolumeAsync(Num("delta", 10));
                    break;
                case "inputvolumedown":
                    await _discord.NudgeInputVolumeAsync(-Num("delta", 10));
                    break;
                case "outputvolumeup":
                    await _discord.NudgeOutputVolumeAsync(Num("delta", 10));
                    break;
                case "outputvolumedown":
                    await _discord.NudgeOutputVolumeAsync(-Num("delta", 10));
                    break;
                case "setinputvolume":
                    await _discord.SetInputVolumeAsync(Num("value", 100));
                    break;
                case "setoutputvolume":
                    await _discord.SetOutputVolumeAsync(Num("value", 100));
                    break;

                // Modo de voz
                case "togglevoicemode":
                case "toggleptt":
                    await _discord.ToggleVoiceModeAsync();
                    break;
                case "setvoicemode":
                    await _discord.SetVoiceModeAsync(
                        string.IsNullOrEmpty(Get("mode")) ? "VOICE_ACTIVITY" : Get("mode"));
                    break;

                // Por usuário
                case "usermute":
                case "usermutetoggle":
                    if (!string.IsNullOrEmpty(Get("userId")))
                        await _discord.ToggleUserMuteAsync(Get("userId"));
                    break;
                case "uservolume":
                    if (!string.IsNullOrEmpty(Get("userId")))
                        await _discord.SetUserVoiceAsync(Get("userId"), null, Num("value", 100));
                    break;

                default: // toggleMute
                    await _discord.SetMuteAsync(null);
                    break;
            }
        }

        /// <summary>
        /// Casa inteligente Tuya/Smart Life (cobre os rebrands: Nova Digital, Positivo, RSmart...).
        ///
        /// "toggle" e o caso comum de um deck e depende do estado que o push MQTT mantem em
        /// memoria -- por isso ele nao faz leitura na nuvem antes (o que gastaria cota a cada
        /// clique). Se o push estiver fora do ar o estado pode estar velho; nesse caso o usuario
        /// ve o botao inverter "para o lado errado" uma vez e acerta no clique seguinte.
        /// Para um comando deterministico use "set" com o valor explicito.
        /// </summary>
        private async Task ExecuteTuyaAction(DeckAction action)
        {
            var p = action.Parameters;
            string Get(string k) => p.TryGetValue(k, out var v) ? v : "";

            var deviceId = Get("deviceId");
            var code = Get("code");
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Tuya: acao sem deviceId/code");
                return;
            }

            var operation = (Get("operation") is { Length: > 0 } op ? op : "toggle").Trim().ToLowerInvariant();

            switch (operation)
            {
                case "toggle":
                    await _tuya.ToggleAsync(deviceId, code);
                    break;

                case "set":
                    await _tuya.SendCommandAsync(deviceId, code, ParseValue(Get("value"), Get("valueType")));
                    break;

                default:
                    _logger.LogWarning("Tuya: operacao desconhecida {Op}", operation);
                    break;
            }
        }

        /// <summary>
        /// Os parametros de acao sao string (Dictionary&lt;string,string&gt;), mas a Tuya exige o
        /// tipo certo: mandar "true" onde ela espera booleano faz a API recusar. O valueType vem
        /// do specifications do proprio aparelho, gravado no botao pelo editor.
        /// </summary>
        private static object? ParseValue(string raw, string valueType)
        {
            switch ((valueType ?? "").Trim().ToLowerInvariant())
            {
                case "boolean":
                    return bool.TryParse(raw, out var b) && b;
                case "integer":
                    return long.TryParse(raw, out var l) ? l : 0L;
                default:
                    // Enum, String e Json seguem como texto -- e o que a Tuya espera nesses casos.
                    return raw;
            }
        }

        private async Task ExecuteObsAction(DeckAction action)
        {
            var p = action.Parameters;
            p.TryGetValue("operation", out var op);
            string Get(string k) => p.TryGetValue(k, out var v) ? v : "";

            switch ((op ?? "").Trim().ToLowerInvariant())
            {
                case "setscene":
                    if (!string.IsNullOrEmpty(Get("scene"))) await _obs.SetSceneAsync(Get("scene"));
                    break;
                case "togglerecord":
                    await _obs.ToggleRecordAsync();
                    break;
                case "startrecord":
                    await _obs.StartRecordAsync();
                    break;
                case "stoprecord":
                    await _obs.StopRecordAsync();
                    break;
                case "togglestream":
                    await _obs.ToggleStreamAsync();
                    break;
                case "togglevirtualcam":
                    await _obs.ToggleVirtualCamAsync();
                    break;
                case "togglereplaybuffer":
                    await _obs.ToggleReplayBufferAsync();
                    break;
                case "savereplay":
                    await _obs.SaveReplayAsync();
                    break;
                case "toggleinputmute":
                    if (!string.IsNullOrEmpty(Get("inputName")))
                        await _obs.ToggleInputMuteAsync(Get("inputName"));
                    break;
            }
        }

        private class MultiStep
        {
            public string? Type { get; set; }
            public Dictionary<string, string>? Parameters { get; set; }
            public int DelayMs { get; set; }
        }
    }
}
