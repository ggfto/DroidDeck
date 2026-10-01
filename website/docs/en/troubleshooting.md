# Troubleshooting

## The phone can't find or connect to the PC

1. **Same network**: phone and PC must be on the same local network. Guest Wi-Fi networks and
   "AP isolation" block devices from seeing each other.
2. **Firewall**: allow TCP 4787 and UDP 7573 on private networks (see [Installation](getting-started/installation.md#firewall)),
   and make sure Windows marks your network as **Private**.
3. **DroidDeck is running**: look for the tray icon. On the PC, `http://localhost:4787/api/ping`
   should return `{"ok":true,...}`.
4. **Wrong IP**: on PCs with several network adapters, VPNs or virtual adapters (Hyper-V, WSL,
   VirtualBox), the QR may contain the wrong address. Find the right one with `ipconfig` and type it
   manually in **IP do Servidor**.
5. **"Servidor não encontrado"** during search: discovery uses broadcast, which some routers block.
   Type the IP manually.

## "Falha de autenticação… Refaça o pareamento"

The key on the phone doesn't match the PC's (for example, after the `apikey` file was deleted). Scan
the QR code again from the tray menu.

## The configurator shows 401 errors

The configurator only signs in automatically on the PC itself. Open it at `http://localhost:4787/`
on the machine where DroidDeck runs.

## A hotkey does nothing

- The keys go to the **focused window** — make sure the right app is in front.
- Apps running **as administrator** ignore input from a non-administrator DroidDeck.
- Games with anti-cheat may ignore simulated keys.
- Check the syntax in [Hotkey](guide/actions.md#hotkey).

## Media buttons do nothing

Check that the app shows up in Windows' media overlay (press a media key on the keyboard). If the
log mentions the media broker hanging, wait a minute or restart the app that is playing.

## Integrations

- **Discord**: see [Discord → Troubleshooting](integrations/discord.md#troubleshooting).
- **OBS**: see [OBS → Troubleshooting](integrations/obs.md#troubleshooting).
- **Tuya**: *"QR expirado; gere outro."* — the QR is valid for 2 minutes. Brand apps refusing the
  QR: see [Tuya](integrations/tuya.md#linking-your-account).

## Logs

DroidDeck writes a log to **`%LocalAppData%\DroidDeck.log`** (next to the `DroidDeck` folder, not
inside it). Attach it when you [open an issue](https://github.com/ggfto/DroidDeck/issues) — it never
contains your API key.
