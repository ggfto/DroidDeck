# Monitors

A button can show live PC statistics instead of running an action. In the properties panel, choose a
**Dynamic feature**:

| Monitor | Shows |
|---|---|
| **CPU** | Total processor usage, as a gauge. |
| **Memory** | RAM usage, with "used/total GB". |
| **GPU** | 3D engine usage of the GPU, as a gauge. |
| **Network** | Download ↓ and upload ↑ speed across all active network adapters (K = KB/s, M = MB/s). |

Gauges change colour with load: **green** up to 60 %, **orange** above 60 %, **red** above 85 %.

- Values update **every second** while a phone is connected. The PC stops collecting them when no
  phone is connected.
- A monitor button is display-only: choosing a monitor removes the button's action, and tapping it
  does nothing.
- GPU usage comes from Windows performance counters and needs a reasonably recent graphics driver.
  It shows 0 % when unavailable.
