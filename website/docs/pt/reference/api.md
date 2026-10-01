# API

O servidor do DroidDeck expõe uma API REST e um hub SignalR na porta `4787`. O app do celular e o
configurador usam os dois, e você pode usá-los para criar seus próprios clientes ou automações.

## Autenticação

Todo endpoint exige a chave de API, exceto `GET /api/ping` e `GET /api/pairing/local-key`.
Envie a chave de uma destas formas:

- header `X-API-KEY: <key>`
- header `Authorization: Bearer <key>`
- query string `?access_token=<key>` (usada no WebSocket do SignalR)

`GET /api/pairing/local-key` só retorna `{key}` para requisições do próprio PC (loopback) e recusa
requisições cross-site do navegador. O CORS só permite origens em loopback e em faixas de rede privada.

Falhas de integração retornam `502` com `{error}`.

## Exemplos

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

| Método | Rota | Descrição |
|---|---|---|
| GET | `profiles` | Lista os perfis. |
| GET | `profiles/{id}` | Retorna um perfil. |
| POST | `profiles` | Cria ou atualiza um perfil. Dispara `ReceiveDeckUpdate`. |
| DELETE | `profiles/{id}` | Apaga um perfil. Dispara `ReceiveDeckUpdate`. |
| POST | `execute` | Executa uma ação (`{type, parameters}`). |
| GET / POST | `layout` | Lê / define o tamanho da grade (`{rows, columns}`). |

### Payload da ação

```json
{ "type": "hotkey", "parameters": { "keys": "^+m" } }
```

Todos os valores de parâmetros são strings.

| `type` | Parâmetros |
|---|---|
| `hotkey` | `keys` |
| `launch_app` | `path`, `arguments`? |
| `activatewindow` | `windowName` (título exato da janela) |
| `media` | `command`: `playpause`·`play`·`pause`·`next`·`previous`·`stop`; `sessionId`? |
| `mixer` | `operation`: `toggleMute`·`mute`·`unmute`·`setVolume`·`volumeUp`·`volumeDown`; `processName` **ou** `deviceId` (`default` ou o id de um endpoint); `deviceKind`: `output`·`input`; `volume` (0–100); `step` (1–50) |
| `multi` | `steps`: string com um array JSON `[{"type","parameters","delayMs"}]` |
| `discord` | `operation`: `toggleMute`·`mute`·`unmute`·`toggleDeafen`·`joinChannel` (`channelId`)·`disconnect`·`inputVolumeUp/Down`·`outputVolumeUp/Down` (`delta`)·`setInputVolume`·`setOutputVolume` (`value`)·`toggleVoiceMode`·`setVoiceMode` (`mode`: `VOICE_ACTIVITY`·`PUSH_TO_TALK`)·`userMute` (`userId`)·`userVolume` (`userId`, `value` 0–200) |
| `obs` | `operation`: `setScene` (`scene`)·`toggleRecord`·`startRecord`·`stopRecord`·`toggleStream`·`toggleVirtualCam`·`toggleReplayBuffer`·`saveReplay`·`toggleInputMute` (`inputName`) |
| `soundboard` | `operation`: `play`·`stop`; `source`: `myinstants` (`id`, `url`, `title`) ou `discord` (`soundId`, `guildId`) |
| `tuya` | `deviceId`, `code`, `operation`: `toggle`·`set`; `value`, `valueType` |

`open_profile` e `back` existem só no celular.

## Áudio — `/api/v1/Mixer`, `/api/v1/Media`

| Método | Rota | Descrição |
|---|---|---|
| GET | `Mixer/out`, `Mixer/in` | Dispositivos de saída / entrada com suas sessões de app. |
| GET | `Mixer/out/{id}`, `Mixer/in/{id}` | Um dispositivo. |
| PUT | `Mixer/out/{id}`, `Mixer/in/{id}` | `{session, volume?, mute?}` — `session` é o id de um processo, ou `-1` para o dispositivo inteiro. |
| GET | `Media/sessions` | Sessões de mídia, com miniaturas. |
| GET | `Media/sessions/{id}` | Uma sessão. |
| POST | `Media/sessions/{id}/command` | `{command}`. |

## Integrações

| Prefixo | Endpoints |
|---|---|
| `/api/discord` | `POST config` `{clientId, clientSecret}` · `POST connect` · `GET state` · `GET guilds` · `GET channels/{guildId}` · `GET voice-channel` · `POST mute` · `POST deafen` · `GET soundboard-sounds` · `POST play-soundboard` |
| `/api/obs` | `POST config` `{host, port, password}` · `POST connect` · `GET state` · `GET scenes` · `GET audio-inputs` |
| `/api/Soundboard` | `GET search?q=` · `GET trending` · `POST play` · `POST stop` · `GET state` · `GET devices` · `GET/POST config` |
| `/api/tuya` | `POST pair/start` `{userCode}` · `POST pair/poll` (410 quando o QR expirou) · `GET state` · `POST connect` · `POST devices/refresh` · `POST command` `{deviceId, code, value}` |

## Hub SignalR — `/deckHub`

Conecte com `?access_token=<key>`. O hub só envia eventos; os comandos passam pelo REST.

| Evento | Payload |
|---|---|
| `ReceiveSystemStats` | `{cpuUsage, ramUsage, ramTotal, ramAvailable, netUpKBps, netDownKBps, gpuUsage}` — a cada segundo |
| `ReceiveMediaStatus` | `{playing}` |
| `ReceiveMediaState` | `{sessionId, command, playing}` |
| `ReceiveVolumeChange` | `{deviceId, type, data}` |
| `ReceiveMuteState` | `{processName, muted}` |
| `ReceiveDeckUpdate` | `{profileId}` |
| `ReceiveLayoutUpdate` | `{rows, columns}` |
| `ReceiveDiscordState` | Conexão, mudo/ensurdecido, canal, volumes, modo de voz, participantes |
| `ReceiveObsState` | Conexão, cena atual, gravação/transmissão/câmera virtual/replay, cenas, entradas de áudio |
| `ReceiveTuyaState` | Conexão, push, dispositivos com status e funções |
| `ReceiveTuyaDeviceState` | `{deviceId, status}` — só os valores alterados |
| `ReceivePlaybackState` | `{playing, title}` — soundboard |

Ao conectar, o novo cliente recebe o estado atual do Discord, do layout, do OBS e da Tuya.

## Descoberta — UDP 7573

Envie o texto ASCII `DroidDeckDiscoveryRequest` em broadcast para a porta 7573. O servidor responde
com `{"ip": "...", "name": "<machine name>"}`. A descoberta nunca retorna a chave de API.

## URI de pareamento

`droiddeck://pair?ip=<ip>&port=4787&key=<key>`
