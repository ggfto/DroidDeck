# Soundboard

Play sound effects from the deck — into your speakers, and into your microphone so people on
Discord or your stream hear them.

## Sound sources

**MyInstants** (default)
:   Search the [MyInstants](https://www.myinstants.com) library in the button editor, preview the
    sound on the PC, and save it to the button. The label is filled with the sound's title. Sounds
    are downloaded once and cached on the PC.

**Discord**
:   Plays a sound from your server's **native Discord soundboard** in the voice channel you are in.
    Requires the [Discord integration](discord.md) and being in a voice channel. Only server sounds
    are listed; using sounds from another server requires Nitro. These sounds are not heard on
    your stream/OBS.

The **Stop** operation stops whatever is playing. A new sound interrupts the previous one.

## Getting the sound into your microphone

DroidDeck plays MyInstants sounds on an audio device of your choice. To make them reach Discord,
games or OBS, use a virtual audio cable:

1. Install [VB-Cable](https://vb-audio.com/Cable/) (free).
2. In DroidDeck's **Soundboard** settings, set **Saída cabo** (*cable output*) to **CABLE Input**.
3. Turn on **Tocar também no monitor** (*also play on monitor*) and pick your headphones, so you
   hear the sounds too.
4. In Discord (or OBS), use **CABLE Output** as the microphone / an audio input capture.

!!! tip "Talking and playing sounds at the same time"
    With VB-Cable alone, Discord hears only the cable, not your real mic. To mix both, use
    [Voicemeeter](https://vb-audio.com/Voicemeeter/), or add your real mic to OBS as a separate source.

Other settings: **volume** (0–100) and **Parar tudo** (*stop all*). If the output is left empty,
Windows' default device is used.
