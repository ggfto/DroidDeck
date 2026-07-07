# Inspeção DroidDeck — Backend C# + App Flutter (2026-07-07)

Varredura de: **consumo de RAM (backend)**, **plugin Discord**, e **bugs críticos / melhorias gerais** nos dois projetos.
Achados marcados com ✅ foram verificados diretamente na fonte durante a inspeção; os demais vêm da análise dos serviços e devem ser confirmados ao corrigir.

Referências no formato `arquivo:linha` apontam para o estado em `main` (commit `d610fce`).

---

## ✅ Status — CORRIGIDO (2026-07-07)

Todos os itens abaixo foram implementados e commitados em `main` (11 commits, `d610fce..HEAD`).
Backend: build 0 warnings/erros, testes de modelo 4/4. App: `flutter analyze` sem novos issues.
**Falta o teste end-to-end** (backend rodando + APK no celular USB).

| Item | Commit | Observação |
|------|--------|-----------|
| S1 CORS + local-key | `fce4172` | |
| S2 Path traversal | `f98ce35` | |
| D1 Opcode IPC/PONG/CLOSE | `26a7c51` | |
| R2 Cache GPU | `d7b612e` | + libera counters no shutdown |
| D2 Reconexão Discord | `f59ed2e` | watchdog 10s + `_connectLock` |
| R1 Poller de mídia | `24f4c67` | gate por clientes + 3s. **Deferido:** STA dedicado com manager reutilizado (arriscado — `RPC_E_WRONG_THREAD`) |
| R3 Dispose NAudio/GDI | `5512a2c` | também corrigiu a corrida do `MixerChannel` |
| S3 API key segredo | `f5ca4ee` | redação em log/URL. **Deferido:** migrar SharedPreferences→`flutter_secure_storage` (risco de perder o pareamento; fazer com migração cuidadosa) |
| D3 Refresh token | `73c26fa` | |
| Robustez (backend) | `38501ae` | discovery, mixer 500, OBS hello, remove `Startup.cs`/`SendMessage` |
| Robustez (app) | `62900ab` | reconexão SignalR, leak de assinatura, mounted, discovery |

> **Pré-existente (não corrigido aqui):** o projeto órfão `tests/DroidDeck.Tests` (fora da `.sln`) não compila — `MixerControllerTests` constrói `MixerController` sem o `hubContext`. Independente desta inspeção; vale consertar ou remover.

---

## 0. Resumo executivo — o que atacar primeiro

| # | Tema | Severidade | Esforço | Arquivo(s) principal(is) |
|---|------|-----------|---------|--------------------------|
| S1 | CORS reflete qualquer origem + `local-key` anônimo → **RCE a partir de qualquer site** | 🔴 Crítico (segurança) | Baixo | `Program.cs:154-163,216-224` |
| S2 | Path traversal via `profile.Id` → escrita/exclusão de arquivo arbitrária | 🔴 Crítico (segurança) | Baixo | `StreamDeckConfigService.cs:119,143` |
| S3 | API key em texto claro (rede, logs, SharedPreferences) | 🔴 Crítico (segurança) | Médio | `signalr_service.dart:109,113` |
| R1 | Poller de mídia WinRT recria `SessionManager` a cada 1s, 24/7 | 🔴 Alto (RAM) | Médio | `SystemMonitorService.cs:103` + `MediaControlService.cs:23` |
| R2 | `SampleGpu()` recria `PerformanceCounterCategory` + `GetInstanceNames()` a cada 1s | 🟠 Alto (RAM/GC) | Baixo | `SystemMonitorService.cs:152-153` |
| R3 | Objetos COM NAudio + ícones GDI + `Process` nunca liberados | 🟠 Alto (RAM/handles) | Médio | `MixerChannel.cs:31-36`, `MixerMaster.cs`, `AudioControlService.cs` |
| D1 | Opcode do frame IPC ignorado → sem PONG → Discord derruba conexão ociosa | 🔴 Alto (Discord) | Baixo | `DiscordRpcService.cs:406-418` |
| D2 | Zero reconexão automática após a conexão cair | 🔴 Alto (Discord) | Médio | `DiscordAutoConnect.cs:25-37` |
| D3 | Token OAuth2 nunca renovado (só reusado) → para de funcionar em ~7 dias | 🟠 Médio (Discord) | Médio | `DiscordRpcService.cs:141-192` |

