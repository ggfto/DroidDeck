# Mixer & media

Besides the [Mixer](../guide/actions.md#mixer) and [Media](../guide/actions.md#media) buttons, the
phone app has an **Áudio** tab — a full mixer for the PC.

## The Áudio tab

- **Output and input devices**: mute and volume slider for the current speakers and microphone.
- **Apps**: one slider and mute button per app currently playing audio, with its icon and window title.
- **Media**: play/pause, next and previous for apps that expose a media session.

Changes made on the PC (Windows volume mixer, keyboard keys) are pushed to the phone.

## How media control works

DroidDeck uses Windows' **media session** system — the same one behind the media overlay that
appears when you press a media key. Any app that shows up there can be controlled: Spotify, browsers
(YouTube, etc.), Windows Media Player, VLC and others.

If nothing is playing, the command goes to the most recent session. If Windows' media service stops
responding, DroidDeck pauses its background checks for a minute (you will see a warning in the log),
while button presses keep working.

## Tips

- Use a **Mixer → Toggle mute** button with target **Default → input** as a universal mic mute that
  works in any app.
- For a game-friendly volume knob, create two buttons: **Volume up** and **Volume down** on the
  **Default → output** device.
