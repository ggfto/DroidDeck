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

        public ActionExecutorService(
            ILogger<ActionExecutorService> logger,
            IAppActivator appActivator,
            MediaControlService mediaService,
            MixerService mixerService,
            IAudioControlService audioControl,
            IHubContext<DeckHub> hubContext,
            DiscordRpcService discord,
            ObsService obs,
            SoundboardService soundboard)
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
            if (action.Parameters.TryGetValue("command", out var command))
            {
                string sessionId = action.Parameters.TryGetValue("sessionId", out var sess) ? sess : "";

                if (string.IsNullOrEmpty(sessionId))
                {
                    // Sem sessão específica: controla a que está TOCANDO (como as teclas de mídia);
                    // se nenhuma estiver tocando, usa a primeira disponível.
                    var sessions = await _mediaService.GetAllSessionsAsync();
                    if (sessions.Count > 0)
                    {
                        sessionId = sessions[0].Id!;
                        foreach (var s in sessions)
                        {
                            if (string.Equals(s.PlaybackStatus, "Playing", StringComparison.OrdinalIgnoreCase))
                            {
                                sessionId = s.Id!;
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(sessionId))
                {
                    await _mediaService.SendCommandAsync(sessionId, command);
                }
            }
        }

        /// <summary>
        /// Executa ações de mixer disparadas por um botão do deck.
        /// Parâmetros aceitos:
        ///   operation : "toggleMute" (padrão) | "mute" | "unmute" | "setVolume"
        ///   processName : nome do processo (ex.: "Spotify") -> controla o áudio daquele app
        ///   deviceId    : id do dispositivo -> controla o dispositivo inteiro
        ///   volume      : 0-100 (usado com setVolume em deviceId)
        /// </summary>
        private async Task ExecuteMixerAction(DeckAction action)
        {
            var p = action.Parameters;

            p.TryGetValue("operation", out var operationRaw);
            var operation = (operationRaw ?? "toggleMute").Trim().ToLowerInvariant();

            p.TryGetValue("processName", out var processName);
            p.TryGetValue("deviceId", out var deviceId);

            int? volume = null;
            if (p.TryGetValue("volume", out var volStr) && int.TryParse(volStr, out var v))
                volume = Math.Clamp(v, 0, 100);

            // Alvo = aplicativo (por nome de processo): mute/toggle por sessão de áudio.
            if (!string.IsNullOrWhiteSpace(processName))
            {
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

            // Alvo = dispositivo (por id): volume/mute do dispositivo inteiro.
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var data = new MixerData { Session = -1 };
                switch (operation)
                {
                    case "mute":
                        data.Mute = true;
                        break;
                    case "unmute":
                        data.Mute = false;
                        break;
                    case "setvolume":
                        data.Volume = volume;
                        break;
                    default: // toggleMute
                        data.Mute = !(_mixerService.FindOne(deviceId)?.Mute ?? false);
                        break;
                }
                new MixerMaster(deviceId).SetOptions(deviceId, data);
                _logger.LogInformation("Mixer: '{Op}' no dispositivo '{Dev}' (vol={Vol})", operation, deviceId, volume);
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