**Se for corrigir em ordem de impacto/custo:** S1 → S2 → D1 → R2 → D2 → R1/R3 → S3 → D3 → resto.

---

## 1. Consumo de RAM (backend C#)

Contexto de DI (`Program.cs:166-185`): quase tudo é **singleton**, com três `IHostedService` sempre ativos — `SystemMonitorService` (loop 1s), `ObsAutoConnect` (loop 10s), `DiscordAutoConnect` (uma vez no boot). O `.csproj` não configura GC → **workstation GC**. O consumo alto e crescente mesmo **com o app ocioso** é explicado por R1 + R2; sob uso do mixer, soma-se R3.

### R1 — 🔴 Poller de mídia cria um `SessionManager` WinRT novo a cada 1s, para sempre ✅
- `SystemMonitorService.cs:103` chama `_media.IsAnythingPlayingAsync()` a cada tick do loop de 1s (`SystemMonitorService.cs:68-114`).
- `MediaControlService.GetSessionManagerAsync()` (`MediaControlService.cs:23-26`) executa `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()` — **um objeto COM/WinRT novo por chamada**, mais `GetSessions()` (um objeto WinRT por sessão) e um `GetPlaybackInfo()` por sessão.
- Roda **incondicionalmente, mesmo sem nenhum cliente conectado** (o loop não checa se há clientes). ~86.400 managers/dia, todos finalizáveis (RCW), sempre à frente do GC.
- O comentário em `MediaControlService.cs:20-22` explica por que **não** cachear o manager (afinidade STA → `RPC_E_WRONG_THREAD`) — mas recriar sempre é justamente o padrão que acumula RCWs.
- **Correção:** (a) reduzir a frequência do `ReceiveMediaStatus` para 3-5s e só publicar quando `houver clientes`; (b) rodar o acesso WinRT num **STA dedicado de vida longa**, reutilizar o manager e assinar `SessionsChanged`/`PlaybackInfoChanged` em vez de pollar; e/ou (c) `Marshal.FinalReleaseComObject` nos objetos por tick.

### R2 — 🟠 `SampleGpu()` recria a categoria de perf a cada 1s ✅
- `SystemMonitorService.cs:152-153`: a cada segundo faz `new PerformanceCounterCategory("GPU Engine")` + `cat.GetInstanceNames()` (varredura PDH que aloca **uma string por instância de engine** — centenas em máquinas com várias GPUs) + `HashSet` + `ToList()` por volta.
- Os `PerformanceCounter` em si já são cacheados corretamente (`_gpuCounters`), então o problema é **alocação/pressão de GC contínua**, não vazamento puro — mas é o que mantém o heap "inflado" e faz a RAM não descer.
- **Correção:** cachear os nomes de instância e recomputar só a cada N ticks (ex.: 30s); reaproveitar as coleções; publicar stats a cada 2-3s.

### R3 — 🟠 COM do NAudio + ícones GDI + `Process` nunca liberados ✅ (GDI confirmado)
Disparado por REST e ações do deck (sob demanda, não em loop — mas acumula por interação):
- `MMDeviceEnumerator`, `MMDevice`, `AudioEndpointVolume`, `AudioSessionManager`, `AudioSessionControl`, `SimpleAudioVolume` são `IDisposable` e **nunca sofrem `Dispose`** em: `MixerMaster.cs:47,91,117`, `NAudioAdapters.cs:68,76`, `AudioControlService.cs:23-79`.
- **Vazamento de GDI real e confirmado** — `MixerChannel.cs:31-36`: `Icon.ExtractAssociatedIcon(...)` e `icon.ToBitmap()` criados por sessão de áudio e **nunca liberados** (cada um segura um HICON/HBITMAP; cota de ~10k handles GDI por processo). Sob polling do mixer, esgota handles antes do finalizador rodar.
- **Handles de `Process`** sem `Dispose`: `MixerChannel.cs:22-23,90`, `AudioControlService.cs:32,68`.
- `MixerService.FindOne` (`MixerService.cs:40-41`) enumera **todos** os dispositivos render **e** capture (com extração de ícone completa) só para achar um por id — chamado inclusive no toggle-mute do deck.
- **Correção:** `using`/`Dispose` em todos os objetos NAudio, no `Icon`/`Bitmap` de `MixerChannel` e nos `Process`; idealmente um serviço de enumeração com cache invalidado por `IMMNotificationClient`, e resolver device por id direto em `FindOne`.

