# Evolução do DroidDeck: melhorias e integrações (out/2026)

Análise feita sobre o código da v1.5.0. As referências são caminhos relativos à raiz do repo.

## A) Melhorias no que já existe

### 1. Bugs e riscos de robustez

- **Cache da soundboard nunca acerta entre execuções.** `RaspDeck/Services/SoundboardService.cs:187` usa `url.GetHashCode()`, que muda a cada processo no .NET 8. Quando o id não é "seguro", o mp3 é baixado de novo a cada boot e a pasta de cache só cresce. O download não tem limite de tamanho nem checagem de content-type. Correção: SHA-256 da URL e teto de MB.
- **Discovery depende de internet e de IP fixo.** `RaspDeck/Lib/DiscoveryServer.cs:27` (e `Lib/NetworkInfo.cs:17`) descobre o IP com `Connect("8.8.8.8")` uma única vez. Num PC sem rota padrão isso lança exceção e derruba o discovery. Se o DHCP trocar o IP, ele passa a anunciar o endereço antigo. Correção: responder com o IP da interface que recebeu o pacote, calculado a cada request.
- **Gravação não atômica.** `StreamDeckConfigService.cs` e `ObsService.cs` usam `File.WriteAllText` direto. Uma queda no meio corrompe o JSON, e o perfil some sem aviso no load. Correção: gravar em `.tmp` e depois `File.Move`, como a soundboard já faz.
- **Save falha em silêncio.** Com Id inválido, `SaveProfile` só loga, e o controller responde `200 OK`.
- **Hotkeys frágeis.** `AppActivator.cs` usa `SendKeys.SendWait`. Ele não envia a tecla Win, é ignorado por jogos DirectInput/RawInput e roda numa thread do pool. Trocar por `SendInput` com scancodes.
- **Ativar janela frágil.** Só encontra a janela pelo título exato, e o foreground lock do Windows bloqueia `SetForegroundWindow`.
- **Multi-ação sem controle.** Não há cancelamento, nem "parar se um passo falhar", nem feedback de erro para o celular. O `/execute` sempre responde 200.
- **OneSignal sobrando do template.** `app/lib/main.dart:20-22` inicializa o OneSignal dentro do `build()`, com log `verbose` em release, e pede permissão de notificação sem uso real. Deve ser removido (também bloqueia o F-Droid).
- **Código morto.** `Lib/DBHelper.cs`, junto com o pacote `LocalStorage`, e o `SoftwareController` legado.

### 2. Segurança

- **A API key equivale a execução remota de código.** `EnableSoftwareActivation:false` só bloqueia `/api/software/activate`, mas `POST /api/streamdeck/execute` aceita qualquer `DeckAction` vinda do cliente, inclusive `launchApp` com `UseShellExecute` e `hotkey`. Duas opções: executar **por id de botão** (o servidor busca a ação no perfil salvo) ou criar uma allowlist com toggle "permitir launchApp/script".
- **HTTP puro na LAN.** A chave vai no header e na query `access_token` do WebSocket. Quem estiver no mesmo Wi-Fi e capturar uma requisição ganha a chave. Proposta: certificado autoassinado gerado no primeiro boot, com o fingerprint no QR de pareamento e pinning no app.
- **Uma chave única e eterna.** Não há rotação nem revogação. Proposta: uma chave por celular, lista de dispositivos pareados e botão "revogar".
- **Segredos em texto puro** (`discord.json`, `obs.json`, `tuya.json`). Proteger com DPAPI (`ProtectedData`, CurrentUser).
- **Chave no celular em `SharedPreferences`.** Migrar para `flutter_secure_storage`.
- O que já está bom: CORS restrito à LAN, `Sec-Fetch-Site` no `local-key`, comparação em tempo constante e proteção contra path traversal.

### 3. Arquitetura

- **Classes que fazem tudo:**
  - `ActionExecutorService`: switch gigante e 9 serviços concretos injetados;
  - `DiscordRpcService`: ~665 linhas;
  - `app/.../button_property_panel.dart`: ~2100 linhas, com `if (tipo == 'x')` espalhados.
- **Adicionar um tipo de ação hoje toca ~9 pontos em 2 linguagens:** service, autoconnect, `Program.cs`, construtor e switch do executor, controller, `DeckHub.OnConnectedAsync`, painel de propriedades, tela de settings e renderização dinâmica.
- **Proposta de plugins em 2 etapas:**
  1. **Interno:** `IActionProvider { Type; Execute(params); Describe() -> ActionSchema; State }` registrado via DI, mais um `GET /api/actions/schema`. O Flutter gera o formulário a partir do schema, o que elimina ~4 dos 9 pontos.
  2. **Externo:** plugins fora do processo falando WebSocket, no estilo do SDK da Elgato.
- **Modelo de dados com tipagem fraca.** `Parameters` é `Dictionary<string,string>`, e a multi-ação guarda JSON dentro de string. Adicionar `schemaVersion` ao perfil para permitir migrações.

### 4. Lacunas de UX em relação ao Elgato Stream Deck

