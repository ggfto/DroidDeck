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

State comes straight from OBS events, so changes made in OBS itself are reflected on the deck too.

## Setup

1. In OBS: **Tools → WebSocket Server Settings**. Tick **Enable WebSocket server**. Note the port
   (default `4455`) and the password (or untick authentication).
2. In DroidDeck, open the OBS settings (phone: **settings → OBS**; configurator: the OBS button in the sidebar).
3. Fill in **Host** (`localhost`), **Port** and **Password**, click **Salvar**, then **Conectar**.

On the phone you can also tap **Ler QR do OBS** and scan the QR from **Show Connect Info** in OBS.
The host is always set to `localhost`, because OBS runs on the same PC as DroidDeck.

Once configured, DroidDeck reconnects automatically every 10 seconds when OBS is closed and reopened.

## Troubleshooting

| Message | Cause / fix |
|---|---|
| *OBS não respondeu (handshake). O obs-websocket está ativo?* | Enable the WebSocket server in OBS and check the port. |
| *OBS recusou o pedido.* | Wrong password, or the scene/source no longer exists. |
