using Windows.Media.Control;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    public class MediaControlService
    {
        private readonly ILogger<MediaControlService> _logger;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;

        public MediaControlService(ILogger<MediaControlService> logger)
        {
            _logger = logger;
        }

        private async Task<GlobalSystemMediaTransportControlsSessionManager> GetSessionManagerAsync()
        {
            if (_sessionManager == null)
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            return _sessionManager;
        }

        public async Task<List<MediaSessionInfo>> GetAllSessionsAsync()
        {
            try
            {
                var manager = await GetSessionManagerAsync();
                var sessions = manager.GetSessions();
                var result = new List<MediaSessionInfo>();

                foreach (var session in sessions)
                {
                    var info = await GetSessionInfoAsync(session);
                    if (info != null)
                    {
                        result.Add(info);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media sessions");
                return new List<MediaSessionInfo>();
            }
        }

        public async Task<MediaSessionInfo?> GetSessionByIdAsync(string sessionId)
        {
            try
            {
                var manager = await GetSessionManagerAsync();
                var sessions = manager.GetSessions();
                var session = sessions.FirstOrDefault(s => s.SourceAppUserModelId == sessionId);

                if (session != null)
                {
                    return await GetSessionInfoAsync(session);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session by ID: {sessionId}", sessionId);
            }
            return null;
        }

        private async Task<MediaSessionInfo?> GetSessionInfoAsync(GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                var mediaProperties = await session.TryGetMediaPropertiesAsync();
                var playbackInfo = session.GetPlaybackInfo();
                var timelineProperties = session.GetTimelineProperties();

                string? thumbnailBase64 = null;
                if (mediaProperties.Thumbnail != null)
                {
                    var streamRef = mediaProperties.Thumbnail;
                    using var stream = await streamRef.OpenReadAsync();
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    var bytes = new byte[stream.Size];
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(bytes);
                    thumbnailBase64 = "data:image/png;base64," + Convert.ToBase64String(bytes);
                }

                return new MediaSessionInfo
                {
                    Id = session.SourceAppUserModelId,
                    Title = mediaProperties.Title,
                    Artist = mediaProperties.Artist,
                    AlbumTitle = mediaProperties.AlbumTitle,
                    ThumbnailBase64 = thumbnailBase64,
                    PlaybackStatus = playbackInfo.PlaybackStatus.ToString(),
                    CanPlayPause = playbackInfo.Controls.IsPlayPauseToggleEnabled,
                    CanGoNext = playbackInfo.Controls.IsNextEnabled,
                    CanGoPrevious = playbackInfo.Controls.IsPreviousEnabled,
                    Position = timelineProperties.Position.TotalSeconds,
                    Duration = timelineProperties.EndTime.TotalSeconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session info");
                return null;
            }
        }

        /// <summary>True se há mídia TOCANDO (sessão atual ou qualquer uma). Leve — sem thumbnail/props.</summary>
        public async Task<bool> IsAnythingPlayingAsync()
        {
            try
            {
                var manager = await GetSessionManagerAsync();
                var current = manager.GetCurrentSession();
                if (current != null &&
                    current.GetPlaybackInfo().PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return true;
                foreach (var s in manager.GetSessions())
                {
                    if (s.GetPlaybackInfo().PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        return true;
                }
                return false;
            }
            catch { return false; }
        }

        public async Task<bool> SendCommandAsync(string sessionId, string command)
        {
            try
            {
                var manager = await GetSessionManagerAsync();
                var sessions = manager.GetSessions();
                var session = sessions.FirstOrDefault(s => s.SourceAppUserModelId == sessionId);

                if (session == null)
                {
                    _logger.LogWarning("Session not found: {sessionId}", sessionId);
                    return false;
                }

                switch (command.ToLower())
                {
                    case "play":
                        await session.TryPlayAsync();
                        break;
                    case "pause":
                        await session.TryPauseAsync();
                        break;
                    case "playpause":
                    case "toggle":
                        await session.TryTogglePlayPauseAsync();
                        break;
                    case "next":
                        await session.TrySkipNextAsync();
                        break;
                    case "previous":
                        await session.TrySkipPreviousAsync();
                        break;
                    case "stop":
                        await session.TryStopAsync();
                        break;
                    default:
                        _logger.LogWarning("Unknown command: {command}", command);
                        return false;
                }

                _logger.LogInformation("Sent command {command} to session {sessionId}", command, sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending command {command} to session {sessionId}", command, sessionId);
                return false;
            }
        }
    }

    public class MediaSessionInfo
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? AlbumTitle { get; set; }
        public string? ThumbnailBase64 { get; set; }
        public string? PlaybackStatus { get; set; }
        public bool CanPlayPause { get; set; }
        public bool CanGoNext { get; set; }
        public bool CanGoPrevious { get; set; }
        public double Position { get; set; }
        public double Duration { get; set; }
    }
}
