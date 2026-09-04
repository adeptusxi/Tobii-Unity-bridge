# Talon Gaze Bridge

Gaze input for Unity on macOS, sourced from a Tobii eye tracker through Talon.

The package is transport-agnostic: game code reads gaze through
`IGazeProvider` / `GazeService`, and the provider behind it can be the Talon UDP
bridge or anything else implementing the interface.

## Layout

| Assembly | Contents |
| --- | --- |
| `TalonBridge.Gaze` | Runtime: samples, coordinates, provider base, service, providers, debug cursor |
| `TalonBridge.Gaze.InputSystem` | Optional. Pointer position via com.unity.inputsystem; excluded when the package is absent |
| `TalonBridge.Gaze.Editor` | Editor tooling |

## Reading gaze

```csharp
using TalonBridge.Gaze;

if (GazeService.HasValidGaze)
{
    Vector2 screen = GazeService.GazePositionScreen;
}
```

Or subscribe:

```csharp
GazeService.SampleReceived += sample => { ... };
```

## Coordinates

Normalized gaze is `0,0` at the **top-left** of the game window and `1,1` at the
bottom-right. `GazeCoordinates` converts to and from Unity screen pixels, which
are `0,0` at the bottom-left:

```csharp
screen = new Vector2(n.x * Screen.width, (1f - n.y) * Screen.height);
```

Normalizing against the window rather than the display keeps the conversion
independent of macOS display scaling.

## Providers

Providers register with `GazeService` when enabled; the highest `Priority` wins.
`IsValid` goes false when no valid sample has arrived within `Validity Timeout`
(0.1 s by default), while `NormalizedPosition` keeps the last valid value. A
dropped packet or two therefore does not interrupt gaze, while a real tracking
loss does.

### UdpGazeProvider

Receives the Talon bridge's stream on `127.0.0.1` at the configured port. A
background thread owns the socket and decodes packets; the main thread publishes
the newest sample each frame, so superseded samples are discarded rather than
queued and no Unity API is touched off the main thread. The steady state
allocates nothing per packet.

`Coordinate Source` selects which coordinates from a packet to use:

| Source | Behaviour |
| --- | --- |
| `Auto` | The Game view's rect in the editor; window-relative in a player, else screen-relative |
| `Window` | Window-relative only |
| `Screen` | Screen-relative, for a build filling the main screen |
| `GameView` | The same Game view mapping, requested explicitly |

The Game view mapping covers editor testing, where the bridge sees the whole
editor window rather than the docked Game view. The rect comes from the editor's
own Game view geometry, so it tracks docking, resizing, zoom and letterboxing.
`Require Inside Surface` treats gaze outside the surface as invalid.

`Diagnostics` exposes packets received, parse errors, invalid packets, packet
rate, time since the last packet, transport latency and the last socket error.
