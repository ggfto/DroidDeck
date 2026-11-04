# Tally and ON AIR

A phone in full screen becomes an **on-air** light for a radio studio, or a tally light for a camera: red on
air, green in preview, dark when off air. The screen stays on while this is open.

## How to use it

1. Open **Settings → Integrations → Tally and ON AIR**, or create a **Tally and ON AIR** button with **Open
   the tally screen**.
2. Choose **what to follow**. Each phone keeps its own choice: one can be camera 1, another camera 2, and
   another the light in the hallway.
3. Tap the screen to show the back and change buttons.

## What you can follow

The list only shows what is connected now:

| Source | What shows up | When it turns red |
|---|---|---|
| [OBS](obs.md) | Each scene, streaming and recording | Scene in program (green in Studio Mode preview), or while streaming/recording |
| [vMix](vmix.md) | Each input, streaming and recording | Input in program (green in preview), or while streaming/recording |
| [mAirList](radio.md) | On air | When mAirList is on air |
| [RadioDJ](radio.md) | Live input | When the live input is on |
| [Voicemeeter](voicemeeter.md) | Open microphone, per input | When the input isn't muted |
| Manual signal | ON AIR | When a deck button or the tally screen turns the signal on |

For a radio station with no automation, the **manual signal** is enough: a **Manual ON AIR signal (toggle)**
button on the presenter's deck, and a phone in the hallway following the manual signal. You can also turn it
on and off from the tally screen itself: with the manual signal selected, tap the screen and use the button
that shows up.

## NO SIGNAL

If the phone loses its connection to the PC, or the program it follows closes, the screen turns gray and shows
**NO SIGNAL**. It never shows "off air" without knowing: whoever is in front of the camera has to be able to
trust it.

!!! warning "This isn't a professional broadcast tally"
    The state travels over Wi-Fi and arrives with a small delay. It works as a cue for presenters and crew, but
    it doesn't have the frame accuracy of a wired tally system.

## On a button

With **Only show the state on the button**, the button turns red or green without opening the full screen.
