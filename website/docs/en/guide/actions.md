# Buttons & actions

Each button runs one **action**. Pick it under **Action** in the button editor; the fields below
change with the type. Every action runs on the PC, except **Open profile** and **Back**, which
run on the phone.

Besides the normal tap, a button can have a [long-press and a double-tap](#gestures) action, be a
[two-state button](#two-states), [show information](monitors.md) (clock, weather, what's playing, a
slider, AI usage...), take [more than one cell](#size) and be pressed by an [automation](automations.md).

!!! info "Keyboard shortcut, Open program, Script and HTTP request are created on the PC"
    For security, buttons that send keys, open programs, run scripts or make HTTP requests (including
    multi-actions with those steps) can only be created or changed in the
    [configurator](../getting-started/configurator.md) on the PC. The phone runs them normally.

## Action types

### On the PC

Send keys, open things and script Windows itself.

| Action | What it does |
|---|---|
| [Keyboard shortcut](#hotkey) | Sends a key combination to the focused window. |
| [Open program](#launch-app) | Opens a program, file, folder or URL. |
| [Multi-action](#multi-action) | Runs several steps in sequence, with delays and conditions. |
| [Script](#script) | Runs a PowerShell or Python script or a `.bat` file, with arguments. |
| [HTTP request](#http) | Calls a webhook or any API. |
| [Windows](#windows) | Switches the default audio device, locks, sleeps or turns the screen off. |
| [Steam game](#steam) | Opens an installed game, with its cover on the button. |

### Media and audio

Control what's playing and how loud it is.

| Action | What it does |
|---|---|
| [Media](#media) | Play/pause, next, previous, stop. |
| [Mixer](#mixer) | Mutes apps; mutes or changes the volume of audio devices. |
| [Spotify](../integrations/spotify.md) | Player, like, shuffle, playlists and the cover on the button. |
| [Voicemeeter](../integrations/voicemeeter.md) | Mute and gain of strips and buses, Macro Buttons, parameters. |
| [Soundboard](../integrations/soundboard.md) | Plays a sound into your mic. |

### Streaming

Voice chat, your stream and the tools around it. Each one needs its integration set up once.

| Action | What it does |
|---|---|
| [Discord](../integrations/discord.md) | Voice controls and soundboard. |
| [OBS](../integrations/obs.md) | Scenes, recording, streaming and more. |
| [Twitch](../integrations/twitch.md) | Marker, clip, ad, chat, poll and title. |
| [YouTube](../integrations/youtube.md) | Live chat and viewers. |
| [Streamer.bot](../integrations/streamerbot.md) | Runs a Streamer.bot action, with arguments. |
| [VTube Studio](../integrations/vtubestudio.md) | Triggers model hotkeys or loads a model. |

### Studio: radio and TV { #studio }

| Type | What it does |
|---|---|
| [Tally and ON AIR](../integrations/tally.md) | Full-screen tally or ON AIR, or the state on the button. |
| [vMix](../integrations/vmix.md) | Cut, inputs, overlays, audio, recording and streaming. |
| [Companion](../integrations/companion.md) | Opens the Bitfocus Companion surface. |
| [mAirList and RadioDJ](../integrations/radio.md) | On air, automation, players and carts. |
| [Network (OSC, TCP, UDP)](../integrations/network.md) | Sends a message to mixing consoles, QLab and other devices on the network. Can only be created on the PC. |

### Lights and smart home

Devices on your network or in your smart-home hub.

| Action | What it does |
|---|---|
| [Tuya](../integrations/tuya.md) | Smart Life lights, plugs and switches. |
| [Home Assistant](../integrations/homeassistant.md) | Lights, plugs, scenes, scripts and sensors. |
| [Philips Hue](../integrations/hue.md) | Lights, rooms and scenes through the Hue Bridge. |
| [Elgato Key Light](../integrations/elgato.md) | On/off, brightness and temperature of Elgato lights. |
| [Govee](../integrations/govee.md) | Govee lights over the LAN API. |
| [MQTT](../integrations/mqtt.md) | Publishes a message to your broker. |

### Timers and navigation

| Action | What it does |
|---|---|
| [Timer, pomodoro & counter](#timer) | A live clock or count on the button. |
| [Open profile](#open-profile-and-back) | Opens another profile as a folder (on the phone). |
| [Back](#open-profile-and-back) | Returns from a folder (on the phone). |

## Keyboard shortcut { #hotkey }

Sends keys to **the window that has focus** on the PC, using the Windows *SendKeys* syntax plus `#`
for the Windows key: `^c` is Ctrl+C, `^+s` is Ctrl+Shift+S, `%{F4}` is Alt+F4, `#d` shows the
desktop. The full syntax, special keys and limitations are on [Keyboard shortcuts](hotkeys.md).

## Open program { #launch-app }

**Path** accepts anything you could open from *Run* (++win+r++): an `.exe`
(`C:\Windows\System32\notepad.exe`), a document, a folder or a URL (`https://…`, `steam://…`).

## Multi-action { #multi-action }

Runs several steps one after the other, each with a delay, and each optionally only when a
[condition](multi-action.md#conditions) holds (the stream is live, an app is open, it's a weekday...).
You can also [record the steps from the keyboard](multi-action.md#record). See
[Multi-action](multi-action.md).

## Media { #media }

Controls the media session Windows is showing in its media overlay: Spotify, browsers, players.

**Command:** **Play / Pause** (default), **Next track**, **Previous track**, **Stop**. A play/pause button switches its
icon live between ▶ and ⏸.

## Mixer { #mixer }

| Operation | App target | Device target |
|---|---|---|
| Toggle mute / Mute / Unmute | ✓ | ✓ |
| Set volume (0–100) | — | ✓ |
| Volume up / down (step) | — | ✓ |

- **App target**: the process name **without** `.exe`, e.g. `Spotify` or `chrome`. It affects every
  audio stream of that app. Per-app volume is available on the phone's **Audio** tab.
- **Device target**: **Default** follows Windows' current default device (choose **output** or
  **input**, i.e. speakers or microphone), or pick a specific device. Turning the volume up also
  unmutes the device.

Set an **active colour** to light the button up while the target is muted.

## Open profile and back { #open-profile-and-back }

**Open profile** shows another profile on top of the current one, like a folder. Folders can be
nested. **Back** closes the current folder; the title-bar arrow does the same. Both actions run on
the phone only.

## Timer, pomodoro & counter { #timer }

The button shows the time live. The clock runs on the PC, so every phone sees the same time and the
end of a countdown is caught even with the phone's screen off.

| Type | Tap | At the end |
|---|---|---|
| **Timer (countdown)** | Start / pause. After it ends, a tap resets it. | Sound on the PC, vibration on the phone, and the button lights up in its active colour. |
| **Stopwatch** | Start / pause. | — |
| **Pomodoro (focus and break)** | Start / pause. Focus and break alternate on their own and the button counts cycles. | Alert at every phase change. |
| **Counter** | Adds the **step**. | — |

**On tap** changes what a tap does: **Reset**, **Skip to the next phase (pomodoro)**,
**Add (counter)** / **Subtract (counter)**. With **Controlled timer**
a button acts on another button's timer, for example a "reset" button next to the clock. Counters
survive a PC restart.

## Windows { #windows }

| Action | What it does |
|---|---|
| **Change the default audio device** | Sets Windows' default output (or input). With **Toggle with (optional)** set, each tap switches between the two, e.g. headphones ↔ speakers. The button lights up while the chosen device is the default. |
| **Lock the computer** | Same as ++win+l++. |
| **Sleep** | Puts the PC to sleep. |
| **Turn off the display** | Turns the monitors off; moving the mouse turns them back on. |

## HTTP request { #http }

Calls a URL from the PC: n8n, Home Assistant or IFTTT webhooks, or any API. Pick the **method**, the
**URL**, the **headers** (one per line, `Name: value`) and, for POST/PUT/PATCH, the **body** and its
format (JSON, form or text). The request has 10 seconds to answer; the result goes to DroidDeck's log.

## Script { #script }

Runs a script on the PC, with no window. **Script** picks where it comes from:

- **Written here**: pick the **Language** (PowerShell, Python or Command Prompt `.bat`) and type the
  **Code** in the button. DroidDeck saves it to `%LocalAppData%\DroidDeck\scripts\` and runs it from
  there.
- **A file on the PC**: the **File** path of a `.ps1`, `.py`, `.bat` or `.cmd`.

**Arguments (optional)** are separated by spaces; use quotes for an argument with spaces
(`"my game" 2`). They arrive in `$args` (PowerShell), `sys.argv` (Python) or `%1`, `%2`… (`.bat`), so one
script file can serve several buttons, each with its own arguments.

??? note "How scripts run"
    - PowerShell runs with `-NoProfile -ExecutionPolicy Bypass`. Python uses the `py` launcher from the
      official installer, or `python` from the `PATH`. The working folder is the script's folder.
    - A script that runs for more than **5 minutes** is stopped.
    - Output isn't shown on the button. Errors (non-zero exit code) go to DroidDeck's log. To show a
      value on a button, use the [Command output](monitors.md#information) info instead.

!!! warning "Created on the PC only"
    A script can do anything your Windows account can, so, like keyboard shortcuts, script buttons can
    only be created or changed in the configurator on the PC. The phone and browser decks run them
    normally.

## Steam game { #steam }

Lists the games installed in the PC's Steam (every library). Picking one gives the button the game's
name and cover; tapping it launches the game through Steam. The cover comes from Steam's cache: if a
game shows no image, open your Steam library once so it downloads the covers.

## Size { #size }

Under **Size** in the editor, **Width (cells)** and **Height (cells)** make a button span up to 4 × 4
cells: a big mute button, a tall [slider](monitors.md#volume-slider), a wide clock. The
button's position is its top-left cell.

A large button never covers other buttons: it stops where another button is, and it's cut at the edge
of the grid. To make room, move the neighbours first.

## Long press and double tap { #gestures }

Besides the normal tap, a button can have one action for **holding** and another for **double
tapping** (the editor's **Long press and double tap** section). They take the same action types as the
tap.

- Example: a timer that starts on tap and resets on long press; an OBS scene on tap and recording on
  double tap.
- Only buttons with a double-tap action wait a moment (~0.3 s) before running the single tap, to tell
  them apart. Every other button reacts right away.
- On a button with a long-press action, holding runs it; to edit that button on the phone, use
  [edit mode](profiles.md#edit-mode).

## Two states { #two-states }

In the editor's **Two states** section:

- **Toggle on each tap** turns the button on with one tap and off with the next, for actions with no
  state of their own (a hotkey that toggles something in a game, for example). The state lives on the
  PC: every phone shows the same one and it survives a restart.
- **Action when on** (optional) runs on the tap that turns the button off. Example: one tap starts
  recording with a hotkey and the next sends the stop hotkey. Without it, the same action runs every
  time.
- **Label when active** and **Icon when active** change the look while the button is active, through
  the toggle or through the action's own state (Discord mute, OBS recording, a light on...). The
  **active colour** sits with the button's colours.

## Button state

Some buttons reflect live state:

| Action | State shown |
|---|---|
| Media play/pause | ▶ / ⏸ icon |
| Mixer (app mute) | Volume icon and active colour while muted |
| Discord | Active colour while you are muted/deafened |
| OBS | Lit for the current scene, or while recording/streaming/virtual cam/replay buffer is on |
| Tuya | On/off, brightness level and the lamp's real colour |
| Timer | Time left or elapsed, pomodoro phase and active colour at the end |
| Windows (audio) | Active colour while the device is the default |
| Home Assistant | Lit while the entity is on (with the light's brightness and colour), or the sensor value |
| Spotify | Play/pause, liked, shuffle and repeat; the cover on "show what's playing" |
| Twitch / YouTube | Live and viewers, on "status only" |
| Two states | Active colour, label and icon while on |
