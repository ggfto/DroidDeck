using System;
using System.Threading.Tasks;
using AnyDeck.Models;
using Microsoft.Extensions.Logging;

namespace AnyDeck.Services
{
    public class ActionExecutorService
    {
        private readonly ILogger<ActionExecutorService> _logger;
        private readonly IAppActivator _appActivator;
        private readonly MediaControlService _mediaService;
        private readonly MixerService _mixerService;
        private readonly IAudioControlService _audioControl;

        public ActionExecutorService(
            ILogger<ActionExecutorService> logger,
            IAppActivator appActivator,
            MediaControlService mediaService,
            MixerService mixerService,
            IAudioControlService audioControl)
        {
            _logger = logger;
            _appActivator = appActivator;
            _mediaService = mediaService;
            _mixerService = mixerService;
            _audioControl = audioControl;
        }

        public async Task ExecuteActionAsync(DeckAction action)
        {
            try
            {
                _logger.LogInformation("Executing action type: {Type}", action.Type);

                switch (action.Type.ToLower())
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
                        ExecuteMixerAction(action);
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

        private async Task ExecuteMediaAction(DeckAction action)
        {
            if (action.Parameters.TryGetValue("command", out var command))
            {
                string sessionId = action.Parameters.TryGetValue("sessionId", out var sess) ? sess : "";

                if (string.IsNullOrEmpty(sessionId))
                {
                    // Fallback: try to find any active session or use system default if available
                    // For now, we need a sessionId for the service
                    var sessions = await _mediaService.GetAllSessionsAsync();
                    if (sessions.Count > 0)
                    {
                        sessionId = sessions[0].Id!;
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
        private void ExecuteMixerAction(DeckAction action)
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
                _logger.LogInformation("Mixer: '{Op}' no processo '{Proc}'", operation, processName);
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
    }
}
