# Talon → Unity bridge

Unity package and Python UDP bridge for receiving eye tracking data from [Talon](https://talonvoice.com/) and making it available to Unity applications. Developed to send Tobii eye tracker data to Unity games on macOS, which is not currently supported by the Tobii Unity SDK. 

This is an independent project and is not affiliated with, endorsed by, or officially supported by Tobii or Talon. 

This project was developed with the assistance of Claude Code. 

## Overview 
Talon reads the eye tracker, a Talon script streams the gaze point to a local
UDP port, and a Unity package receives it and exposes it to game code for interactions with UI, 3D scenes, and shaders. All traffic is loopback only; no gaze data leaves the local machine.

## Repository layout

Note: The Unity project is not at the repository root.

```
talon/
  gaze_bridge.py                Talon user script: reads gaze, sends UDP
tools/
  gaze_monitor.py               UDP listener for inspecting the stream
  test_gaze_bridge.py           Offline self-test, no Talon or tracker required
  pack_package.sh               Builds the package as a tarball
Unity/Talon-Unity-bridge/       Demo Unity project (URP)
  Assets/Scenes/
    GazeDemoUI.unity            Dwell buttons, UI shader, spectator view
    GazeDemo3D.unity            3D gaze, world shader, fullscreen shader
  Packages/com.talonbridge.gaze/    The Talon Gaze Bridge package
    Runtime/                    Providers, interaction, filtering, shaders, spectator view
    Editor/                     Monitor window, inspectors, creation menu
    ShaderLibrary/              Gaze.hlsl
    Samples~/                   Importable samples
    README.md                   Component reference
```

To use gaze in your own game you need `talon/gaze_bridge.py` and the Unity package.

## Requirements

- macOS, single display.
- Talon (tested against 0.4.0) with a Tobii eye tracker attached (tested
  against the 4C) and calibrated in Talon. The bridge does not calibrate.
- Talon's macOS Accessibility permission, which the bridge uses to read the
  Unity window rect.
- Unity 6000.0 or newer. The gaze shader sample needs URP; everything else is
  independent of the render pipeline.

## Quick start

### 1. Install the Talon script

Symlink the script into the Talon user directory:

```sh
ln -s "$PWD/talon/gaze_bridge.py" ~/.talon/user/gaze_bridge.py
```

Talon loads it at once and reloads it whenever the file is saved. It logs
`[gaze_bridge] streaming gaze to 127.0.0.1:5005`, followed by
`[gaze_bridge] attached to tracker …` once a tracker is present. The log is at
`~/.talon/talon.log`.

### 2. Check the stream

```sh
python3 tools/gaze_monitor.py          # live status line
python3 tools/gaze_monitor.py --raw    # one JSON packet per line
```

Expect ~60–90 Hz, `OK LR`, and `screen` coordinates that follow your gaze:
`0,0` is the top-left of the main screen, `1,1` the bottom-right. Covering the
tracker or looking away drops validity to `---` within ~250 ms.

Only one process can receive on a UDP port. Stop the monitor before entering
play mode in Unity.

### 3. Add the package to your Unity project

In Package Manager, **Add package from git URL**:

```text
https://github.com/adeptusxi/Talon-Unity-bridge.git?path=Unity/Talon-Unity-bridge/Packages/com.talonbridge.gaze
```

The `?path=` suffix is required because the package is not at the repository
root. With a local clone, **Add package from disk** and pick
`Unity/Talon-Unity-bridge/Packages/com.talonbridge.gaze/package.json` instead.

To get the package in a single file, build a tarball and install it
with **Add package from tarball**:

```sh
tools/pack_package.sh      # writes dist/com.talonbridge.gaze-<version>.tgz
```

### 4. Set up a scene

Everything is under `GameObject ▸ Talon Gaze Bridge`:

1. **Talon Gaze Rig** creates the UDP receiver and a debug crosshair. Enter
   play mode with Talon running and the crosshair follows your gaze.
2. **Gaze Pointer** adds dwell activation for UI. The scene also needs an
   `EventSystem`.
3. Add a `GazeInteractable` component to each UI element that should react to
   gaze. Wire its `Activated` event in the inspector or from code.
4. **Gaze World Pointer** adds the 3D gaze position, if the game needs one.

### 5. Use gaze from code

Game code reads gaze through `GazeService`, and never touches UDP or Talon:

```csharp
using TalonBridge.Gaze;

if (GazeService.HasValidGaze)
{
    Vector2 screen = GazeService.GazePositionScreen;          // Unity pixels
    Vector2 normalized = GazeService.GazePositionNormalized;  // 0..1, top-left origin
}
```

React to dwell on a UI element:

```csharp
interactable.Activated.AddListener(() => Fire());
interactable.DwellProgressed.AddListener(progress => bar.fillAmount = progress);
```

Read the 3D position:

```csharp
if (worldPointer.HasWorldPosition)
{
    Vector3 lookedAt = worldPointer.GazePositionWorld;
}
```

## Working without the Tobii

`GameObject ▸ Talon Gaze Bridge ▸ Mouse Gaze Rig` creates a `MouseGazeProvider`,
which reports the mouse pointer as gaze. Everything downstream behaves the same,
so gaze features can be built and debugged with no tracker and no Talon. It can
add jitter to approximate tracker noise and simulate tracking loss.

When a mouse rig and a Talon rig are both in a scene, the one with the higher
`Priority` is used.

## Features

Settings and API for each are in the
[package README](Unity/Talon-Unity-bridge/Packages/com.talonbridge.gaze/README.md).

- **Dwell activation.** A `GazeInteractable` activates after gaze rests on it
  for a configurable duration, with a cooldown before it can fire again.
  Progress resets when gaze leaves and stops while tracking is lost.
- **Region activation.** `Region Radius` on the pointer lets gaze activate
  targets near the gaze point, not only under it.
- **Dwell feedback.** Presentation is separate from detection: implement
  `IGazeFeedback`, or use the radial fill, scale and outline components in the
  UI Interaction Feedback sample.
- **3D gaze.** `GazeWorldPointer` raycasts from the camera through the gaze
  point and reports the world position and the collider under it.
- **Smoothing.** Off by default. Add `OneEuroGazeFilter` or
  `MovingAverageGazeFilter` to the gaze rig.
- **Gaze shaders.** `GazeShaderGlobals` publishes gaze to all shaders. The Gaze
  Shaders sample has a fullscreen effect, a 3D material and a UI material.
- **Spectator view.** `GazeSpectatorView` draws a minimal cursor and trail over
  the game for streams and recordings.
- **Debug view.** `GazeDebugCursor` for diagnostics, `GazeCanvasCursor` for a
  cursor styled as part of the UI.
- **Talon Gaze Monitor.** `Window ▸ Talon Gaze Monitor` shows live gaze
  coordinates, the current target, the 3D position and transport statistics.

## Coordinates

- Gaze is normalized to `[0, 1]` with the origin at the **top-left** and y
  increasing downward, the same convention Talon uses.
- Unity screen space has its origin at the bottom-left. `GazeCoordinates` does
  the conversion: `screenX = x * Screen.width`,
  `screenY = (1 - y) * Screen.height`.
- **Windowed and fullscreen.** The Talon script sends gaze normalized to the
  main screen and to the Unity window. `Source`, under `Mapper` on the
  receiver, picks which to use; the default, `Auto`, handles a windowed player, a
  fullscreen player and the editor's Game view without changing the setting.
  Gaze outside the game window is reported as invalid.
- **Retina displays.** Screen and window rects are macOS logical points, not
  backing pixels. Gaze reaches Unity already normalized against the window, so
  display scaling does not affect it.

## Talon script reference

### Configuration

Constants at the top of `talon/gaze_bridge.py`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `HOST` / `PORT` | `127.0.0.1` / `5005` | UDP destination. `PORT` must match `Port` on the Unity receiver |
| `MAX_RATE_HZ` | `0` | `0` sends every frame; otherwise caps the send rate |
| `HEARTBEAT_INTERVAL` | `0.25` s | Idle "gaze invalid" packet interval |
| `WINDOW_MATCH` | `("unity",)` | Substrings matched against a window's app name, bundle id and executable path |
| `WINDOW_EXCLUDE` | Unity Hub, etc. | Window titles to ignore |
| `WINDOW_POLL` | `250ms` | Window rect refresh interval |
| `TRACKER_POLL` | `1s` | Tracker attach/detach poll interval |
| `DEBUG` | `False` | Verbose logging; never logs per packet |

A window is matched when its app name, bundle id or executable path contains
one of the `WINDOW_MATCH` substrings. A built game often contains none of
them: add your game's name. When no window matches, only screen-relative gaze
is sent.

### Manual control

`bridge_start()`, `bridge_stop()` and `bridge_status()` can be called from the
Talon REPL (`~/.talon/bin/repl`).

### Self-test

`tools/test_gaze_bridge.py` loads the bridge against stub Talon modules and
asserts the emitted packets: eye combination, validity, window mapping,
heartbeat and shutdown.

```sh
python3 tools/test_gaze_bridge.py      # -v dumps the packets
```

## Wire protocol (v1)

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

## Not supported

- **Multiple monitors and secondary displays.** Gaze is normalized against the
  main screen, which is what Talon calibrates against.
- **Focal depth.** The tracker reports a gaze direction, not a depth. The 3D
  position is the first surface along the gaze ray.
- **Packet history and replay.** Only the newest sample is kept.
- **Gaze-driven OS cursor.** Gaze does not move the system pointer.
- **Windows and Linux.** Only macOS has been tested.
- **Blink Handling.** Blinks can cause the gaze point given by Talon to jump away for a moment.

## Troubleshooting

`tools/gaze_monitor.py` shows what Talon is sending, and
`Window ▸ Talon Gaze Monitor` in Unity shows what was received. The package
README has a
[symptom table](Unity/Talon-Unity-bridge/Packages/com.talonbridge.gaze/README.md#troubleshooting).
A syntax error in the Talon script stops the stream silently; check
`~/.talon/talon.log`.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
