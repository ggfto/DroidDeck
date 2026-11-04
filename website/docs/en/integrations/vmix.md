# vMix

DroidDeck controls [vMix](https://www.vmix.com) through its **TCP API** and shows the state on the button: the
input in program turns red, the one in preview turns green, and recording and streaming light up.

## Setup

1. Open vMix. The TCP API (port `8099`) is already on, with no setup or password.
2. Open **Settings → Integrations → vMix** in DroidDeck and tap **Connect**.

vMix on another PC: enter its IP and allow port `8099` in its firewall.

## What you can do

Create a **vMix** button and choose:

- **Cut and transition:** cut (preview to program) and Fade, with its duration.
- **Inputs:** send to preview, send to program, Quick Play. The list shows your vMix inputs.
- **Overlays:** toggle an input on an overlay (1 to 8) and remove all of them.
- **Audio:** mute and volume for an input, mute and volume for the master.
- **Outputs:** record, stream, External, MultiCorder, Fade to Black and screenshot.
- **Other function:** any function from the
  [vMix shortcut reference](https://www.vmix.com/help29/ShortcutFunctionReference.html), with parameters as in
  the Web API (`Input=1&SelectedName=Title&Value=Hi`).

To use a phone as a camera tally, see [Tally and ON AIR](tally.md).
