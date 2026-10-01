# Arquitetura

O DroidDeck é um monorepo com duas metades que evoluem juntas:

```
DroidDeck/
  RaspDeck/      C# backend (.NET 8): WinForms tray + ASP.NET Core + SignalR. Serves the API and the web app.
  app/           Flutter app: the phone runtime and the web configurator (same code).
  tests/         Backend tests.
  scripts/       Utilities (deploying the web build to wwwroot).
  website/       This documentation and the project's landing page.
```

## Backend (`RaspDeck/`)

- **Host**: `Program.cs` sobe o ASP.NET Core em `0.0.0.0:4787` e o ícone da bandeja em WinForms
  (`frmPrincipal.cs`), a não ser que `--headless` seja passado.
- **Auth**: `Auth/` — handler de autenticação por chave de API (header, bearer ou query `access_token`),
  com comparação em tempo constante. A chave é gerada em `%LocalAppData%\DroidDeck\apikey`.
- **Controllers**: `Controllers/` — endpoints REST por área (StreamDeck, Mixer, Media, Discord, OBS,
  Soundboard, Tuya).
- **Hub**: `Hubs/DeckHub.cs` — hub SignalR em `/deckHub`; envia o estado para os clientes.
- **Services**: `Services/`
    - `ActionExecutorService` — despacha a ação de um botão, pelo `type`, para o serviço certo.
    - `StreamDeckConfigService` — persistência de perfis e layout.
    - `SystemMonitorService` — amostragem de CPU/GPU/RAM/rede, transmitida a cada segundo.
    - `MixerService`, `AudioControlService`, `MediaControlService` — Core Audio via NAudio e sessões de mídia do WinRT.
    - `DiscordRpcService`, `ObsService`, `SoundboardService`, `TuyaService` — integrações, cada uma
      com um hosted service `*AutoConnect` que reconecta em segundo plano.
- **Descoberta**: `Lib/DiscoveryServer.cs` — responde em UDP 7573.
- **Web estática**: o build web do Flutter é servido a partir de `wwwroot/`.

## App (`app/`)

Uma única base de código Flutter, com dois papéis:

- **No navegador** (servido pelo PC), abre o **configurador**: perfis, grade com arrastar e soltar,
  painel de propriedades.
- **No celular**, é o **runtime**: a grade de botões (dimensionada para a tela), a aba Áudio e as
  configurações de pareamento e integrações.

Pontos principais:

- `lib/core/droiddeck_client/` — modelos da API (`DeckButton`, perfis…).
- `lib/src/stream_deck/` — grade, botões e o editor do painel de propriedades.
- `lib/src/services/signalr_service.dart` — conexão com o hub, com reconexão sem fim.
- `lib/src/config/` — páginas de configuração (pareamento, Discord, OBS, Tuya, soundboard).

## Contrato

O app e o backend compartilham:

- **REST** em `/api/...` — veja [API](../reference/api.md);
- **SignalR** em `/deckHub` — eventos do servidor para o cliente;
- **Descoberta UDP** na porta 7573;
- **URI de pareamento** `droiddeck://pair?ip=&port=&key=`.

## Adicionando um novo tipo de ação

Hoje, um novo tipo de ação mexe nos dois lados:

1. Um serviço em `RaspDeck/Services/` (mais um hosted service `*AutoConnect` se ele mantiver uma conexão).
2. O registro em `Program.cs`.
3. Um `case` no `ActionExecutorService` que lê os `parameters` da ação.
4. Um controller, se o editor precisar de listas ou configurações, e o estado enviado pelo `DeckHub`.
5. O tipo, seus campos e a renderização do estado no editor e nos botões do Flutter.
6. Uma página de configuração no app, se a integração precisar ser configurada.
