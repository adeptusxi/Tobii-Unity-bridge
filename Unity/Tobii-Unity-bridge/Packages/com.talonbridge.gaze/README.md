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
(0.1 s by default), while `NormalizedPosition` keeps the last valid value.
