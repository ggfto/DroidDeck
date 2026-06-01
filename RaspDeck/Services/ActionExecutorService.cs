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

        public ActionExecutorService(
            ILogger<ActionExecutorService> logger,
            IAppActivator appActivator,
            MediaControlService mediaService,
            MixerService mixerService)
        {
            _logger = logger;
            _appActivator = appActivator;
            _mediaService = mediaService;
            _mixerService = mixerService;
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

        private void ExecuteMixerAction(DeckAction action)
        {
             // TODO: Implement mixer actions (mute, volume set)
             // Need to map parameters to MixerService calls
             // Example: action=toggleMute, deviceName=Speakers
        }
    }
}
