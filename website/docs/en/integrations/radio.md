# Radio: mAirList and RadioDJ

DroidDeck controls two radio automation programs. For the **on-air** light, see [Tally and ON AIR](tally.md);
for the microphone cough button, see [Voicemeeter](voicemeeter.md).

## mAirList

Uses the [mAirList](https://www.mairlist.com) REST interface.

1. In mAirList, open **Configuration → Remote Control** and add a **REST** interface (the default port is
   `9300`).
2. Create a user and turn on the **ExecuteCommand** (commands) and **GeneralRead** (on-air state) plugins.
3. Open **Settings → Integrations → mAirList** in DroidDeck and fill in the address, username and password (the
   password is stored encrypted).

According to the mAirList forum, the REST interface needs the **Advanced** edition or higher.

**mAirList** buttons:

- **On air:** toggle, go on air and go off air. The button turns red while on air.
- **Automation:** toggle, on and off, per playlist.
- **Playlist and players:** next, play, stop and fade out.
- **Cartwall:** play a cart and stop all of them.
- **Other command:** any
  [mAirList remote command](https://wiki.mairlist.com/reference:remote_control_commands).

## RadioDJ

Uses the **REST Server** plugin for [RadioDJ](https://www.radiodj.ro).

1. In RadioDJ 2, copy `Plugin_RestServer` from the **Disabled Plugins** folder to the **Plugins** folder and open
   RadioDJ.
2. In the plugin options, set the address, port and password, and start the server.
3. Open **Settings → Integrations → RadioDJ** in DroidDeck and fill in the same address and password.

**RadioDJ** buttons:

- **Player:** play a playlist track, stop, pause, resume, restart and play from the intro.
- **Modes:** AutoDJ, assisted and live input, with the state on the button.
- **Events and carts:** turn events on and off, play a cart.
- **Load:** a playlist or a track, by ID.
- **Other command:** a command from the list in the plugin options.

!!! warning "ID commands on version 2.0.4"
    The RadioDJ forum has reports of errors with **Load a playlist** and **Put a track on top** on version 2.0.4,
    while other commands work. The developer couldn't reproduce the problem.

RadioDJ doesn't report whether the player is paused, so the pause buttons don't show that state.
