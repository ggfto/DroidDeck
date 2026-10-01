# Files & settings

## Data on the PC

**`%AppData%\DroidDeck\`** (Roaming) — your decks:

| File | Contents |
|---|---|
| `Profiles\<id>.json` | One file per profile: name and buttons. Custom icons are embedded as base64. |
| `layout.json` | Grid size reported by the phone (`rows`, `columns`). |

**`%LocalAppData%\DroidDeck\`** — keys and integrations:

| File | Contents |
|---|---|
| `apikey` | The pairing key. Delete it to revoke every paired phone. |
| `discord.json` | Discord Client ID, Client Secret and tokens. |
| `obs.json` | OBS host, port and password. Its presence turns on OBS auto-connect. |
| `soundboard.json` | Soundboard output devices and volume. |
| `SoundCache\*.mp3` | Downloaded MyInstants sounds. Safe to delete. |
| `tuya.json` | Tuya user code, session token and app registration (`ClientId`, `Schema`). |

**`%LocalAppData%\DroidDeck.log`** — the log file.

!!! info "Secrets are encrypted"
    `apikey`, `discord.json`, `obs.json` and `tuya.json` are encrypted with Windows DPAPI and can only
    be read by your Windows account on this PC. Files from older versions, saved in plain text, are
    encrypted automatically the first time they are read. To edit one by hand (e.g. a Tuya `ClientId`),
    save it as plain JSON; DroidDeck encrypts it again on the next start.

## Backup and moving to another PC

Copy `%AppData%\DroidDeck\Profiles\` to keep your decks. Secrets are tied to this PC and Windows
account, so on a new PC you set up the integrations again and pair the phones again.

## Command-line options

| Option | Effect |
|---|---|
| *(none)* | Tray app + server. |
| `--headless` / `--no-tray` | Server only, no tray icon. Stop with Ctrl+C. |
| `--print-pairing` | Prints the pairing URI, saves the QR to `%TEMP%\droiddeck-pair.png` and exits. |

| Environment variable | Effect |
|---|---|
| `ASPNETCORE_URLS` | Overrides the address the server listens on (default `http://0.0.0.0:4787`). The QR code and the Discord redirect still use port 4787. |
| `ASPNETCORE_ENVIRONMENT=Development` | Enables Swagger at `/swagger` and detailed error pages. |

## `appsettings.json`

Next to `DroidDeck.exe`:

| Key | Default | Meaning |
|---|---|---|
| `EnableDiscovery` | `true` | Answers UDP discovery requests on port 7573. |
| `EnableSoftwareActivation` | `false` | Enables the legacy `/api/Software/activate` endpoint. |
| `AllowedTargets` | *(empty)* | Comma-separated process names allowed by the legacy `/api/Software` mute endpoints. |
