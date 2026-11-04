# Companion

With [Bitfocus Companion](https://bitfocus.io/companion), your phone becomes a **surface**, like a Stream Deck:
the keys and pages you set up there show up on the phone, and a tap triggers whatever they're set to do. This
gives DroidDeck access to the hundreds of devices Companion controls (video switchers, audio consoles, PTZ
cameras...).

## Setup

1. Open Companion. The Satellite API port (`16622`) is on by default.
2. Open **Settings → Integrations → Companion** in DroidDeck, choose the grid's **columns** and **rows** (the
   default is 8 × 4), and tap **Connect**.
3. DroidDeck shows up under **Surfaces** in Companion.

Companion on another PC: enter its IP (`192.168.0.20`, or `192.168.0.20:16622`).

## How to use it

Tap **Open the surface**, or create a **Companion** button on the deck. The screen stays on while this is open.
Holding a key counts as holding: Companion gets the press and the release separately.

- All phones show the same surface. To Companion, it's a single device.
- If a phone drops with a finger on a key, the PC releases the key by itself.
