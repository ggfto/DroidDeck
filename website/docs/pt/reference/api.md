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

## Ações privilegiadas

**Atalho de teclado**, **abrir programa**, **ativar janela** e **script** executam código no PC, e a **requisição HTTP**
sai com o acesso de rede do PC, então ficam protegidas até de quem tem a chave. Requisições que não vêm do próprio PC (loopback) não podem:

- executá-las pelo `execute`; use o `press` com um botão salvo no PC;
- criar um botão com uma dessas ações, ou mudar os parâmetros dela, pelo `POST profiles`. Mover,
  renomear, mudar a cor e apagar esses botões continua liberado.

Uma multi-ação conta como privilegiada se qualquer passo dela for. Requisições recusadas retornam
`403` com `{error}`.

Conexões de um celular pelo [cabo USB](../getting-started/browser-usb.md#usb) chegam pelo loopback,
através do `adb`; o servidor as reconhece e as trata como remotas, não como o PC.

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

## Deck: `/api/StreamDeck` { #deck-apistreamdeck }

| Método | Rota | Descrição |
|---|---|---|
| GET | `profiles` | Lista os perfis. |
| GET | `profiles/{id}` | Retorna um perfil. |
| POST | `profiles` | Cria ou atualiza um perfil. Dispara `ReceiveDeckUpdate`. |
| DELETE | `profiles/{id}` | Apaga um perfil. Dispara `ReceiveDeckUpdate`. |
| POST | `press` | Aperta um botão salvo (`{profileId, buttonId, gesture}`; `gesture` é `tap` (padrão), `longPress` ou `doubleTap`): o servidor executa a ação gravada no perfil para esse gesto. É o que o celular usa. |
| POST | `execute` | Executa uma ação avulsa (`{type, parameters}`). De fora do PC, ações privilegiadas retornam `403`. |
| GET / POST | `layout` | Lê / define o tamanho da grade (`{rows, columns}`). |

### Payload da ação

```json
{ "type": "hotkey", "parameters": { "keys": "^+m" } }
```

Todos os valores de parâmetros são strings. A tabela abaixo é um resumo; a fonte oficial, legível
por máquina, é o [`GET /api/actions/schema`](#action-types-apiactions), que lista cada tipo de ação
com os parâmetros (`fields[].key`), os valores aceitos (`options`) e quando cada campo vale
(`visibleWhen`).

| `type` | Parâmetros |
|---|---|
| `hotkey` | `keys` |
| `launch_app` | `path`, `arguments`? |
| `activatewindow` | `windowName` (título exato da janela) |
| `media` | `command`: `playpause`·`play`·`pause`·`next`·`previous`·`stop`; `sessionId`? |
| `mixer` | `operation`: `toggleMute`·`mute`·`unmute`·`setVolume`·`volumeUp`·`volumeDown`; `processName` **ou** `deviceId` (`default` ou o id de um endpoint); `deviceKind`: `output`·`input`; `volume` (0–100); `step` (1–50) |
| `multi` | `steps`: string com um array JSON `[{"type","parameters","delayMs","if"}]`; `if` é opcional: `{check, value, negate}`, com `check` entre `obs_streaming`·`obs_recording`·`discord_mute`·`discord_deaf`·`toggle_on` (`value` = id do botão)·`app_running`·`app_focused` (`value` = processo)·`time_between` (`value` = `08:00-18:00`)·`weekday` (`value` = `1-5`, 0 = domingo) |
| `discord` | `operation`: `toggleMute`·`mute`·`unmute`·`toggleDeafen`·`joinChannel` (`channelId`)·`disconnect`·`inputVolumeUp/Down`·`outputVolumeUp/Down` (`delta`)·`setInputVolume`·`setOutputVolume` (`value`)·`toggleVoiceMode`·`setVoiceMode` (`mode`: `VOICE_ACTIVITY`·`PUSH_TO_TALK`)·`userMute` (`userId`)·`userVolume` (`userId`, `value` 0–200) |
| `obs` | `operation`: `setScene` (`scene`)·`toggleRecord`·`startRecord`·`stopRecord`·`toggleStream`·`toggleVirtualCam`·`toggleReplayBuffer`·`saveReplay`·`toggleInputMute` (`inputName`) |
| `soundboard` | `operation`: `play`·`stop`; `source`: `myinstants` (`id`, `url`, `title`) ou `discord` (`soundId`, `guildId`) |
| `tuya` | `deviceId`, `code`, `operation`: `toggle`·`set`; `value`, `valueType` |
| `homeassistant` | `operation`: `toggle`·`turn_on`·`turn_off`·`brightness` (`step`)·`scene`·`script`·`display`·`service` (`service` = `domínio.serviço`, `data` em JSON); `entityId` |
| `spotify` | `operation`: `playPause`·`next`·`previous`·`like`·`shuffle`·`repeat`·`volume` (`step`)·`playlist` (`uri`, `shuffleOn`)·`nowPlaying` |
| `twitch` | `operation`: `marker` (`description`)·`clip`·`ad` (`length`)·`chat` (`message`)·`poll` (`question`, `choices` uma por linha, `duration`)·`title` (`title`)·`status` |
| `youtube` | `operation`: `chat` (`message`)·`status` |
| `network` | `protocol`: `osc` (`path`, `args`: `i:1`·`f:0.5`·`s:texto`·`T`·`F`, um por linha)·`udp`·`tcp` (`message`, `encoding`: `text`·`hex`, `ending`: `none`·`lf`·`crlf`·`cr`); `host`, `port`. Privilegiada |
| `tally` | `operation`: `open`·`show` (`target`, da lista em `GET /api/tally/targets`)·`manual_toggle`·`manual_on`·`manual_off` |
| `vmix` | `operation`: `cut`·`fade` (`duration`)·`preview`·`program`·`quickplay`·`toggle_audio` (`input`)·`set_volume` (`input`, `volume`)·`overlay` (`input`, `overlay`)·`overlay_off`·`master_audio`·`master_volume` (`volume`)·`toggle_record`·`toggle_stream`·`toggle_external`·`toggle_multicorder`·`fade_to_black`·`snapshot`·`custom` (`function`, `query`) |
| `companion` | Só o app abre a superfície |
| `mairlist` | `operation`: `on_air_toggle`·`on_air`·`off_air`·`automation_toggle`·`automation_on`·`automation_off`·`next` (`playlist`)·`player_toggle`·`player_start`·`player_stop`·`player_fadeout` (`playlist`, `player`)·`cart` (`cart`)·`cart_stop_all`·`custom` (`command`) |
| `radiodj` | `operation`: `play_track` (`track`, a partir de 1)·`stop`·`pause`·`resume`·`restart`·`play_intro`·`autodj_toggle`·`assisted_toggle`·`input_toggle`·`events_on`·`events_off`·`cart` (`cart`)·`load_playlist`·`load_track` (`id`)·`custom` (`command`, `arg`) |
| `streamerbot` | `action` (id ou nome), `args` (`chave=valor` por linha, ou JSON) |
| `vtubestudio` | `operation`: `hotkey` (`hotkey`: id ou nome)·`model` (`model`: id do modelo) |
| `voicemeeter` | `operation`: `toggle_mute`·`mute`·`unmute`·`set_gain` (`gain` dB)·`adjust_gain` (`step` dB) com `target`: `strip`·`bus` e `index` (1–8)·`macro` (`button`)·`param_set` (`parameter`, `value`)·`param_toggle` (`parameter`) |
| `hue` | `operation`: `toggle`·`on`·`off`·`brightness` (`brightness` %)·`brightness_step` (`step` %) com `target`: `light`·`group` e `id`·`scene` (`scene`) |
| `keylight` | `host` (IP ou IP:porta); `operation`: `toggle`·`on`·`off`·`brightness` (`brightness`)·`brightness_step` (`step`)·`temperature` (`kelvin`)·`temperature_step` (`kelvinStep`) |
| `govee` | `ip`; `operation`: `toggle`·`on`·`off`·`brightness` (`brightness`)·`color` (`color` #RRGGBB)·`temperature` (`kelvin`) |
| `mqtt` | `topic`, `payload`, `qos` (0–2), `retain` (`true`/`false`) |
| `script` | `source`: `inline` (`language`: `powershell`·`python`·`cmd`; `code`) ou `file` (`path`); `arguments` |
| `http` | `method`: `POST`·`GET`·`PUT`·`PATCH`·`DELETE`; `url`; `headers` (um `Nome: valor` por linha); em POST, PUT e PATCH, `contentType` (`application/json`·`application/x-www-form-urlencoded`·`text/plain`) e `body` |
| `timer` | `mode`: `countdown` (`durationMin`)·`stopwatch`·`pomodoro` (`workMin`, `breakMin`)·`counter` (`step`); `operation`: `toggle`·`reset`·`skip`·`increment`·`decrement`; `target`: `self` ou o id de outro botão de timer |
| `windows` | `operation`: `setDefaultAudio` (`deviceKind`: `output`·`input`; `deviceId`; `altDeviceId` para alternar entre dois, ou `none`)·`lock`·`sleep`·`displayOff` |
| `steam` | `appId`: o id numérico do app na Steam (de `GET /api/steam/games`) |

`open_profile` e `back` existem só no celular.

## Áudio: `/api/v1/Mixer`, `/api/v1/Media` { #audio }

| Método | Rota | Descrição |
|---|---|---|
| GET | `Mixer/out`, `Mixer/in` | Dispositivos de saída / entrada com suas sessões de app. |
| GET | `Mixer/out/{id}`, `Mixer/in/{id}` | Um dispositivo. |
| PUT | `Mixer/out/{id}`, `Mixer/in/{id}` | `{session, volume?, mute?}`: `session` é o id de um processo, ou `-1` para o dispositivo inteiro. |
| GET | `Media/sessions` | Sessões de mídia, com miniaturas. |
| GET | `Media/sessions/{id}` | Uma sessão. |
| POST | `Media/sessions/{id}/command` | `{command}`. |

Um perfil é `{id, name, rows, columns, isDefault, buttons, autoSwitchApps}`; um botão tem `action`,
`longPressAction` e `doubleTapAction` (cada um `{type, parameters}` ou `null`). A regra das ações
privilegiadas vale para os três.

Botões de dois estados acrescentam `toggle` (bool), `activeAction` (roda no toque que desliga),
`activeLabel`, `activeIconName` e `activeIconBase64`. Botões de informação usam `dynamicType`
(`cpu_monitor`, `memory_monitor`, `gpu_monitor`, `network_monitor`, `clock`, `weather`, `now_playing`,
`command`, `url`, `notifications`, `volume_slider`, `ai_usage`) com `dynamicParameters` (`city`, `units`,
`command`, `url`, `path`, `format`, `interval`, `app`, `target`, `provider`, `show`…); `command` e `url` seguem a regra das
ações privilegiadas. Botões grandes acrescentam `columnSpan` e `rowSpan` (padrão 1; `row`/`column` é a
casa do canto superior esquerdo).

| Método | Caminho | Descrição |
|---|---|---|
| GET | `/api/slider?target=…` | Controle deslizante: `{volume, mute, label, max, text}` atual. `target`: `output`, `input`, `app` (`app`), `obs` (`inputName`), `discord_user` (`userId`), `monitor`, `media`, `tuya` (`deviceId`), `homeassistant` (`entityId`), `hue` (`hueTarget`, `id`), `govee` (`ip`), `keylight` (`host`). `max` é 100, exceto Discord (200) e música (duração em segundos). `404` quando o alvo não existe no momento; `503 {error}` quando a integração não responde. |
| PUT | `/api/slider` | `{target, app, params, volume}` (0 até `max`). Devolve o estado novo. |
| GET | `/api/slider/options?target=tuya\|discord_user` | Lista `{value, label}` de lâmpadas Smart Life com brilho ou de pessoas na call. |

## Biblioteca: backup, Steam, apps { #library }

| Método | Caminho | Descrição |
|---|---|---|
| GET | `/api/backup` | Baixa todos os perfis num `.droiddeck` (zip). |
| POST | `/api/backup` | Restaura um `.droiddeck` (corpo = o arquivo). Perfis com o mesmo id são substituídos. De fora do PC, recusa com `403` se for criar botões privilegiados. |
| GET | `/api/steam/games` | Jogos instalados na Steam (`{value: appId, label: nome}`). |
| GET | `/api/steam/icon/{appId}` | `{iconBase64}`: PNG quadrado recortado da capa do jogo. |
| GET | `/api/system/apps` | Programas com janela visível (para a troca automática de perfil). |
| GET | `/api/system/info` | `{name, version}`: o nome do PC no Windows, usado para identificar os PCs pareados. |
| GET | `/api/info` | Valores atuais dos botões de informação, o que está tocando e os botões de dois estados ligados. |
| GET / POST | `/api/macro`, `/api/macro/start`, `/api/macro/stop` | Gravação de macro pelo teclado. Só do próprio PC (senão `403`); `stop` devolve `{steps}` no formato da multiação. |
| POST | `/api/import/streamdeck` | Importa um `.streamDeckProfile` (corpo = o arquivo, até 50 MB) como perfis novos. Devolve `{profiles, buttons, unsupported, stripped, firstProfileId}`; de fora do PC, botões privilegiados entram sem a ação e `stripped` conta quantos. `400` com `{error}` se não for um perfil do Stream Deck. |
| GET | `/api/share/{profileId}` | `{data, url, qr, images}`: o perfil comprimido num link (`url` é a página de compartilhamento do site com `#data`); `qr` diz se cabe num QR code; `images`, se imagens próprias ficaram de fora. |
| GET | `/api/share/{profileId}/qr` | O link de compartilhamento como QR code em PNG. |
| POST | `/api/share/import` | `{data}` de um link compartilhado → salvo como perfil novo. Devolve `{profileId, name, buttons, stripped}`; mesma regra das ações privilegiadas da importação do Stream Deck. |

## Automações: `/api/triggers` { #automations }

| Método | Caminho | Descrição |
|---|---|---|
| GET | `/api/triggers` | A lista de automações. |
| PUT | `/api/triggers` | Troca a lista inteira. Devolve a lista salva. |
| POST | `/api/triggers/{id}/run` | Aperta o botão da automação agora. `404` se a automação ou o botão não existir. |

Uma automação é `{id, name, enabled, event, time, days, app, profileId, buttonId, if}`. `event` é
`schedule` (`time` `HH:mm`, `days` como `1-5`), `app_start` / `app_exit` (`app`), `resume` ou
`startup`; `if` é uma condição opcional no formato da multiação. Como as automações só apertam botões
salvos, elas seguem as regras com que esses botões foram criados.

## Tipos de ação: `/api/actions` { #action-types-apiactions }

| Método | Caminho | Descrição |
|---|---|---|
| GET | `schema` | Todos os tipos de ação, com título, ícone, categoria e campos. O editor monta o formulário a partir disto. |
| GET | `state/{type}` | Estado ao vivo atual de um tipo (o mesmo conteúdo do evento SignalR dele). |
| GET | `timer/targets` | Botões de timer que outro botão pode controlar. |
| GET | `windows/audio-devices?kind=output\|input` | Dispositivos de áudio ativos (`{value, label}`). |

O schema é a fonte oficial dos parâmetros das ações: o editor do app monta os formulários a partir
dele, então um tipo de ação ou campo novo aparece aqui primeiro. Os textos seguem o
`Accept-Language`. Uma resposta resumida:

```json
[
  {
    "type": "steam",
    "title": "Jogo da Steam",
    "icon": "sports_esports",
    "category": "Sistema",
    "privileged": false,
    "stateful": false,
    "editor": "steam",
    "stateEvent": null,
    "fields": [
      {
        "key": "appId",
        "label": "Jogo",
        "kind": "select",
        "required": true,
        "optionsSource": "/api/steam/games",
        "visibleWhen": null
      }
    ]
  }
]
```

## Integrações

| Prefixo | Endpoints |
|---|---|
| `/api/discord` | `POST config` `{clientId, clientSecret}` · `POST connect` · `GET state` · `GET guilds` · `GET channels/{guildId}` · `GET voice-channel` · `POST mute` · `POST deafen` · `GET soundboard-sounds` · `POST play-soundboard` |
| `/api/obs` | `POST config` `{host, port, password}` · `POST connect` · `GET state` · `GET scenes` · `GET audio-inputs` · `GET sources` · `GET scene-items?scene=` · `GET filters?source=` |
| `/api/Soundboard` | `GET search?q=` · `GET trending` · `POST play` · `POST stop` · `GET state` · `GET devices` · `GET/POST config` |
| `/api/tuya` | `POST pair/start` `{userCode}` · `POST pair/poll` (410 quando o QR expirou) · `GET state` · `POST connect` · `POST devices/refresh` · `POST command` `{deviceId, code, value}` |
| `/api/govee` | `GET devices`: procura na rede os aparelhos Govee com Controle LAN (cerca de 2 s); lista de `{value: ip, label}` |
| `/api/tally` | `GET targets`: o que a tela de tally pode acompanhar agora, em `{value, label}` |
| `/api/companion` | `GET keys`: `{connected, columns, rows, keys}`, com as teclas atuais da superfície |
| `/api/integrations/{nome}` | `homeassistant`, `spotify`, `twitch`, `youtube`, `hue`, `mqtt`, `streamerbot`, `vtubestudio`, `vmix`, `companion`, `mairlist`, `radiodj`. `GET` estado `{connected, configured, account, message, loginPending, userCode, verificationUri, redirectUri, fields}` · `POST` os campos da configuração (conecta ou abre o login no navegador do PC) · `DELETE` esquece a conta · `GET options/{lista}` (`entities?operation=`, `playlists`, Hue `items?target=`·`scenes`, Streamer.bot `actions`, vMix `inputs`, VTube Studio `hotkeys`·`models`) · `GET callback` (retorno do OAuth, sem chave da API) |

As respostas seguem o `Accept-Language` do pedido: com `en`, tipos de ação, rótulos dos campos e
mensagens de erro vêm em inglês.

## Hub SignalR: `/deckHub` { #signalr-hub }

Conecte com `?access_token=<key>`. O hub só envia eventos; os comandos passam pelo REST. A exceção é
`SetLanguage("en")`, que troca o idioma dos textos que o PC monta (erros dos botões de informação).
O idioma inicial vem de `?lang=pt|en|es` ou, sem ele, do `Accept-Language`.

| Evento | Payload |
|---|---|
| `ReceiveSystemStats` | `{cpuUsage, ramUsage, ramTotal, ramAvailable, netUpKBps, netDownKBps, gpuUsage, disks}`: a cada segundo. `disks` é `[{name: "C:", label, totalBytes, freeBytes}]` e é lido a cada 30 s |
| `ReceiveMediaStatus` | `{playing}` |
| `ReceiveMediaState` | `{sessionId, command, playing}` |
| `ReceiveVolumeChange` | `{deviceId, type, data}` |
| `ReceiveMuteState` | `{processName, muted}` |
| `ReceiveDeckUpdate` | `{profileId}` |
| `ReceiveLayoutUpdate` | `{rows, columns}` |
| `ReceiveDiscordState` | Conexão, mudo/ensurdecido, canal, volumes, modo de voz, participantes |
| `ReceiveObsState` | Conexão, cena atual, gravação/transmissão/câmera virtual/replay, cenas, entradas de áudio |
| `ReceiveTuyaState` | Conexão, push, dispositivos com status e funções |
| `ReceiveTuyaDeviceState` | `{deviceId, status}`: só os valores alterados |
| `ReceiveHueState`, `ReceiveMqttState`, `ReceiveStreamerBotState`, `ReceiveVTubeStudioState` | `{connected}` (o VTube Studio manda também `model`) |
| `ReceiveTimerState` | `{buttonId: {mode, running, finished, elapsedMs, durationMs, count, phase, cycles}}`: a cada mudança |
| `ReceiveWindowsState` | `{defaultOutput, defaultInput}`: ids dos dispositivos de áudio padrão |
| `ReceiveAutoProfile` | `{profileId, app}`: perfil do app em foco; `profileId` nulo quando o app não tem perfil |
| `ReceivePlaybackState` | `{playing, title}`: soundboard |
| `ReceiveButtonToggles` | `{buttonId: true}`: botões de dois estados ligados |
| `ReceiveInfoState` | `{buttonId: {text, sub, code, isDay, error}}`: botões de clima, comando e URL |
| `ReceiveNowPlaying` | `{title, artist, playing, thumbnailBase64}` |
| `ReceiveHomeAssistantState` | `{connected, entities: {id: {state, name, unit, brightness, color}}}`: entidades usadas nos botões |
| `ReceiveSpotifyState` | `{connected, account, playing, track, artist, liked, shuffle, repeat, volume, device, coverBase64}` |
| `ReceiveTwitchState`, `ReceiveYouTubeState` | `{connected, account, live, viewers, title}` |

Ao conectar, o novo cliente recebe o estado atual do Discord, do layout, do OBS e da Tuya.

## Descoberta: UDP 7573 { #discovery }

Envie o texto ASCII `DroidDeckDiscoveryRequest` em broadcast para a porta 7573. O servidor responde
com `{"ip": "...", "name": "<machine name>"}`. A descoberta nunca retorna a chave de API.

## URI de pareamento

`droiddeck://pair?ip=<ip>&port=4787&key=<key>`