| Recurso | Hoje | Proposta |
|---|---|---|
| Estados/toggle | Só `ActiveColor` | Estado 0/1 com ícone e label por estado |
| Long-press / duplo toque | Long-press abre o editor; o modelo tem um `// Future: LongPressAction` | `LongPressAction`, `DoubleTapAction` |
| Pastas | `open_profile`, sem "voltar" | Subperfil com botão voltar fixo e pilha de navegação |
| Perfil por app em foco | Não existe | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` + regras processo → perfil |
| Backup | Não existe | Export/import `.droiddeck` (zip de perfis e ícones) |
| Vários celulares | Grade global | Grade e perfil por dispositivo |
| Haptics | Nenhum | `HapticFeedback.lightImpact` |
| Offline | Sem cache | Último perfil em cache + estado "desconectado" |
| Ícones | Material + base64 | Packs de ícones, busca, ícone do .exe |

### 5. Distribuição

- **Hoje:** zip self-contained + APK. Não há instalador, assinatura nem auto-update.
- **Instalador:** Velopack (com auto-update via GitHub Releases) ou Inno Setup. Ele também criaria a regra de firewall para 4787/TCP e 7573/UDP, uma causa comum de "não conecta".
- **Windows:** winget e assinatura de código (Azure Trusted Signing), que acaba com o aviso do SmartScreen.
- **Android:** Play Store (track interno) ou Obtainium/F-Droid.
- **Diagnóstico:** handler global de exceção e item "exportar diagnóstico" na bandeja.

### 6. Testes e CI

- **O CI não roda testes.** Só existe o workflow manual de release.
- **Dois projetos de teste:** `RaspDeck/DroidDeck.Tests` cobre só os modelos. `tests/DroidDeck.Tests` tem testes de integração, mas **está fora da `.sln`**.
- **Sem cobertura:** `ActionExecutorService`, autenticação (401/403), CORS, `StreamDeckConfigService`.
- **Proposta:** `ci.yml` em PR/push com `dotnet test`, `flutter analyze` e `flutter test`.

## B) Integrações novas (ordenadas por valor/esforço)

| # | Integração | O que permite | Abordagem | Esforço |
|---|---|---|---|---|
| 1 | HTTP/Webhook | Qualquer API (n8n, IFTTT, HA) | `HttpClient`: método, URL, headers, body | P |
| 2 | Script/PowerShell | Automações livres | `Process` com timeout; atrás de um toggle de segurança | P |
| 3 | Timer/relógio/Pomodoro/contador | Botões dinâmicos | Estado no backend + `DynamicType` | P |
| 4 | OBS completo | Mostrar/ocultar fonte, filtros, transição, marcador de capítulo, screenshot | `SetSceneItemEnabled`, `SetSourceFilterEnabled`, `CreateRecordChapter` | P–M |
| 5 | Home Assistant | Qualquer casa inteligente; contorna o problema do app de marca da Tuya | REST + token de longa duração + WebSocket `subscribe_events` | M |
| 6 | Windows | Trocar dispositivo de áudio padrão, desktops virtuais, travar/suspender | `IPolicyConfig` (COM), lib VirtualDesktop, `LockWorkStation` | P–M |
| 7 | Mute no Teams/Zoom/Meet | Home office | Teams: API local de dispositivos de terceiros (WebSocket, com estado). Zoom/Meet: hotkey | M |
| 8 | MQTT genérico | Zigbee2MQTT, Tasmota, ESPHome | `MQTTnet` **já é dependência** | P |
| 9 | Elgato Key Light / Philips Hue | Iluminação | Key Light: REST `:9123`. Hue: API local v2 | P cada |
| 10 | Steam | Abrir jogos com ícone | `steam://rungameid/` + `libraryfolders.vdf` | P |
| 11 | Voicemeeter | Rotas e mute de bus/strip | `VoicemeeterRemote64.dll` via P/Invoke | M |
| 12 | Twitch | Marcador, clip, título, anúncio | Helix + OAuth device flow + EventSub | M |
| 13 | Spotify | Curtir, playlist, faixa atual | Web API com OAuth PKCE | M |
| 14 | Streamlabs Desktop | Cenas, gravação | API WebSocket com token | M |
| 15 | Prompt de IA | Resumir/traduzir o clipboard e colar | HTTP + clipboard + `SendInput` | M |
| 16 | OpenRGB, clima (Open-Meteo), VS Code/GitHub, YouTube Live | Nichos | Diversos | P–M |

## C) Roadmap sugerido

**Fase 1: ganhos rápidos**
- **Segurança:** `/execute` por id de botão (ou allowlist), DPAPI nos segredos, botão "regenerar chave", remover o OneSignal.
- **Correções:** hash estável no cache da soundboard, discovery sem 8.8.8.8, gravação atômica, `SendInput`.
- **CI:** `ci.yml` com testes, mais os testes consolidados na sln.
- **App:** haptics e perfil offline.
- **Integrações:** HTTP/Webhook, Timer/Pomodoro, Steam, Key Light e a extensão do OBS.

**Fase 2: próximo passo**
- `IActionProvider` + UI gerada por schema, quebrando o `button_property_panel.dart`.
- **UX:** estados/toggle, long-press, pastas com "voltar", import/export, perfil por app em foco.
- **Distribuição:** instalador com auto-update e firewall, winget, assinatura.
- **Integrações:** Home Assistant, MQTT, script, troca de dispositivo de áudio, Teams, Voicemeeter.

**Fase 3: apostas maiores**
- TLS com pinning, chave por dispositivo, vários celulares.
- Twitch, Spotify, Streamlabs.
- Plugins fora do processo com SDK documentado.
- Play Store, iOS e layouts para tablet.
