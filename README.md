# AnyDeck

Small notes to build and run the project locally (Windows).

## Build

From a PowerShell prompt in repository root:

```powershell
Set-Location -Path 'G:\ggfto\AnyDeck\RaspDeck'
dotnet build -c Debug
```

## Run

```powershell
dotnet run -c Debug
```

- The application is a WinForms desktop app that also starts an embedded ASP.NET Core web server.
- The web API runs in the same process and, in development, is reachable at `http://localhost:5000` (default Kestrel endpoints).

## Shutdown

- The web host observes a `CancellationToken` which is triggered when the WinForms `Application.Run` ends. The `DiscoveryServer` is registered as a hosted background service and will stop gracefully on cancellation.

## Logging

- NLog configuration lives in `RaspDeck/NLog.config` and controls file logging (default path: LocalApplicationData/AnyDeck.log).
- The application also uses `Microsoft.Extensions.Logging` (ILogger<T>) across hosted services and controllers; the host is configured to use console logging and will attempt to wire NLog as a provider when `NLog.Extensions.Logging` is available.

## Tests

- A test project skeleton `AnyDeck.Tests` exists. Expand tests to cover services and controllers. The current unit test is a simple placeholder.

## Next steps

- Add unit tests for `MixerService` using abstractions for NAudio to allow mocking.
- Protect sensitive endpoints (e.g., `SoftwareController`) with authentication/authorization.
- Consider serving static files via ASP.NET Core `UseStaticFiles()` instead of `PostBuild` copy.

---
Created/updated by automation.