### RAM — menores
- `SystemMonitorService.cs:98,104` — broadcast SignalR **duplo por segundo independente de clientes**; adicionar throttle + `if (clients>0)`.
- `MediaControlService.cs:82-91` — thumbnails convertidos em Base64 (data URI) por sessão a cada `GetAllSessionsAsync`; `byte[]` + string grandes vão para LOH (fragmentação). Limitar/cachear.
- `IconExtractor.cs` / `Lib/IconExtractor.cs` — vazariam HICON (`Icon.FromHandle` sem `DestroyIcon`), mas são **código morto** (sem chamadores). Remover ou corrigir se reativados.
- `_cpuCounter`/`_gpuCounters` não liberados no shutdown do `SystemMonitorService` (leak só no encerramento).

---

## 2. Plugin Discord

O caminho "feliz" (Discord aberto, token válido, conexão recém-feita) funciona: o framing IPC lê `len` corretamente, a correlação por `nonce` (`ConcurrentDictionary<string,TaskCompletionSource>`) é sólida, e o contrato de operação app↔executor **bate 100%**. Os defeitos são de **durabilidade da conexão** — ela cai sozinha e não volta, e o token expira sem renovação. Casa com "funcionava e parou / às vezes funciona".

### D1 — 🔴 Opcode do frame IPC é ignorado: sem PONG, CLOSE não tratado
- `DiscordRpcService.cs:406-418` lê só o `length` (`BitConverter.ToInt32(header,4)`) e trata todo frame como FRAME; o **opcode em `header[0..4]` nunca é lido**.
- Protocolo IPC do Discord: `0=HANDSHAKE, 1=FRAME, 2=CLOSE, 3=PING, 4=PONG`. O Discord manda **PING** e espera **PONG** com o mesmo payload; como nunca respondemos PONG, conexões ociosas são fechadas. Frames **CLOSE** (razão do fechamento) são silenciosamente descartados em `HandleFrame` (`:439-471`).
- **Correção:** ler `int opcode = BitConverter.ToInt32(header,0)` e dar `switch` — opcode 3 → `WriteFrameAsync(4, payload)`; opcode 2 → logar mensagem e derrubar limpo; opcode 1 → `HandleFrame`.

### D2 — 🔴 Sem reconexão automática após a queda
- `DiscordAutoConnect.cs:25-37` tenta conectar **uma vez** no `StartAsync`. O `catch` do read loop (`DiscordRpcService.cs:420-425`) só seta `Connected=false` + broadcast e para.
- Se o pipe cai (D1, Discord reiniciado, usuário fechou/abriu o Discord), nada re-tenta; o deck fica morto até o usuário abrir a config e tocar "Conectar".
- **Correção:** loop de reconexão com backoff quando já houve conexão bem-sucedida e há token salvo (`interactive:false`), disparado a partir do `catch` do read loop — espelhar o watchdog do OBS (`ObsAutoConnect`).

### D3 — 🟠 Token OAuth2 nunca é renovado (só reusado)
- `DiscordRpcService.cs:169-192` guarda **só** o `access_token` (`:191`); o `refresh_token` é descartado e não há fluxo `grant_type=refresh_token`.
- Access tokens do Discord expiram (~7 dias). Ao expirar, `AUTHENTICATE` falha → em auto-conexão lança "Sem token válido" e desiste silenciosamente; o usuário precisa **reaprovar o popup toda semana**.
- **Correção:** salvar `refresh_token` + `expires_in`; antes de `AUTHENTICATE` (ou no `catch`) renovar via `grant_type=refresh_token` sem popup.

