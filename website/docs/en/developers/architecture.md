# Architecture

DroidDeck is a monorepo with two halves that evolve together:

```
DroidDeck/
  RaspDeck/      C# backend (.NET 8): WinForms tray + ASP.NET Core + SignalR. Serves the API and the web app.
  app/           Flutter app: the phone runtime and the web configurator (same code).
  tests/         Backend tests.
  scripts/       Utilities (deploying the web build to wwwroot).
  website/       This documentation and the project's landing page.
```

## Backend (`RaspDeck/`)

- **Host**: `Program.cs` starts ASP.NET Core on `0.0.0.0:4787` and the WinForms tray
  (`frmPrincipal.cs`), unless `--headless` is passed.
- **Auth**: `Auth/` — API-key authentication handler (header, bearer or `access_token` query),
  constant-time comparison. The key is generated in `%LocalAppData%\DroidDeck\apikey`.
- **Controllers**: `Controllers/` — REST endpoints per area (StreamDeck, Mixer, Media, Discord, OBS,
  Soundboard, Tuya).
- **Hub**: `Hubs/DeckHub.cs` — SignalR hub at `/deckHub`; pushes state to clients.
- **Services**: `Services/`
    - `ActionExecutorService` — dispatches a button action by `type` to the right service.
    - `StreamDeckConfigService` — profile and layout persistence.
    - `SystemMonitorService` — CPU/GPU/RAM/network sampling, broadcast every second.
    - `MixerService`, `AudioControlService`, `MediaControlService` — NAudio Core Audio and WinRT media sessions.
    - `DiscordRpcService`, `ObsService`, `SoundboardService`, `TuyaService` — integrations, each with
      an `*AutoConnect` hosted service that reconnects in the background.
- **Discovery**: `Lib/DiscoveryServer.cs` — UDP 7573 responder.
- **Static web**: the Flutter web build is served from `wwwroot/`.

## App (`app/`)

One Flutter codebase, two roles:

- **In the browser** (served by the PC) it opens the **configurator**: profiles, drag-and-drop grid,
  property panel.
- **On the phone** it is the **runtime**: the button grid (sized to the screen), the Áudio tab, and
  settings for pairing and integrations.

Key places:

- `lib/core/droiddeck_client/` — API models (`DeckButton`, profiles…).
- `lib/src/stream_deck/` — grid, buttons and the property panel editor.
- `lib/src/services/signalr_service.dart` — hub connection with endless reconnection.
- `lib/src/config/` — settings pages (pairing, Discord, OBS, Tuya, soundboard).

## Contract

The app and the backend share:

- **REST** under `/api/...` — see [API](../reference/api.md);
- **SignalR** at `/deckHub` — server-to-client events;
- **UDP discovery** on port 7573;
- **Pairing URI** `droiddeck://pair?ip=&port=&key=`.

## Adding a new action type

Today a new action type touches both sides:

1. A service in `RaspDeck/Services/` (plus an `*AutoConnect` hosted service if it keeps a connection).
2. Registration in `Program.cs`.
3. A `case` in `ActionExecutorService` that reads the action's `parameters`.
4. A controller if the editor needs lists or settings, and state pushed through `DeckHub`.
5. The type, its fields and its state rendering in the Flutter editor and buttons.
6. A settings page in the app if the integration needs configuration.
