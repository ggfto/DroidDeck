# Voicemeeter

DroidDeck talks to [Voicemeeter](https://vb-audio.com/Voicemeeter/) (Standard, Banana or Potato) running on
the same PC through the **Voicemeeter Remote API**. There is nothing to set up: create a button of type
**Voicemeeter** and pick what it does.

DroidDeck loads `VoicemeeterRemote64.dll` from the folder where Voicemeeter was installed (found through
the Windows registry, or `C:\Program Files (x86)\VB\Voicemeeter`). If Voicemeeter isn't installed, or isn't
running, the button does nothing and the log says why.

## What you can do

| Operation | Parameters |
|---|---|
| Mute (toggle) / Mute / Unmute | **Where**: input (Strip) or output (Bus); **Number** counting from the left (1 = the first) |
| Set gain | Where, number and gain in dB (-60 to 12) |
| Gain (+/-) | Where, number and step in dB (the result stays between -60 and 12) |
| Press a Macro Buttons button | The button id in Macro Buttons (the first is 0). Pressed and released, like a click |
| Set a parameter | Any numeric Remote API parameter, e.g. `Strip[0].A1`, `Bus[1].Gain`, `Fx.Reverb.On`, and a value |
| Toggle a parameter (0/1) | Same, flipping between 0 and 1 (e.g. route a strip to A1 on and off) |

!!! tip
    Parameter names are 0-based (`Strip[0]` is the first input), unlike the **Number** field. The full list
    is in the *Voicemeeter Remote API* PDF that comes with Voicemeeter.

The buttons don't show Voicemeeter's state (e.g. muted). For a button that looks different while on, use a
[two-state button](../guide/actions.md#two-states) with **Mute** and **Unmute**.

## Cough button

The **Cut while held (cough)** operation mutes the input only while your finger is on the button. When you let go, it returns to what it was: if it was already muted, it stays muted. If the phone loses its connection with a finger on the button, the PC restores the microphone by itself, so it doesn't stay muted on air.

!!! warning "Cough button not yet tested with the real program"
    The cut was tested with a simulator, not with real Voicemeeter. Test it before using it on air. If something
    doesn't work, [open an issue](https://github.com/ggfto/DroidDeck/issues).
