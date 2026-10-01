# Installation

DroidDeck has two parts: the **Windows app** (server) and the **Android app** (your deck). Both are
attached to every [GitHub release](https://github.com/ggfto/DroidDeck/releases/latest).

## Windows

1. Download **`DroidDeck-win-x64.zip`** from the latest release.
2. Extract it to a permanent folder, for example `C:\Program Files\DroidDeck` or `%LocalAppData%\DroidDeck-app`.
3. Run **`DroidDeck.exe`**.

The app has no window. It lives in the **system tray**; a notification confirms it is running at
`http://localhost:4787/`. The build is self-contained, so you don't need to install .NET.

!!! warning "Windows SmartScreen"
    The executable is not code-signed yet, so Windows may show *"Windows protected your PC"*.
    Click **More info → Run anyway**.

### Tray menu

Right-click the tray icon:

| Item | What it does |
|---|---|
| **Abrir no navegador** | Opens the configurator at `http://localhost:4787/` (double-clicking the icon does the same). |
| **Parear dispositivo (QR)…** | Shows the pairing QR code for the phone. |
| **Iniciar com o Windows** | Starts DroidDeck when you sign in to Windows. |
| **Sair** | Quits DroidDeck. |

### Firewall

The phone connects to the PC on **TCP 4787**, and automatic server discovery uses **UDP 7573**. If
Windows asks whether to allow DroidDeck on the network, allow it on **private networks**. If you
dismissed that prompt, add the rules manually in an elevated PowerShell:

```powershell
New-NetFirewallRule -DisplayName "DroidDeck (TCP 4787)" -Direction Inbound -Protocol TCP -LocalPort 4787 -Profile Private -Action Allow
New-NetFirewallRule -DisplayName "DroidDeck discovery (UDP 7573)" -Direction Inbound -Protocol UDP -LocalPort 7573 -Profile Private -Action Allow
```

Make sure your Wi-Fi is set as a **Private** network in Windows settings.

## Android

1. On the phone, download **`DroidDeck.apk`** from the latest release.
2. Open it and allow installing from unknown sources when Android asks.
3. Open **DroidDeck** and continue with [Pairing](pairing.md).

Android 7.0 or later is required. The app needs the camera only to scan pairing QR codes.
