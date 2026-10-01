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

## Deck — `/api/StreamDeck`

| Method | Route | Description |
|---|---|---|
| GET | `profiles` | List profiles. |
| GET | `profiles/{id}` | Get a profile. |
| POST | `profiles` | Create or update a profile. Broadcasts `ReceiveDeckUpdate`. |
| DELETE | `profiles/{id}` | Delete a profile. Broadcasts `ReceiveDeckUpdate`. |
| POST | `execute` | Run an action (`{type, parameters}`). |
| GET / POST | `layout` | Read / set the grid size (`{rows, columns}`). |

### Action payload

```json
{ "type": "hotkey", "parameters": { "keys": "^+m" } }
```

All parameter values are strings.

| `type` | Parameters |
|---|---|
| `hotkey` | `keys` |
| `launch_app` | `path`, `arguments`? |
| `activatewindow` | `windowName` (exact window title) |
| `media` | `command`: `playpause`·`play`·`pause`·`next`·`previous`·`stop`; `sessionId`? |
| `mixer` | `operation`: `toggleMute`·`mute`·`unmute`·`setVolume`·`volumeUp`·`volumeDown`; `processName` **or** `deviceId` (`default` or an endpoint id); `deviceKind`: `output`·`input`; `volume` (0–100); `step` (1–50) |
| `multi` | `steps`: JSON array string `[{"type","parameters","delayMs"}]` |
| `discord` | `operation`: `toggleMute`·`mute`·`unmute`·`toggleDeafen`·`joinChannel` (`channelId`)·`disconnect`·`inputVolumeUp/Down`·`outputVolumeUp/Down` (`delta`)·`setInputVolume`·`setOutputVolume` (`value`)·`toggleVoiceMode`·`setVoiceMode` (`mode`: `VOICE_ACTIVITY`·`PUSH_TO_TALK`)·`userMute` (`userId`)·`userVolume` (`userId`, `value` 0–200) |
| `obs` | `operation`: `setScene` (`scene`)·`toggleRecord`·`startRecord`·`stopRecord`·`toggleStream`·`toggleVirtualCam`·`toggleReplayBuffer`·`saveReplay`·`toggleInputMute` (`inputName`) |
| `soundboard` | `operation`: `play`·`stop`; `source`: `myinstants` (`id`, `url`, `title`) or `discord` (`soundId`, `guildId`) |
| `tuya` | `deviceId`, `code`, `operation`: `toggle`·`set`; `value`, `valueType` |

`open_profile` and `back` exist only on the phone.

## Audio — `/api/v1/Mixer`, `/api/v1/Media`

| Method | Route | Description |
|---|---|---|
| GET | `Mixer/out`, `Mixer/in` | Output / input devices with their app sessions. |
| GET | `Mixer/out/{id}`, `Mixer/in/{id}` | One device. |
| PUT | `Mixer/out/{id}`, `Mixer/in/{id}` | `{session, volume?, mute?}` — `session` is a process id, or `-1` for the whole device. |
| GET | `Media/sessions` | Media sessions, with thumbnails. |
| GET | `Media/sessions/{id}` | One session. |
| POST | `Media/sessions/{id}/command` | `{command}`. |

## Integrations

| Prefix | Endpoints |
|---|---|
| `/api/discord` | `POST config` `{clientId, clientSecret}` · `POST connect` · `GET state` · `GET guilds` · `GET channels/{guildId}` · `GET voice-channel` · `POST mute` · `POST deafen` · `GET soundboard-sounds` · `POST play-soundboard` |
| `/api/obs` | `POST config` `{host, port, password}` · `POST connect` · `GET state` · `GET scenes` · `GET audio-inputs` |
| `/api/Soundboard` | `GET search?q=` · `GET trending` · `POST play` · `POST stop` · `GET state` · `GET devices` · `GET/POST config` |
| `/api/tuya` | `POST pair/start` `{userCode}` · `POST pair/poll` (410 when the QR expired) · `GET state` · `POST connect` · `POST devices/refresh` · `POST command` `{deviceId, code, value}` |

## SignalR hub — `/deckHub`

Connect with `?access_token=<key>`. The hub only pushes events; commands go through REST.

| Event | Payload |
|---|---|
| `ReceiveSystemStats` | `{cpuUsage, ramUsage, ramTotal, ramAvailable, netUpKBps, netDownKBps, gpuUsage}` — every second |
| `ReceiveMediaStatus` | `{playing}` |
| `ReceiveMediaState` | `{sessionId, command, playing}` |
| `ReceiveVolumeChange` | `{deviceId, type, data}` |
| `ReceiveMuteState` | `{processName, muted}` |
| `ReceiveDeckUpdate` | `{profileId}` |
| `ReceiveLayoutUpdate` | `{rows, columns}` |
| `ReceiveDiscordState` | Connection, mute/deaf, channel, volumes, voice mode, participants |
| `ReceiveObsState` | Connection, current scene, recording/streaming/virtual cam/replay, scenes, audio inputs |
| `ReceiveTuyaState` | Connection, push, devices with status and functions |
| `ReceiveTuyaDeviceState` | `{deviceId, status}` — changed values only |
| `ReceivePlaybackState` | `{playing, title}` — soundboard |

On connect, the new client receives the current Discord, layout, OBS and Tuya state.

## Discovery — UDP 7573

Send the ASCII text `DroidDeckDiscoveryRequest` as a broadcast to port 7573. The server answers with
`{"ip": "...", "name": "<machine name>"}`. Discovery never returns the API key.

## Pairing URI

`droiddeck://pair?ip=<ip>&port=4787&key=<key>`
