# OBS Studio

DroidDeck connects to **OBS Studio 28 or later** through its built-in WebSocket server (obs-websocket v5).

## What you can do

| Operation | Parameters | Button lights up when |
|---|---|---|
| Switch scene | Scene (picked from a list) | That scene is live |
| Toggle recording | — | Recording |
| Toggle streaming | — | Live |
| Toggle virtual camera | — | Virtual camera on |
| Toggle replay buffer | — | Replay buffer on |
| Save replay | — | — |
| Toggle audio source mute | Audio source (picked from a list) | — |
| Show / hide source | Scene and source (picked from lists) | — |
| Toggle filter | Source or scene, and filter (picked from lists) | — |
| Pause recording (toggle) | — | Recording paused |
| Recording chapter marker | Chapter name (optional). Needs OBS 30.2+ recording to Hybrid MP4 | — |
| Screenshot | — (PNG of the program scene, saved to OBS's recording folder) | — |

State comes straight from OBS events, so changes made in OBS itself are reflected on the deck too.

The **Switch scene** button shows where the scene is: red in program (or the color chosen for the button) and, with
**Studio Mode** on, green in preview. If the same scene is in both, program wins. By default OBS swaps the two scenes on
each transition, so the button alternates between red and green. Tapping the button sends the scene straight to program.

To use a phone as an on-air light or a camera tally, see [Tally and ON AIR](tally.md).

## Setup

1. In OBS: **Tools → WebSocket Server Settings**. Tick **Enable WebSocket server**. Note the port
   (default `4455`) and the password (or untick authentication).
2. In DroidDeck, open the OBS settings (phone: **Settings → Integrations → OBS**; configurator: **Integrations** in the sidebar).
3. Fill in **Host** (`localhost`), **Port** and **Password**, click **Save**, then **Connect**.

On the phone you can also tap **Scan OBS QR code** and scan the QR from **Show Connect Info** in OBS.
The host is always set to `localhost`, because OBS runs on the same PC as DroidDeck.

Once configured, DroidDeck reconnects automatically every 10 seconds when OBS is closed and reopened.

## Troubleshooting

| Message | Cause / fix |
|---|---|
| *OBS didn't respond (handshake). Is obs-websocket enabled?* | Enable the WebSocket server in OBS and check the port. |
| *OBS recusou o pedido.* | Wrong password, or the scene/source no longer exists. |
