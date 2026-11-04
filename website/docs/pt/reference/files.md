# Arquivos e configurações

## Dados no PC

**`%AppData%\DroidDeck\`** (Roaming) — seus decks:

| Arquivo | Conteúdo |
|---|---|
| `Profiles\<id>.json` | Um arquivo por perfil: nome e botões. Ícones personalizados ficam embutidos em base64. |
| `layout.json` | Tamanho da grade informado pelo celular (`rows`, `columns`). |
| `counters.json` | Valores dos botões de contador, para sobreviverem a um reinício. |
| `toggles.json` | Botões de dois estados ligados. |
| `triggers.json` | [Automações](../guide/automations.md). |

**`%LocalAppData%\DroidDeck\`** — chaves e integrações:

| Arquivo | Conteúdo |
|---|---|
| `apikey` | A chave de pareamento. Apague-a para revogar todos os celulares pareados. |
| `discord.json` | Client ID, Client Secret e tokens do Discord. |
| `obs.json` | Host, porta e senha do OBS. A presença dele ativa a conexão automática com o OBS. |
| `soundboard.json` | Dispositivos de saída e volume do soundboard. |
| `SoundCache\*.mp3` | Sons baixados do MyInstants (até 5 MB cada). Limitado a 200 MB: os tocados há mais tempo saem primeiro. Pode apagar sem problema. |
| `tuya.json` | Código de usuário da Tuya, token de sessão e registro do app (`ClientId`, `Schema`). |
| `homeassistant.json` | Endereço e token de acesso do Home Assistant. |
| `spotify.json`, `twitch.json`, `youtube.json` | Client ID (e o Client Secret do YouTube) e os tokens da conta. |
| `hue.json` | IP do Hue Bridge e o usuário que ele deu ao DroidDeck no pareamento. |
| `mqtt.json` | Endereço, usuário e senha do broker MQTT. |
| `streamerbot.json` | Endereço e senha do WebSocket Server do Streamer.bot. |
| `vtubestudio.json` | Endereço da API do VTube Studio e o token do plugin. |
| `vmix.json`, `companion.json` | Endereço do vMix; endereço, grade e id de aparelho do Companion. |
| `mairlist.json`, `radiodj.json` | Endereço, usuário e senha do mAirList; endereço e senha do plugin REST Server do RadioDJ. |
| `usb.json` | Se a [conexão USB](../getting-started/browser-usb.md#usb) está ligada (`Enabled`). Sem o arquivo, ela só fica ligada quando um `adb` do Android SDK é encontrado. |
| `platform-tools\` | As platform-tools do Google (`adb`), quando o DroidDeck as baixou para a conexão USB. Pode apagar com o DroidDeck fechado; ligar a conexão USB de novo baixa outra vez. |
| `scripts\` | [Scripts](../guide/actions.md#script) escritos num botão, salvos como arquivo para rodar (um por conteúdo). Pode apagar; eles são recriados. |

**`%LocalAppData%\DroidDeck.log`** — o arquivo de log.

**`%LocalAppData%\DroidDeckPC\`** — o programa em si, quando instalado pelo `DroidDeck-Setup.exe`.
As atualizações substituem esta pasta; seus dados ficam nas pastas acima e são mantidos.

!!! info "Os segredos são cifrados"
    `apikey` e os arquivos das integrações (`discord.json`, `obs.json`, `tuya.json`,
    `homeassistant.json`, `spotify.json`, `twitch.json`, `youtube.json`, `hue.json`, `mqtt.json`,
    `streamerbot.json`, `vmix.json`, `companion.json`, `mairlist.json`, `radiodj.json`, `vtubestudio.json`) são cifrados com a DPAPI do Windows e só podem
    ser lidos pela sua conta do Windows neste PC. Arquivos de versões antigas, em texto puro, são
    cifrados automaticamente na primeira leitura. Para editar um deles à mão (ex.: o `ClientId` da
    Tuya), salve-o como JSON puro; o DroidDeck cifra de novo na próxima inicialização.

## Backup e troca de PC

Copie `%AppData%\DroidDeck\Profiles\` para guardar seus decks. Os segredos ficam presos a este PC e
à sua conta do Windows, então num PC novo você configura as integrações e pareia os celulares de novo.

## Opções de linha de comando

| Opção | Efeito |
|---|---|
| *(nenhuma)* | App na bandeja + servidor. |
| `--headless` / `--no-tray` | Só o servidor, sem ícone na bandeja. Pare com Ctrl+C. |
| `--print-pairing` | Imprime a URI de pareamento, salva o QR em `%TEMP%\droiddeck-pair.png` e sai. |

| Variável de ambiente | Efeito |
|---|---|
| `ASPNETCORE_URLS` | Muda o endereço em que o servidor escuta (padrão `http://0.0.0.0:4787`). O QR code e o redirect do Discord continuam usando a porta 4787. |
| `ASPNETCORE_ENVIRONMENT=Development` | Ativa o Swagger em `/swagger` e páginas de erro detalhadas. |

## `appsettings.json`

Fica ao lado do `DroidDeck.exe`:

| Chave | Padrão | Significado |
|---|---|---|
| `EnableDiscovery` | `true` | Responde a pedidos de descoberta UDP na porta 7573. |
| `EnableSoftwareActivation` | `false` | Ativa o endpoint legado `/api/Software/activate`. |
| `AllowedTargets` | *(vazio)* | Nomes de processo, separados por vírgula, permitidos pelos endpoints legados de mudo `/api/Software`. |