### Discord — menores
- `DiscordRpcService.cs:262-299` — `RefreshVoiceChannelAsync` engole **qualquer** exceção (inclusive `TimeoutException` transitório) e zera `VoiceChannelId/Name`/`_participants` + broadcast → "flicker" de sair do canal / lista vazia mesmo estando na call. Em erro, **preservar** o estado anterior.
- `DiscordRpcService.cs:158-159` — assina `VOICE_SETTINGS_UPDATE` e `VOICE_CHANNEL_SELECT`, mas **não** `VOICE_STATE_CREATE/UPDATE/DELETE`; quem entra/sai da call enquanto você já está nela não reflete na lista de participantes.
- `deck_grid_button.dart:108-117` — cada botão de Discord chama `GET /api/discord/state` no `initState`; rajada redundante (o estado já chega por SignalR). Fazer uma vez no nível da página.
- `droiddeck_client.dart:152-163` — `POST /api/discord/connect` sem timeout explícito, mas o backend pode bloquear até 60s esperando aprovação do popup; conferir o `receiveTimeout` do `Dio` para não dar erro antes.

---

## 3. Bugs críticos de segurança (varredura geral)

> Ponto positivo: a auth global está bem-feita — `FallbackPolicy` exige autenticação em todos os endpoints (controllers + hub) e a comparação de chave usa `CryptographicOperations.FixedTimeEquals` (timing-safe).

### S1 — 🔴 CORS reflete qualquer origem + `local-key` anônimo → RCE por qualquer site ✅
- `Program.cs:154-163`: `SetIsOriginAllowed(origin => true)` + `AllowCredentials()` faz o ASP.NET **refletir o `Origin` exato** — qualquer site pode ler respostas de `http://localhost:5000`.
- `Program.cs:216-224`: `/api/pairing/local-key` é `[AllowAnonymous]` e devolve a API key para **qualquer conexão de loopback**. Um `fetch('http://localhost:5000/api/pairing/local-key')` feito pelo navegador da vítima sai de `127.0.0.1` → o site lê a chave → chama `/api/StreamDeck/execute` com `launchApp`/`hotkey` → **execução de código arbitrária no PC**.
- **Correção:** restringir CORS a origens conhecidas (o próprio host servido); remover `AllowCredentials()` (auth é por header, não cookie); no `local-key`, exigir ausência de `Origin`/`Sec-Fetch-Site: cross-site` ou casar com nonce local.

### S2 — 🔴 Path traversal via `profile.Id` → escrita/exclusão de arquivo arbitrária ✅
- `StreamDeckConfigService.cs:119` (`SaveProfile`) e `:143` (`DeleteProfile`): `Path.Combine(_profilesDirectory, $"{profile.Id}.json")` usa o `Id` do corpo JSON **sem validação**. `{"id":"..\\..\\..\\Windows\\Temp\\evil"}` escreve/apaga `.json` fora do diretório de perfis.
- **Correção:** validar que `Id` é GUID (ou `Path.GetFileName` + whitelist); rejeitar `/`, `\`, `..`.

### S3 — 🔴 API key em texto claro (rede, logs, storage)
- `signalr_service.dart:109` — chave na query string do WebSocket sobre `ws://` sem TLS (sniffável na LAN).
- `signalr_service.dart:113` — `_logger.info('Connecting ... $hubUrl')` **loga a chave**; `currentUrl.value = hubUrl` é exibido no diálogo de debug (`stream_deck_page.dart:321`).
- Mobile persiste a chave em `SharedPreferences` (texto plano).
- **Correção:** `flutter_secure_storage`; remover/mascarar a chave em logs e no `currentUrl`; considerar TLS.

---

## 4. Robustez / correção (backend + app)

