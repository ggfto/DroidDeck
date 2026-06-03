namespace DroidDeck.Services
{
    public interface IAudioControlService
    {
        /// <summary>
        /// Mute the audio sessions belonging to processes with the given process name (without .exe), returns number of sessions affected.
        /// </summary>
        int MuteByProcessName(string processName, bool mute);

        /// <summary>
        /// Toggle mute state for processes with given name. Returns new mute state (true if muted).
        /// </summary>
        bool ToggleMuteByProcessName(string processName);

        /// <summary>
        /// Get mute state for first matching process session, or null if none.
        /// </summary>
        bool? GetMuteStateByProcessName(string processName);
    }
}
