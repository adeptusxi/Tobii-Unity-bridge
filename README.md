# macOS Tobii Eye Tracker → Talon → Unity gaze bridge

Gaze input for Unity on macOS, where the official Tobii Unity SDK does not run.
This system streams the gaze point from Talon to a local UDP port and consumes it in Unity.

```
Tobii → Talon → [ Talon Python gaze callback → localhost UDP → Unity C# receiver ]
```

All traffic is loopback only; no gaze data leaves the local machine.

| File | Contents |
| --- | --- |
| `talon/gaze_bridge.py` | Talon user script: reads gaze, sends UDP |
| `tools/gaze_monitor.py` | UDP listener for inspecting the stream |
| `tools/test_gaze_bridge.py` | Offline self-test, no Talon or tracker required |
| `Unity/Tobii-Unity-bridge/` | Unity project and the `com.talonbridge.gaze` package |


## 1. Talon side

### Requirements

- Talon (tested against 0.4.0) on macOS with the Tobii eye tracker attached (tested against model 4C).
- The tracker calibrated in Talon. This bridge does not replace Tobii/Talon
  calibration.
- Talon's macOS Accessibility permission, which the bridge uses to read the
  Unity window rect.

### Install

Symlink the script into the Talon user directory:

```sh
ln -s "$PWD/talon/gaze_bridge.py" ~/.talon/user/gaze_bridge.py
```

The script should start streaming on load and log `[gaze_bridge] streaming gaze to 127.0.0.1:5005`, followed by `[gaze_bridge] attached to tracker …` once a tracker is present. The log is at
`~/.talon/talon.log`.

`bridge_start()`, `bridge_stop()` and `bridge_status()` can be called from the
Talon REPL (`~/.talon/bin/repl`) for manual control and counters.

### Verify the stream

```sh
python3 tools/gaze_monitor.py          # live status line
python3 tools/gaze_monitor.py --raw    # one JSON packet per line
```

Expect ~60–90 Hz, `OK LR`, and `screen` coordinates that follow your gaze:
`0,0` is the top-left of the main screen, `1,1` the bottom-right. Covering the
tracker or looking away drops validity to `---` within ~250 ms.

Only one process can receive on a UDP port, so stop the monitor before running
the Unity player.

### Configuration

Constants at the top of `talon/gaze_bridge.py`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `HOST` / `PORT` | `127.0.0.1` / `5005` | UDP destination |
| `MAX_RATE_HZ` | `0` | `0` sends every frame; otherwise caps the send rate |
| `HEARTBEAT_INTERVAL` | `0.25` s | Idle "gaze invalid" packet interval |
| `WINDOW_MATCH` | `("unity",)` | Substrings matched against a window's app name, bundle id and executable path |
| `WINDOW_EXCLUDE` | Unity Hub, etc. | Window titles to ignore |
| `WINDOW_POLL` | `250ms` | Window rect refresh interval |
| `TRACKER_POLL` | `1s` | Tracker attach/detach poll interval |
| `DEBUG` | `False` | Verbose logging; never logs per packet |

### Self-test

`tools/test_gaze_bridge.py` loads the bridge against stub Talon modules and
asserts the emitted packets: eye combination, validity, window mapping,
heartbeat and shutdown.

```sh
python3 tools/test_gaze_bridge.py      # -v dumps the packets
```

---

## 2. Wire protocol (v1)

One JSON object per UDP datagram, no framing; the newest packet supersedes any
earlier one.

```json
{"v":1,"t":38793.1,"wall":1788541233.48,"ok":1,"eyes":3,
 "sx":0.5,"sy":0.25,"scr":[0,0,1728,1117],
 "win":[100,50,800,600],"title":"MyGame","wx":0.955,"wy":0.382,"in":1}
```

| Field | Meaning |
| --- | --- |
| `v` | Protocol version (`1`) |
| `t` | Talon timestamp, seconds on the `time.perf_counter()` clock |
| `wall` | Unix time at send, for latency measurement |
| `ok` | `1` if this is a valid gaze sample |
| `eyes` | Bitmask of tracked eyes: `1` left, `2` right |
| `sx`, `sy` | Gaze normalized to the main screen, `0,0` = top-left. Absent when `ok` is `0` |
| `scr` | Main screen rect `[x, y, w, h]` in macOS logical points |
| `win` | Matched Unity window rect `[x, y, w, h]` in logical points. Absent if no window matched |
| `title` | Matched window title, truncated to 64 characters |
| `wx`, `wy` | Gaze normalized to the Unity window, `0,0` = its top-left |
| `in` | `1` when `wx`/`wy` fall inside `[0, 1]` |

Invalid samples are sent as `ok:0` without coordinates, so tracking loss
reaches the consumer immediately rather than through a timeout. While no frames
arrive, an `ok:0` heartbeat goes out every 250 ms, distinguishing a running
bridge from a stopped one.

A compact binary encoding is a possible future optimization; at ~90 Hz over
loopback, JSON is not a bottleneck.

## 3. Coordinate conventions

- Talon reports gaze normalized to `[0, 1]` against the main screen, origin
  top-left, y increasing downward. This project uses that convention
  throughout.
- Screen and window rects are in macOS logical points, not backing pixels;
  retina scale is not applied. Normalizing against the window rect (`wx`, `wy`)
  keeps the Unity side independent of display scaling — it only multiplies by
  `Screen.width` / `Screen.height`.
- Unity's y axis points up, so the Unity-side conversion is
  `screenX = wx * Screen.width`, `screenY = (1 - wy) * Screen.height`.

### Not supported

- **Multiple monitors and secondary displays.** Gaze is normalized against the
  main screen, which is what Talon calibrates against.
- **Unity Editor Game view alignment.** `win` is the whole editor window rather
  than the docked Game view, so window-relative coordinates are exact only for
  a standalone player or a maximized Game view. The Unity side will provide a
  manual alignment path for editor testing.

## 4. Unity side

Not implemented yet.