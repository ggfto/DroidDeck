# Pairing

Pairing gives the phone the PC's address and a secret **API key**. You only do it once.

## Pair with a QR code (recommended)

1. On the PC, right-click the tray icon → **Parear dispositivo (QR)…**. A window shows a QR code
   and the text `IP: <address>:4787  Chave: <key>`.
2. On the phone, open DroidDeck. On first launch the **settings** screen opens; otherwise tap the
   gear on the **Áudio** tab.
3. Tap **Parear (QR)** and scan the code.

The phone saves the address, port and key, and connects. The status dot turns green (*Connected*).

??? info "What is inside the QR code"
    `droiddeck://pair?ip=<LAN IP>&port=4787&key=<api key>` — the PC's LAN address, the port and the
    API key. Treat it like a password: anyone who scans it can control your PC through DroidDeck.

## Find the server automatically

**Pesquisar Servidor** (*Search server*) broadcasts on the local network (UDP 7573) and fills in
the PC's IP address. Discovery does **not** send the API key, so you still need to pair with the QR
code before the phone can connect.

## Enter the address manually

Type the PC's IP in **IP do Servidor** (`192.168.0.10` or `192.168.0.10:4787`) and tap **Salvar**.
An API key from an earlier pairing is kept.

## Re-pairing and resetting the key

- If the phone shows *"Falha de autenticação (chave inválida ou expirada). Refaça o pareamento."*,
  scan the QR code again.
- The key is stored on the PC in `%LocalAppData%\DroidDeck\apikey`. To revoke access for every
  paired phone, quit DroidDeck, delete that file and start it again. A new key is generated, and
  every phone must pair again.

## Connection behaviour

The phone reconnects by itself if the PC restarts or the Wi-Fi drops. It retries quickly at first
(0, 2, 5, 10, 20 s) and then every 30 s, indefinitely. Tap the status dot to see the connection URL
(with the key masked) and the last data received.
