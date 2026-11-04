# Automations

An **automation** presses a deck button by itself when something happens on the PC — at a set time, when
an app opens or closes, when the PC wakes up, when something goes on air. You don't touch the phone.

Automations don't have actions of their own: they press a button that already exists. Create the button
with the action you want first (it can be on a profile you don't use day to day), then point an
automation at it.

## Create an automation

Open **Automations**: in the phone app under **Settings → Automations**, or in the PC configurator's
sidebar. Tap **New automation** and fill in:

| Field | Meaning |
|---|---|
| **Name (optional)** | Shown in the list. Without a name, the list shows the event. |
| **When** | The event — see below. |
| **Time** / **Days (optional)** | For *At a time*: `HH:mm` in 24-hour format, e.g. `09:00`. Days use `0` = Sunday … `6` = Saturday, with ranges and commas: `1-5` (weekdays), `0,6` (weekend). Empty = every day. |
| **App process** | For the app events: the process name without `.exe`, e.g. `obs64`, `Spotify`, `chrome`. |
| **What to follow** | For the tally events: a scene, input, microphone or the manual signal. The list only shows what is connected now. |
| **Press the button** | Any button with an action, from any profile (`Profile · label`). |

| Event | When it fires |
|---|---|
| **At a time** | Once at that minute, on the chosen days. |
| **When an app opens** | When the process starts. |
| **When an app closes** | When the process ends. |
| **When the PC wakes from sleep** | After sleep or hibernation. |
| **When DroidDeck starts** | A few seconds after DroidDeck starts — right after you log in, if it starts with Windows. |
| **When a tally target goes on air** | When the [target](../integrations/tally.md) turns red. |
| **When a tally target goes off air** | When it stops being red. Losing the connection doesn't count, because there's no way to know: if OBS closes, nothing fires; when it comes back, the automation compares with the last known state. |

The switch next to each automation turns it on or off. **Test now** (in the item's menu) presses the
button immediately, without waiting for the event; **Delete** removes it.

## How it behaves

- Automations run on the PC: they work with the phone off or disconnected.
- The PC checks every couple of seconds, so an app event can take up to about 2 s to fire. Tally events fire
  right away.
- The button runs just as if you had tapped it, including a [two-state](actions.md#two-states) button,
  which switches state. A [multi-action](multi-action.md) button keeps its
  [step conditions](multi-action.md#conditions).
- If the button is deleted, the automation stays in the list as *(button deleted)* and does nothing.
- Since an automation only presses saved buttons, it can't do anything a button couldn't already do.

!!! example "Ideas"
    - At `18:00` on `1-5`, press a button that starts your "end of day" multi-action.
    - When `obs64` opens, press a button that turns on your Key Light; when it closes, another that
      turns it off.
    - When the PC wakes, switch the default audio device back to your headphones.
    - When the presenter's microphone opens in Voicemeeter, turn on a red Hue light in the hallway; when it
      closes, turn it off.

Automations are saved in `%AppData%\DroidDeck\triggers.json` (see [Files & settings](../reference/files.md)).