- **`DiscoveryServer.cs:76-85`** — discovery **morre para sempre** em qualquer exceção (`break`). No Windows, UDP recebe `WSAECONNRESET (10054)` quando um destino some (ICMP port-unreachable) e a próxima `ReceiveAsync` lança → discovery para. Em erro transitório, logar e `continue`; `break` só em cancelamento/`ObjectDisposedException`.
- **`MixerController.cs:48-49,84-85`** — `if (device == null)` após `new` é morto; `SetOptions` chama `GetDevice(id)` de novo (`MixerMaster.cs:118`) que lança `COMException` para id inexistente → **500 não tratado**. Validar id e retornar `BadRequest`/`NotFound`.
- **`MixerChannel.cs:22`** — `Process.GetProcessById(...)` está **fora** do try/catch (que só cobre o ícone). Processo que termina entre `ProcessExists` (`:20`) e essa linha → `ArgumentException` sobe na enumeração → 500. Mover para dentro do try.
- **`home_controller.dart:21`** — `volumeUpdates.subscribe(...)` no construtor, mas `dispose()` (`:147`) não cancela a assinatura → callback dispara `fetchOutputs()` num controller descartado (leak + erro). Guardar e chamar o dispose da assinatura.
- **`splash_page.dart:56`** — `Navigator.pushReplacementNamed` após `await signalR.init(...)` sem re-checar `mounted`. Adicionar `if (!mounted) return;` após o await.
- **`signalr_service.dart:119`** — `withAutomaticReconnect()` sem política usa defaults `[0,2,10,30]s` e **desiste após ~30s**; celular que perde a rede por mais que isso nunca reconecta. Passar `IReconnectPolicy` com retry indefinido + backoff limitado.
- **`ObsService.cs:279`** — `_ = HandleHelloAsync(...)` fire-and-forget; se o auth vier sem `challenge`/`salt`, `GetProperty(...).GetString()!` (`:136-137`) lança em Task não observada e o `_identifiedTcs` só cai no timeout de 6s. Completar o TCS com falha. Senha do OBS em texto plano em `obs.json` (`:80`).
- **`server_discovery_service.dart:52-69`** — o `listen` chama `socket.close()` (`:67`) no **primeiro** datagrama mesmo se o parse falhar/sem `ip`; um pacote malformado inicial impede ler os seguintes. Só fechar após completar com sucesso (ou ouvir até timeout).
- **`Startup.cs`** — **código morto** (o `Program.cs` configura tudo inline) e conflitante (CORS `WithOrigins("http://localhost:5000")`, pipeline sem auth). Remover para não confundir auditoria.

---

## 5. Menores / cosméticos

- `ActionExecutorService.cs:49` — `action.Type.ToLower()` cultura-sensível (i turco) + NRE se `Type` null. Usar `ToLowerInvariant()` + null-check.
- `droiddeck_client.dart` — `process`/`id`/`sessionId` interpolados na URL sem `Uri.encodeComponent` (quebra com espaço/caractere especial).
- `Program.cs:151,192` — versão do Swagger via `.Replace(".","").Replace("0","")` embaralha dígitos (`1.0.5`→`15`).
- `DiscoveryServer.cs:64-66` — resposta JSON montada por `string.Replace`; quebra se `MachineName` tiver `"`/`\`. Serializar de verdade.
- `DiscoveryServer.cs:50` — `Task.Delay(Timeout.Infinite, token)` recriado a cada iteração (tasks órfãs até o cancelamento).
- `Program.cs:75` — `webTask.Wait()` no shutdown pode lançar `AggregateException`. Envolver em try/catch.
- `Log.cs:126-151` — `UpdateLog` substitui a config global do NLog inteira (derruba console/regras).
- `stream_deck_controller` `setGridSize` clampa `1..10` vs backend `SaveLayout` `1..20` (inconsistente); `SaveProfile` não valida `Rows/Columns`.
- `DeckHub.cs:16-19` — `SendMessage` faz broadcast para todos sem uso aparente nem validação (qualquer cliente autenticado pode floodar). Remover se não usado.
- `main.dart:20` — `OneSignal.Debug.setLogLevel(verbose)` fixo em produção.

---

## 6. Próximos passos sugeridos

1. **Bloco de segurança (rápido e crítico):** S1 (CORS + local-key), S2 (validar `profile.Id`). São de baixo esforço e fecham o vetor de RCE.
2. **Discord (o que o usuário sente):** D1 (PONG + tratar CLOSE) e D2 (reconexão automática) resolvem "parou de funcionar"; D3 (refresh token) elimina a reautorização semanal.
3. **RAM:** R2 (cache dos nomes de GPU — 1 correção pequena, grande alívio de GC), depois R1 (frequência do poller + STA dedicado) e R3 (`using`/`Dispose` no caminho de áudio + ícones).
4. **Robustez:** discovery que não morre (#4), reconexão SignalR no app, leaks de assinatura no app.
5. **Higiene:** remover `Startup.cs` morto, `IconExtractor` morto, `DeckHub.SendMessage` se não usado.

> Nenhum arquivo de código foi alterado nesta inspeção — este documento é o ponto de partida para as correções.
