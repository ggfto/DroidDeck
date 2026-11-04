# Network: OSC, TCP and UDP

A **Network (OSC, TCP, UDP)** button sends a message from the PC to another device or program: mixing consoles like
the X32 and M32, QLab, lighting controllers and anything else that takes commands over the network.

!!! note "Created on the PC only"
    The message leaves from the PC, with its network access. So, like the HTTP request, this button can only be
    created or changed in the PC configurator. The phone only presses it.

## Fields

| Field | Meaning |
|---|---|
| **Protocol** | OSC, UDP or TCP. OSC goes over UDP. |
| **Address** / **Port** | The device's IP or name and the port it listens on. See its manual. |
| **OSC path** | OSC only. Starts with `/` and has no spaces, like `/ch/01/mix/on`. |
| **Arguments (optional)** | OSC only. One per line, see below. |
| **Message** | UDP and TCP. The text or bytes to send. |
| **Format** | UDP and TCP. **Text**, or **Hexadecimal** (`DE AD 01`). |
| **Message ending** | Text only. Nothing, LF, CR + LF or CR. Many devices only accept a command that ends in one of these. |

## OSC arguments

| Line | Type |
|---|---|
| `i:1` | Integer |
| `f:0.5` | Decimal |
| `s:text` | Text |
| `T` or `F` | True or false |

With no prefix, the type is inferred: `7` becomes an integer, `0.5` a decimal, and anything else text. Use the prefix
when the device requires a type: `f:1` sends the decimal `1.0`, and `s:12` sends the text `12`.

!!! example "Example: mute channel 1 on an X32"
    Protocol **OSC**, port `10023`, path `/ch/01/mix/on`, argument `i:0`. Check the path in your console's manual.

!!! warning "No response from the device"
    DroidDeck only sends. UDP and OSC don't confirm delivery, so the button can't tell whether the device got the
    message and doesn't show a state. This integration was tested with a test receiver, not a real console. If
    something doesn't work, [open an issue](https://github.com/ggfto/DroidDeck/issues).
