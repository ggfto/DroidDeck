# Multi-action

A **Multi-action** button runs several steps one after the other. Each step has a **delay** (ms) that
is waited **before** it runs. The editor offers three step kinds:

- **Hotkey**: keys, with the [keyboard shortcut](hotkeys.md) syntax;
- **Open app**: a path, like the [Open program](actions.md#launch-app) action;
- **Mute app**: toggles mute of a process.

Example, "Start meeting": open the meeting app, wait 3000 ms, send `^+m`.

Steps of other types (Discord, OBS, scripts..., for example from a
[Stream Deck import](import-share.md#stream-deck)) are kept and run, even though the editor only
creates these three kinds.

## Conditions { #conditions }

Each step has an **Only if** field. With a condition, the step only runs when it's met; otherwise it
is skipped and the multi-action moves on. **Not** inverts it: the step runs when the condition is
*not* met. The default, **Always**, runs the step every time.

| Only if… | Value |
|---|---|
| **The OBS stream is live** | None |
| **OBS is recording** | None |
| **The Discord mic is muted** | None |
| **Discord audio is deafened** | None |
| **A two-state button is on** | The [two-state](actions.md#two-states) button, picked from a list. |
| **An app is open** | Process name without `.exe`, e.g. `Spotify`. |
| **An app is focused** | Process name of the window in focus. |
| **It is between two times** | `08:00-18:00`. A range that crosses midnight works: `22:00-06:00`. |
| **It is one of these weekdays** | `0` = Sunday … `6` = Saturday, with ranges and commas: `1-5`, `0,6`. |
| **A tally target is on air** / **in preview** | A scene, input, microphone or the manual signal, from the [tally](../integrations/tally.md) list. |

- The condition is checked when the step's turn comes, after its delay. The delay is waited even if
  the step is then skipped.
- Example, one "Go live" button: start streaming; then, *only if* `obs64` is **not** focused, switch
  to the stream scene; and *only if* it's between `18:00-23:59`, turn the desk light on.

## Record from the keyboard { #record }

In the PC configurator, **Record from keyboard** records what you type on the PC's keyboard and
turns it into hotkey steps: go to the program, type the sequence and click **Stop**.

- Keys typed in a row become a single step (`hi{ENTER}`); a pause longer than half a second starts a
  new step, with the delay rounded to 100 ms.
- Recording stops on its own after 60 seconds.

!!! warning "Review before saving"
    Because it reads the whole keyboard, recording only works in the configurator open on the PC
    itself; the phone doesn't have this button. Everything typed goes in, passwords included.

!!! info "Created on the PC only"
    A multi-action with a hotkey, program, script or HTTP step can only be created or changed in the
    [configurator](../getting-started/configurator.md) on the PC. The phone runs it normally.
