# Smart home (Tuya / Smart Life)

Control lights, plugs and switches from the deck. It works with **any brand built on Tuya** — Nova
Digital, Positivo Casa Inteligente, RSmart, Elgin, Geonav, Aubess and many others are rebrands of
the same platform.

**No developer account is needed.** You link your account by scanning a QR code.

## Linking your account

Linking is done in the **phone app**:

1. In the **Smart Life** (or **Tuya Smart**) app: **Me → ⚙️ → Account and Security → User Code**. Copy the code.
2. In DroidDeck on the phone: **settings → Casa inteligente (Tuya)**. Paste the code and tap **Gerar QR**.
3. Scan the QR with the Smart Life app (**Home** tab → scan icon) within 2 minutes. The app asks
   you to confirm a login for **"Home Assistant"** — see why below.

The session is saved on the PC and reconnects automatically on startup.

!!! warning "Brand apps refuse the QR"
    If your devices are in a **brand app** (Nova Digital, Positivo, RSmart…), the scan fails with
    *"please use the designated app to scan the code to login"*. Only **Smart Life** and **Tuya
    Smart** accept it, and sharing devices doesn't help.

    **Fix:** remove the device from the brand app and pair it again in **Smart Life**. It is the same
    hardware and works the same way; you only lose automations created in the brand app.

??? info "Why does it say Home Assistant?"
    DroidDeck uses the public Home Assistant app registration for the QR login, because Tuya does not
    offer that registration through self-service. The `clientId` and `schema` live in `tuya.json`, so
    switching to a dedicated registration later is a configuration change.

## Buttons

Choose the **Tuya** action type, then the **device** (offline devices are marked) and the
**function**. The fields adapt to the device:

| Function type | Editor control |
|---|---|
| On/off | Toggle, or a fixed on/off value |
| Number (brightness, temperature…) | Slider using the device's own range and unit |
| Mode | List of the allowed options |
| Other | Raw text value |

- **Alternar (liga/desliga)** (*toggle*) turns it on if it's off and vice versa — the usual deck behaviour.
- **Sempre ligar / sempre desligar** (*fixed value*) always sends the same value.
- **Testar agora** (*test now*) sends the command immediately.

Set an **active colour** to light the button while the device is on. Dimmers show their level, and
colour lamps show their real colour. State arrives by **push**, so changes made with a wall switch
or the Smart Life app also show up on the deck.

## API quota

Tuya's free tier allows about 26,000 API calls per month. That is why DroidDeck **never polls**: state
comes by push, and the device list is only reloaded when you press refresh (about 4 calls per
device). Pressing buttons is fine — each press is one call.

## Limitations

- **Cloud only.** Commands go through Tuya's cloud, so buttons don't work without internet.
- An **offline device** fails with Tuya error 2001.
- If push is disconnected, the first **toggle** may go the wrong way, because it uses the last known state.
