# API

DroidDeck's server exposes a REST API and a SignalR hub on port `4787`. The phone app and the
configurator use them, and you can use them to build your own clients or automations.

## Authentication

Every endpoint requires the API key, except `GET /api/ping` and `GET /api/pairing/local-key`.
Send the key in one of these ways:

- header `X-API-KEY: <key>`
- header `Authorization: Bearer <key>`
- query string `?access_token=<key>` (used for the SignalR WebSocket)

`GET /api/pairing/local-key` returns `{key}` only to requests from the PC itself (loopback), and
rejects cross-site browser requests. CORS only allows origins on loopback and private network ranges.

Integration failures return `502` with `{error}`.

## Privileged actions

**Keyboard shortcut**, **open program**, **activate window** and **script** run code on the PC, and an **HTTP request** goes
out with the PC's network access, so they are protected even from clients that have the key. Requests that don't come from the PC itself (loopback) cannot:

- run them through `execute`; use `press` with a button that was saved on the PC;
- create a button with one of these actions, or change its parameters, through `POST profiles`.
  Moving, renaming, recolouring and deleting such buttons is allowed.

A multi-action counts as privileged when any of its steps is. Refused requests return `403` with
`{error}`.

Connections from a phone over the [USB cable](../getting-started/browser-usb.md#usb) arrive on loopback
through `adb`; the server recognises them and treats them as remote, not as the PC.

## Examples

```bash
# Is the server up?
curl http://192.168.0.10:4787/api/ping

# Toggle Discord mute
curl -X POST http://192.168.0.10:4787/api/StreamDeck/execute \
  -H "X-API-KEY: $KEY" -H "Content-Type: application/json" \
  -d '{"type":"discord","parameters":{"operation":"toggleMute"}}'

# Mute Spotify
curl -X POST http://192.168.0.10:4787/api/StreamDeck/execute \
  -H "X-API-KEY: $KEY" -H "Content-Type: application/json" \
  -d '{"type":"mixer","parameters":{"operation":"mute","processName":"Spotify"}}'
```

## Deck: `/api/StreamDeck` { #deck-apistreamdeck }

| Method | Route | Description |
|---|---|---|
| GET | `profiles` | List profiles. |
| GET | `profiles/{id}` | Get a profile. |
| POST | `profiles` | Create or update a profile. Broadcasts `ReceiveDeckUpdate`. |
| DELETE | `profiles/{id}` | Delete a profile. Broadcasts `ReceiveDeckUpdate`. |
| POST | `press` | Press a saved button (`{profileId, buttonId, gesture}`; `gesture` is `tap` (default), `longPress` or `doubleTap`): the server runs the action stored in the profile for that gesture. This is what the phone uses. |
| POST | `execute` | Run an ad-hoc action (`{type, parameters}`). From outside the PC, privileged actions return `403`. |
| GET / POST | `layout` | Read / set the grid size (`{rows, columns}`). |

### Action payload

```json
{ "type": "hotkey", "parameters": { "keys": "^+m" } }
```

All parameter values are strings. The table below is a summary; the machine-readable source of
truth is [`GET /api/actions/schema`](#action-types-apiactions), which lists every action type with
its parameters (`fields[].key`), allowed values (`options`) and when each field applies
(`visibleWhen`).

| `type` | Parameters |
|---|---|
| `hotkey` | `keys` |
| `launch_app` | `path`, `arguments`? |
| `activatewindow` | `windowName` (exact window title) |
| `media` | `command`: `playpause`·`play`·`pause`·`next`·`previous`·`stop`; `sessionId`? |
| `mixer` | `operation`: `toggleMute`·`mute`·`unmute`·`setVolume`·`volumeUp`·`volumeDown`; `processName` **or** `deviceId` (`default` or an endpoint id); `deviceKind`: `output`·`input`; `volume` (0–100); `step` (1–50) |
| `multi` | `steps`: JSON array string `[{"type","parameters","delayMs","if"}]`; `if` is optional: `{check, value, negate}` with `check` one of `obs_streaming`·`obs_recording`·`discord_mute`·`discord_deaf`·`toggle_on` (`value` = button id)·`app_running`·`app_focused` (`value` = process)·`time_between` (`value` = `08:00-18:00`)·`weekday` (`value` = `1-5`, 0 = Sunday) |
| `discord` | `operation`: `toggleMute`·`mute`·`unmute`·`toggleDeafen`·`joinChannel` (`channelId`)·`disconnect`·`inputVolumeUp/Down`·`outputVolumeUp/Down` (`delta`)·`setInputVolume`·`setOutputVolume` (`value`)·`toggleVoiceMode`·`setVoiceMode` (`mode`: `VOICE_ACTIVITY`·`PUSH_TO_TALK`)·`userMute` (`userId`)·`userVolume` (`userId`, `value` 0–200) |
| `obs` | `operation`: `setScene` (`scene`)·`toggleRecord`·`startRecord`·`stopRecord`·`toggleStream`·`toggleVirtualCam`·`toggleReplayBuffer`·`saveReplay`·`toggleInputMute` (`inputName`) |
| `soundboard` | `operation`: `play`·`stop`; `source`: `myinstants` (`id`, `url`, `title`) or `discord` (`soundId`, `guildId`) |
| `tuya` | `deviceId`, `code`, `operation`: `toggle`·`set`; `value`, `valueType` |
| `homeassistant` | `operation`: `toggle`·`turn_on`·`turn_off`·`brightness` (`step`)·`scene`·`script`·`display`·`service` (`service` = `domain.service`, `data` JSON); `entityId` |
| `spotify` | `operation`: `playPause`·`next`·`previous`·`like`·`shuffle`·`repeat`·`volume` (`step`)·`playlist` (`uri`, `shuffleOn`)·`nowPlaying` |
| `twitch` | `operation`: `marker` (`description`)·`clip`·`ad` (`length`)·`chat` (`message`)·`poll` (`question`, `choices` one per line, `duration`)·`title` (`title`)·`status` |
| `youtube` | `operation`: `chat` (`message`)·`status` |
| `network` | `protocol`: `osc` (`path`, `args`: `i:1`·`f:0.5`·`s:text`·`T`·`F`, one per line)·`udp`·`tcp` (`message`, `encoding`: `text`·`hex`, `ending`: `none`·`lf`·`crlf`·`cr`); `host`, `port`. Privileged |
| `tally` | `operation`: `open`·`show` (`target`, from the list at `GET /api/tally/targets`)·`manual_toggle`·`manual_on`·`manual_off` |
| `vmix` | `operation`: `cut`·`fade` (`duration`)·`preview`·`program`·`quickplay`·`toggle_audio` (`input`)·`set_volume` (`input`, `volume`)·`overlay` (`input`, `overlay`)·`overlay_off`·`master_audio`·`master_volume` (`volume`)·`toggle_record`·`toggle_stream`·`toggle_external`·`toggle_multicorder`·`fade_to_black`·`snapshot`·`custom` (`function`, `query`) |
| `companion` | Only the app opens the surface |
| `mairlist` | `operation`: `on_air_toggle`·`on_air`·`off_air`·`automation_toggle`·`automation_on`·`automation_off`·`next` (`playlist`)·`player_toggle`·`player_start`·`player_stop`·`player_fadeout` (`playlist`, `player`)·`cart` (`cart`)·`cart_stop_all`·`custom` (`command`) |
| `radiodj` | `operation`: `play_track` (`track`, from 1)·`stop`·`pause`·`resume`·`restart`·`play_intro`·`autodj_toggle`·`assisted_toggle`·`input_toggle`·`events_on`·`events_off`·`cart` (`cart`)·`load_playlist`·`load_track` (`id`)·`custom` (`command`, `arg`) |
| `streamerbot` | `action` (id or name), `args` (`key=value` per line, or JSON) |
| `vtubestudio` | `operation`: `hotkey` (`hotkey`: id or name)·`model` (`model`: model id) |
| `voicemeeter` | `operation`: `toggle_mute`·`mute`·`unmute`·`set_gain` (`gain` dB)·`adjust_gain` (`step` dB) with `target`: `strip`·`bus` and `index` (1–8)·`macro` (`button`)·`param_set` (`parameter`, `value`)·`param_toggle` (`parameter`) |
| `hue` | `operation`: `toggle`·`on`·`off`·`brightness` (`brightness` %)·`brightness_step` (`step` %) with `target`: `light`·`group` and `id`·`scene` (`scene`) |
| `keylight` | `host` (IP or IP:port); `operation`: `toggle`·`on`·`off`·`brightness` (`brightness`)·`brightness_step` (`step`)·`temperature` (`kelvin`)·`temperature_step` (`kelvinStep`) |
| `govee` | `ip`; `operation`: `toggle`·`on`·`off`·`brightness` (`brightness`)·`color` (`color` #RRGGBB)·`temperature` (`kelvin`) |
| `mqtt` | `topic`, `payload`, `qos` (0–2), `retain` (`true`/`false`) |
| `script` | `source`: `inline` (`language`: `powershell`·`python`·`cmd`; `code`) or `file` (`path`); `arguments` |
| `http` | `method`: `POST`·`GET`·`PUT`·`PATCH`·`DELETE`; `url`; `headers` (one `Name: value` per line); for POST, PUT and PATCH, `contentType` (`application/json`·`application/x-www-form-urlencoded`·`text/plain`) and `body` |
| `timer` | `mode`: `countdown` (`durationMin`)·`stopwatch`·`pomodoro` (`workMin`, `breakMin`)·`counter` (`step`); `operation`: `toggle`·`reset`·`skip`·`increment`·`decrement`; `target`: `self` or the id of another timer button |
| `windows` | `operation`: `setDefaultAudio` (`deviceKind`: `output`·`input`; `deviceId`; `altDeviceId` to toggle between two, or `none`)·`lock`·`sleep`·`displayOff` |
| `steam` | `appId`: the numeric Steam app id (from `GET /api/steam/games`) |

`open_profile` and `back` exist only on the phone.

## Audio: `/api/v1/Mixer`, `/api/v1/Media` { #audio }

| Method | Route | Description |
|---|---|---|
| GET | `Mixer/out`, `Mixer/in` | Output / input devices with their app sessions. |
| GET | `Mixer/out/{id}`, `Mixer/in/{id}` | One device. |
| PUT | `Mixer/out/{id}`, `Mixer/in/{id}` | `{session, volume?, mute?}`: `session` is a process id, or `-1` for the whole device. |
| GET | `Media/sessions` | Media sessions, with thumbnails. |
| GET | `Media/sessions/{id}` | One session. |
| POST | `Media/sessions/{id}/command` | `{command}`. |

A profile is `{id, name, rows, columns, isDefault, buttons, autoSwitchApps}`; a button has `action`,
`longPressAction` and `doubleTapAction` (each `{type, parameters}` or `null`). The privileged-action
rule applies to all three.

Two-state buttons add `toggle` (bool), `activeAction` (run on the tap that turns it off),
`activeLabel`, `activeIconName` and `activeIconBase64`. Info buttons use `dynamicType` (`cpu_monitor`,
`memory_monitor`, `gpu_monitor`, `network_monitor`, `clock`, `weather`, `now_playing`, `command`,
`url`, `notifications`, `volume_slider`, `ai_usage`) with `dynamicParameters` (`city`, `units`, `command`, `url`,
`path`, `format`, `interval`, `app`, `target`…); `command` and `url` follow the privileged-action rule.
Large buttons add `columnSpan` and `rowSpan` (default 1; `row`/`column` is the top-left cell).

| Method | Path | Description |
|---|---|---|
| GET | `/api/slider?target=…` | Slider: current `{volume, mute, label, max, text}`. `target`: `output`, `input`, `app` (`app`), `obs` (`inputName`), `discord_user` (`userId`), `monitor`, `media`, `tuya` (`deviceId`), `homeassistant` (`entityId`), `hue` (`hueTarget`, `id`), `govee` (`ip`), `keylight` (`host`). `max` is 100, except Discord (200) and music (duration in seconds). `404` when the target doesn't exist right now; `503 {error}` when the integration doesn't respond. |
| PUT | `/api/slider` | `{target, app, params, volume}` (0 to `max`). Returns the new state. |
| GET | `/api/slider/options?target=tuya\|discord_user` | `{value, label}` list of Smart Life bulbs with brightness or of people in the call. |

## Library: backup, Steam, apps { #library }

| Method | Path | Description |
|---|---|---|
| GET | `/api/backup` | Download every profile as a `.droiddeck` (zip). |
| POST | `/api/backup` | Restore a `.droiddeck` (request body = the file). Same-id profiles are replaced. From outside the PC, refused with `403` if it would create privileged buttons. |
| GET | `/api/steam/games` | Installed Steam games (`{value: appId, label: name}`). |
| GET | `/api/steam/icon/{appId}` | `{iconBase64}`: a square PNG cut from the game's cover. |
| GET | `/api/system/apps` | Programs with a visible window (for automatic profile switching). |
| GET | `/api/system/info` | `{name, version}`: the PC's Windows name, used to label paired PCs. |
| GET | `/api/info` | Current values of the info buttons, what's playing and the two-state buttons that are on. |
| GET / POST | `/api/macro`, `/api/macro/start`, `/api/macro/stop` | Keyboard macro recording. Only from the PC itself (`403` otherwise); `stop` returns `{steps}` in the multi-action format. |
| POST | `/api/import/streamdeck` | Imports a `.streamDeckProfile` (request body = the file, up to 50 MB) as new profiles. Returns `{profiles, buttons, unsupported, stripped, firstProfileId}`; from outside the PC, privileged buttons come in without their action and `stripped` counts them. `400` with `{error}` if it isn't a Stream Deck profile. |
| GET | `/api/share/{profileId}` | `{data, url, qr, images}`: the profile compressed into a link (`url` is the site's share page with `#data`); `qr` says whether it fits in a QR code; `images` whether custom images were left out. |
| GET | `/api/share/{profileId}/qr` | The share link as a PNG QR code. |
| POST | `/api/share/import` | `{data}` from a share link → saved as a new profile. Returns `{profileId, name, buttons, stripped}`; same privileged-action rule as the Stream Deck import. |

## Automations: `/api/triggers` { #automations }

| Method | Path | Description |
|---|---|---|
| GET | `/api/triggers` | The list of automations. |
| PUT | `/api/triggers` | Replaces the whole list. Returns the saved list. |
| POST | `/api/triggers/{id}/run` | Presses the automation's button now. `404` if the automation or the button doesn't exist. |

An automation is `{id, name, enabled, event, time, days, app, profileId, buttonId, if}`. `event` is
`schedule` (`time` `HH:mm`, `days` like `1-5`), `app_start` / `app_exit` (`app`), `resume` or
`startup`; `if` is an optional condition in the multi-action format. Since automations only press saved
buttons, they follow the rules those buttons were created under.

## Action types: `/api/actions` { #action-types-apiactions }

| Method | Path | Description |
|---|---|---|
| GET | `schema` | Every action type with its title, icon, category and fields. The editor builds its form from this. |
| GET | `state/{type}` | Current live state of a type (the same payload as its SignalR event). |
| GET | `timer/targets` | Timer buttons another button can control. |
| GET | `windows/audio-devices?kind=output\|input` | Active audio devices (`{value, label}`). |

The schema is the source of truth for action parameters: the app's editor builds its forms from it,
so a new action type or field shows up here first. Texts follow `Accept-Language`. An abridged
response:

```json
[
  {
    "type": "steam",
    "title": "Steam game",
    "icon": "sports_esports",
    "category": "System",
    "privileged": false,
    "stateful": false,
    "editor": "steam",
    "stateEvent": null,
    "fields": [
      {
        "key": "appId",
        "label": "Game",
        "kind": "select",
        "required": true,
        "optionsSource": "/api/steam/games",
        "visibleWhen": null
      }
    ]
  }
]
```

## Integrations

| Prefix | Endpoints |
|---|---|
| `/api/discord` | `POST config` `{clientId, clientSecret}` · `POST connect` · `GET state` · `GET guilds` · `GET channels/{guildId}` · `GET voice-channel` · `POST mute` · `POST deafen` · `GET soundboard-sounds` · `POST play-soundboard` |
| `/api/obs` | `POST config` `{host, port, password}` · `POST connect` · `GET state` · `GET scenes` · `GET audio-inputs` · `GET sources` · `GET scene-items?scene=` · `GET filters?source=` |
| `/api/Soundboard` | `GET search?q=` · `GET trending` · `POST play` · `POST stop` · `GET state` · `GET devices` · `GET/POST config` |
| `/api/tuya` | `POST pair/start` `{userCode}` · `POST pair/poll` (410 when the QR expired) · `GET state` · `POST connect` · `POST devices/refresh` · `POST command` `{deviceId, code, value}` |
| `/api/govee` | `GET devices`: searches the network for Govee devices with LAN Control (about 2 s); list of `{value: ip, label}` |
| `/api/tally` | `GET targets`: what the tally screen can follow now, as `{value, label}` |
| `/api/companion` | `GET keys`: `{connected, columns, rows, keys}`, with the surface's current keys |
| `/api/integrations/{name}` | `homeassistant`, `spotify`, `twitch`, `youtube`, `hue`, `mqtt`, `streamerbot`, `vtubestudio`, `vmix`, `companion`, `mairlist`, `radiodj`. `GET` status `{connected, configured, account, message, loginPending, userCode, verificationUri, redirectUri, fields}` · `POST` the setup fields (connects or starts sign-in in the PC's browser) · `DELETE` forget the account · `GET options/{list}` (`entities?operation=`, `playlists`, Hue `items?target=`·`scenes`, Streamer.bot `actions`, vMix `inputs`, VTube Studio `hotkeys`·`models`) · `GET callback` (OAuth return, no API key) |

Responses follow the request's `Accept-Language`: with `en`, action types, field labels and error
messages come in English.

## SignalR hub: `/deckHub` { #signalr-hub }

Connect with `?access_token=<key>`. The hub only pushes events; commands go through REST. The
exception is `SetLanguage("en")`, which switches the language of texts the PC builds (info button
errors). The starting language comes from `?lang=pt|en|es` or, without it, from `Accept-Language`.

| Event | Payload |
|---|---|
| `ReceiveSystemStats` | `{cpuUsage, ramUsage, ramTotal, ramAvailable, netUpKBps, netDownKBps, gpuUsage, disks}`: every second. `disks` is `[{name: "C:", label, totalBytes, freeBytes}]`, read every 30 s |
| `ReceiveMediaStatus` | `{playing}` |
| `ReceiveMediaState` | `{sessionId, command, playing}` |
| `ReceiveVolumeChange` | `{deviceId, type, data}` |
| `ReceiveMuteState` | `{processName, muted}` |
| `ReceiveDeckUpdate` | `{profileId}` |
| `ReceiveLayoutUpdate` | `{rows, columns}` |
| `ReceiveDiscordState` | Connection, mute/deaf, channel, volumes, voice mode, participants |
| `ReceiveObsState` | Connection, current scene, recording/streaming/virtual cam/replay, scenes, audio inputs |
| `ReceiveTuyaState` | Connection, push, devices with status and functions |
| `ReceiveTuyaDeviceState` | `{deviceId, status}`: changed values only |
| `ReceiveHueState`, `ReceiveMqttState`, `ReceiveStreamerBotState`, `ReceiveVTubeStudioState` | `{connected}` (VTube Studio also sends `model`) |
| `ReceiveTimerState` | `{buttonId: {mode, running, finished, elapsedMs, durationMs, count, phase, cycles}}`: on every change |
| `ReceiveWindowsState` | `{defaultOutput, defaultInput}`: default audio device ids |
| `ReceiveAutoProfile` | `{profileId, app}`: profile of the focused app; `profileId` null when the app has none |
| `ReceivePlaybackState` | `{playing, title}`: soundboard |
| `ReceiveButtonToggles` | `{buttonId: true}`: two-state buttons that are on |
| `ReceiveInfoState` | `{buttonId: {text, sub, code, isDay, error}}`: weather, command and URL buttons |
| `ReceiveNowPlaying` | `{title, artist, playing, thumbnailBase64}` |
| `ReceiveHomeAssistantState` | `{connected, entities: {id: {state, name, unit, brightness, color}}}`: entities used by buttons |
| `ReceiveSpotifyState` | `{connected, account, playing, track, artist, liked, shuffle, repeat, volume, device, coverBase64}` |
| `ReceiveTwitchState`, `ReceiveYouTubeState` | `{connected, account, live, viewers, title}` |

On connect, the new client receives the current Discord, layout, OBS and Tuya state.

## Discovery: UDP 7573 { #discovery }

Send the ASCII text `DroidDeckDiscoveryRequest` as a broadcast to port 7573. The server answers with
`{"ip": "...", "name": "<machine name>"}`. Discovery never returns the API key.

## Pairing URI

`droiddeck://pair?ip=<ip>&port=4787&key=<key>`
