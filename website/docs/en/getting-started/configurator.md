# Configurator

The configurator is the editor for your decks. It runs in the PC's browser and is served by
DroidDeck itself — open it from the tray (**Abrir no navegador**) or go to
**`http://localhost:4787/`**.

!!! note "Use it on the PC itself"
    The configurator signs in automatically only from the same machine (`localhost`). Opened from
    another computer on the network, its requests are rejected with *401 Unauthorized*.

## Layout

The screen has three panels:

1. **Profiles** — your list of decks. **New** creates one; the trash icon deletes it after confirmation.
2. **Grid** — the buttons of the selected profile. Click a cell to select it.
3. **Properties** — label, icon, colours and the action of the selected button. Click **Save Changes**.

- **Drag and drop** a button to move it. Dropping it on an occupied cell swaps the two.
- The pencil icon renames the profile.
- Shortcuts in the sidebar open the **Discord**, **OBS** and **Soundboard** settings.

Every save is pushed to the phone immediately.

## Grid size

The grid size is decided by the phone: it calculates how many buttons (about 96 px each) fit on its
screen and reports it to the PC. Rotating the phone updates the grid. The configurator shows the
same grid, so what you edit matches what you see on the phone.

## Profiles, pages and folders

- On the phone, **each profile is a page** — swipe sideways to switch.
- A button with the **Abrir perfil** (*open profile*) action opens another profile as a **folder**.
  Use a **Voltar** (*back*) button or the back arrow in the title bar to return.
- New profiles are created in the configurator. If you have none, a *Default* profile is created
  automatically.

## Editing on the phone

You can also edit on the phone itself:

- **Tap** a button to run it; **long-press** to edit it.
- **Tap an empty cell** to create a button there.
- The gear icon in the title bar renames the profile.

## Button properties

| Property | Description |
|---|---|
| **Label** | Text shown on the button. Media and soundboard buttons fill it automatically when empty. |
| **Icon** | One of 16 built-in icons, or a custom image from your gallery (up to 512×512, stored in the profile). |
| **Background colour** | Button colour. |
| **Active colour** | Colour used when the button's state is "on" (muted mic, light on…). Available for Discord, mixer and Tuya actions. |
| **Action type** | What the button does — see [Buttons & actions](../guide/actions.md). |
| **Dynamic feature** | Turns the button into a live monitor — see [Monitors](../guide/monitors.md). |

## Backup

Profiles are JSON files in `%AppData%\DroidDeck\Profiles\`. To back up your decks, or move them to
another PC, copy that folder. See [Files & settings](../reference/files.md).
