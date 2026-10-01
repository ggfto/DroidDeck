# Discord

DroidDeck controls the **Discord desktop app** running on the same PC through Discord's local RPC.

## What you can do

| Operation | Parameters |
|---|---|
| Toggle mute | — |
| Toggle deafen | — |
| Join voice channel | Server and channel (picked from a list) |
| Disconnect from voice | — |
| Mic volume up / down | Step (1–50) |
| Output volume up / down | Step (1–50) |
| Toggle voice mode | Switches between voice activity and push-to-talk |
| Mute a user (locally) | User, picked from the current call |
| Set a user's volume | User and volume (0–200 %) |

There is also a native **Discord soundboard** source in the [Soundboard](soundboard.md) action.

Camera and screen sharing **cannot** be controlled: Discord does not expose them over RPC.

## Setup (once, about 2 minutes)

Discord only lets the **owner of an application** use its RPC without a review by Discord, so each
user creates their own (free) application:

1. Go to [discord.com/developers/applications](https://discord.com/developers/applications) and click **New Application**.
2. Under **OAuth2**, copy the **Client ID** and the **Client Secret**.
3. Under **OAuth2 → Redirects**, add `http://localhost:4787/discord` and click **Save Changes**.
4. In DroidDeck, open the Discord settings — on the phone: **settings → Discord**; in the
   configurator: **Configurar Discord** in the sidebar.
5. Paste the Client ID and Secret, click **Salvar credenciais** (*save credentials*), then **Conectar** (*connect*).
6. Discord shows an authorisation popup on the PC. Approve it within 60 seconds.

The token is renewed automatically and DroidDeck reconnects on its own every time it starts, without
showing the popup again. If Discord is closed, DroidDeck keeps retrying every 10 seconds.

!!! tip
    In the button editor, Discord actions show a warning with a **Configurar** link while Discord is
    not configured or not connected. The participant list for per-user actions only fills while you
    are in a voice call.

## Troubleshooting

| Message | Cause / fix |
|---|---|
| *Pipe do Discord não encontrado — o Discord está aberto?* | The Discord desktop app isn't running (the browser version doesn't work). |
| *Configure o Client ID e o Secret do Discord primeiro.* | Complete the setup above. |
| *Discord não respondeu ao handshake (READY).* | Discord is still starting; it will retry automatically. |
| Authorisation fails | Check that the redirect `http://localhost:4787/discord` was saved in the Developer Portal. |
