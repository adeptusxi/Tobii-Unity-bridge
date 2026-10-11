# Talon Gaze Bridge

Gaze input for Unity on macOS, sourced from a Tobii eye tracker through Talon.

Game code reads gaze through `IGazeProvider` / `GazeService`. The provider
behind it can be the Talon UDP bridge, the mouse simulator, or anything else
implementing the interface.

## Installation

Unity 6000.0 or newer. Depends on `com.unity.ugui` and
`com.unity.modules.physics`. The Gaze Shaders sample also needs URP; nothing
else depends on a render pipeline.

In Package Manager, **Add package from git URL**:

```text
https://github.com/adeptusxi/Tobii-Unity-bridge.git?path=Unity/Tobii-Unity-bridge/Packages/com.talonbridge.gaze
```

The `?path=` suffix is required: the package is not at the repository root.
With a local clone, **Add package from disk** and pick this folder's
`package.json` instead. A tarball built with the repository's
`tools/pack_package.sh` installs with **Add package from tarball**.

The Talon side is set up separately. See the
[repository README](https://github.com/adeptusxi/Tobii-Unity-bridge#quick-start).

## Quick start

Everything is under `GameObject ▸ Talon Gaze Bridge`:

1. **Talon Gaze Rig** creates a `UdpGazeProvider` and a `GazeDebugCursor`. With
   Talon running, enter play mode and the crosshair follows your gaze. Without
   a tracker, use **Mouse Gaze Rig** instead.
2. **Gaze Pointer** adds dwell activation for UI. The scene also needs an
   `EventSystem`.
3. Add a `GazeInteractable` to each UI element that should react to gaze, and
   wire its `Activated` event.
4. **Gaze World Pointer** adds the 3D gaze position.
5. **Gaze Shader Globals** and **Gaze Spectator View** are optional; see
   [Shaders](#shaders) and [Spectator view](#spectator-view).

## Layout

| Assembly | Contents |
| --- | --- |
| `TalonBridge.Gaze` | Runtime: samples, coordinates, providers, filtering, interaction, world gaze, cursors, shader globals, spectator view |
| `TalonBridge.Gaze.InputSystem` | Optional. Pointer position via com.unity.inputsystem; excluded when the package is absent |
| `TalonBridge.Gaze.Editor` | Editor tooling |

`ShaderLibrary/Gaze.hlsl` is the shader include. Samples are in `Samples~/` and
are not compiled until imported.

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

## Providers

Providers register with `GazeService` when enabled; the highest `Priority` wins.
`IsValid` goes false when no valid sample has arrived within `Validity Timeout`
(0.1 s by default), while `NormalizedPosition` keeps the last valid value.

`LastSample` is the sample as published; `RawSample` is the same sample before
filtering.

### UdpGazeProvider

Receives the Talon bridge's stream on `127.0.0.1` at `Port` (5005 by default,
matching the Talon script). Only the newest sample is published each frame;
superseded samples are discarded rather than queued.

`Source`, under `Mapper`, selects which coordinates from a packet to use:

| Source | Behaviour |
| --- | --- |
| `Auto` | The Game view's rect in the editor; window-relative in a player, else screen-relative |
| `Window` | Window-relative only |
| `Screen` | Screen-relative, for a build filling the main screen |
| `GameView` | The same Game view mapping, requested explicitly |

`Require Inside Surface`, also under `Mapper`, treats gaze outside the surface
as invalid.
`Retry Interval` is how long the receiver waits before reopening the socket
after a failure.

`Diagnostics` exposes packets received, parse errors, invalid packets, packet
rate, time since the last packet, transport latency and the last socket error.

### MouseGazeProvider

Simulates gaze from the mouse pointer, for building and testing gaze systems
without a tracker or Talon running. `Jitter Pixels` adds drift to approximate
tracker noise, `Simulate Tracking Loss` reports invalid gaze, and
`Invalid Outside Window` reports invalid gaze when the pointer leaves the game
window.

Requires `com.unity.inputsystem`, or a project using the legacy input backend.

`GameObject ▸ Talon Gaze Bridge ▸ Mouse Gaze Rig` creates one. Give it a higher
`Priority` than the Talon rig to take over while both are in the scene.

## Filtering

Optional smoothing, off by default. Add a filter component to the provider's
GameObject; disable the component to bypass it.

| Component | Behaviour |
| --- | --- |
| `OneEuroGazeFilter` | Smooths a resting gaze while adding little lag to fast movement. `Min Cutoff`, `Beta`, `Derivative Cutoff`. |
| `MovingAverageGazeFilter` | Averages the last `Window Size` samples. |

Filters run on valid samples only. History is kept across a blink or a dropped
packet and discarded once gaze has been invalid for longer than
`Validity Timeout`, so gaze does not slide back from where it was lost.
`RawSample` on the provider stays unfiltered.

A filter component added while playing is picked up within a second. Implement
`IGazeFilter` for anything else; assigning `Filter` on the provider from code
replaces the component lookup.

## Interaction

| Type | Purpose |
| --- | --- |
| `GazePointer` | Reads gaze, resolves targets, drives dwell. The component you add. |
| `GazeUIInteractor` | Hit-tests gaze against uGUI. |
| `GazeInteractable` | Opts a UI element in, and raises its gaze events. |
| `GazeDwellTracker` | Dwell timing and activation, owned by the pointer. |
| `IGazeFeedback` | Presentation hook. `GazeTintFeedback` is a minimal example; the UI Interaction Feedback sample has three more. |
| `IGazeHitSource` | The hit test behind the pointer, so it need not be uGUI. |

`GameObject ▸ Talon Gaze Bridge ▸ Gaze Pointer` creates a ready pointer. The
scene also needs an `EventSystem`.

A UI element takes part by having a `GazeInteractable`; everything else is
untouched.

```csharp
interactable.Activated.AddListener(() => Fire());
interactable.DwellProgressed.AddListener(p => bar.fillAmount = p);
```

Events: `GazeEntered`, `GazeExited`, `DwellProgressed` (0 to 1) and `Activated`,
all also exposed in the inspector.

Gaze does not drive Unity's EventSystem pointer, so a `Button`'s own `onClick`
is not fired by gaze.

### Settings

On `GazePointer`:

| Setting | Effect |
| --- | --- |
| `Dwell Duration` | Gaze time needed to activate a target. |
| `Cooldown` | Time before the same target may fire again. |
| `Dwell Decay Rate` | Progress shed per second off-target. 0 discards it at once. |
| `Region Radius` | Search radius around the gaze point, in pixels. 0 uses the point alone. |
| `Activate All In Radius` | Off: the gaze point wins and the radius is only a fallback. On: everything in the radius dwells and activates together. |

`GazeInteractable` can override the duration and cooldown per element, and
`Repeat While Held` makes gaze held on a target fire repeatedly.

Dwell progress accumulates only while gaze is valid, so tracking loss mid-dwell
stops progress.

On `GazeUIInteractor`, `Region Samples` (8 by default) and `Inner Ring` set how
many raycasts a non-zero `Region Radius` costs per frame: one for the gaze
point, plus `Region Samples` per ring.

## 3D Interaction

`GazeWorldPointer` casts a ray from the camera through the gaze point and
reports where it lands.

| Member | Meaning |
| --- | --- |
| `GazePositionWorld` | World position under the gaze |
| `HasWorldHit` | A collider was hit |
| `HasWorldPosition` | A usable position exists, from a hit or the fallback plane |
| `HitCollider`, `HitNormal`, `HitDistance` | Details of the hit |
| `HitColliderChanged` | Raised when the object under the gaze changes |

Settings: `Source Camera` (empty uses `Camera.main`), `Layer Mask`,
`Max Distance`, `Trigger Interaction`, and a fallback point `Fallback Distance`
along the ray, used when it hits nothing. Distances are measured from the
camera's near plane.

With `Source Camera` empty, `Camera.main` is looked up again whenever the camera
in use is destroyed or disabled, so camera switches are followed.

`GameObject ▸ Talon Gaze Bridge ▸ Gaze World Pointer` creates one.

## Shaders

`GazeShaderGlobals` publishes gaze as global shader properties, so any shader
can react to it. `GameObject ▸ Talon Gaze Bridge ▸ Gaze Shader Globals` creates
one; a scene needs only one.

| Setting | Effect |
| --- | --- |
| `Provider` | Provider to read. Empty follows the active provider. |
| `World Pointer` | Optional `GazeWorldPointer`, for shaders that react in world space. |
| `Radius` | Radius around the gaze point that shaders react in, in pixels. |
| `Use Region Radius`, `Pointer` | Use that `GazePointer`'s `Region Radius` instead, while it is above 0. |
| `Fade Time` | Seconds for effects to fade in and out as gaze becomes valid or is lost. |

| Property | Meaning |
| --- | --- |
| `_GazeScreenUV` | xy: gaze in screen UV, 0..1, with 0,0 at the **bottom-left** |
| `_GazeScreenPos` | xy: gaze in screen pixels |
| `_GazeScreenSize` | xy: screen size in pixels |
| `_GazeRadius` | Radius in pixels |
| `_GazeWeight` | 1 while gaze is valid, fading to 0 when it is lost |
| `_GazeWorldPos` | xyz: 3D gaze position. w: 1 while there is one, fading to 0 |

Positions keep their last valid value when tracking is lost; multiply an effect
by `_GazeWeight` or `_GazeWorldPos.w` so it fades out.

Include `Packages/com.talonbridge.gaze/ShaderLibrary/Gaze.hlsl` to declare the
properties and get `GazeScreenDistance`, `GazeScreenMask` and `GazeWorldMask`.
The include has no render pipeline dependency.

The Gaze Shaders sample has one URP example of each kind: a fullscreen pass, a
3D object material and a UI material. Its README covers setup and Shader Graph.

## Spectator view

`GazeSpectatorView` shows a cursor at the gaze point and an optional trail. It is a minimal visual; use `GazeDebugCursor` for diagnostics.

`GameObject ▸ Talon Gaze Bridge ▸ Gaze Spectator View` creates one on its own
overlay Canvas, above the rest of the UI. It never takes raycasts, so gaze and
mouse interaction with the UI beneath are unaffected.

| Setting | Effect |
| --- | --- |
| `Provider` | Provider to show. Empty follows the active provider. |
| `Show Raw Gaze` | Show gaze before any filter on the provider. |
| `Visible` | Draw the overlay. Also settable from code. |
| `Hide When Invalid` | Hide the cursor while gaze is invalid, rather than leaving it at the last position. |
| `Show Dot`, `Dot Radius` | A filled dot at the gaze point. |
| `Show Cross`, `Cross Size` | A cross at the gaze point. |
| `Show Radius Ring`, `Ring Radius`, `Pointer` | A circle outline. Its radius is the `GazePointer`'s `Region Radius` while that is above 0, else `Ring Radius`. An empty `Pointer` uses one found in the scene. |
| `Cursor Color`, `Line Thickness` | Colour of the cursor; thickness of the cross and ring. |
| `Show Outline`, `Outline Color`, `Outline Width` | Outline the cursor and trail so they read on any background. |
| `Show Trail`, `Trail Color`, `Trail Width` | A line along where gaze has been. |
| `Max Segments` | Most trail segments shown. |
| `Max Age` | Seconds before a trail point disappears. 0 keeps points until `Max Segments` pushes them out. |
| `Min Spacing` | Distance gaze must move, in pixels, before a trail point is added. |
| `Fade Tail` | Fade the trail toward its oldest end. |

Sizes are in screen pixels. Dot, cross and ring can be combined. The trail is
broken when tracking is lost, so a blink is not drawn as a line.

The overlay is part of the Game view. When recording with Unity Recorder,
capture **Game View**; a targeted camera capture does not include it.

## Cursors

| Component | Use |
| --- | --- |
| `GazeDebugCursor` | IMGUI crosshair at the gaze position. No scene setup; for diagnostics. |
| `GazeCanvasCursor` | Moves a UI element to the gaze position. `Smoothing` is cosmetic and does not affect the gaze other systems read. |

Both only show where gaze is. Coordinates, validity, targets and diagnostics
live in the Talon Gaze Monitor.

## Talon Gaze Monitor

`Window ▸ Talon Gaze Monitor` shows validity, coordinates, the current target
and its dwell progress, the 3D position and hit, and the socket status, packet
rate, latency, counters and resolved surface rect.

`GazeInteractable` also has a live dwell readout in its own inspector, and warns
when an element has nothing wired to it or the scene has no `GazePointer`.

## Samples

Import from the Samples tab in Package Manager.

| Sample | Contents |
| --- | --- |
| UI Interaction Feedback | `RadialFillGazeFeedback`, `ScaleGazeFeedback`, `OutlineGazeFeedback` |
| 3D Interaction | `GazeWorldMarker`, which follows the estimated 3D gaze position |
| Gaze Shaders | URP shaders that react to gaze: `GazeFocusColor` (fullscreen), `GazeGlow` (3D object), `GazeUISpotlight` (UI) |

## Not supported

- **Multiple monitors and secondary displays.** Gaze is normalized against the
  main screen.
- **Focal depth.** The tracker reports a direction, not a depth, so
  `GazeWorldPointer` returns the first surface along the gaze ray. Objects on
  the same line of sight cannot be told apart.
- **Packet history and replay.** Only the newest sample is kept.
- **Gaze-driven OS cursor.** Gaze does not move the system pointer.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| No gaze at all | Talon not running, or the bridge script not started. The monitor's `Socket` row shows whether the port is bound, and `Rate` whether packets arrive. |
| `LastError` reports the port is in use | Another process holds the port. Only one receiver can bind it, so stop `tools/gaze_monitor.py` before entering play mode. |
| Gaze lands offset in the editor | `Source` under `Mapper` is not `Auto` or `GameView`. Talon sees the whole editor window, not the Game view. |
| Packets arrive but gaze is invalid | Gaze is outside the game surface and `Require Inside Surface` is on, or the tracker has lost the eyes. |
| Gaze moves but no UI reacts | The scene has no `EventSystem`, the UI elements have no `GazeInteractable`, or the pointer has no `GazeUIInteractor`. The console warns about the last one. |
| UI ignores the mouse as well | With the Input System backend the `EventSystem` needs `InputSystemUIInputModule`, not `StandaloneInputModule`. |
| 3D position never updates | No camera is tagged `MainCamera` and `Source Camera` is empty, or `Layer Mask` excludes the geometry. |
| `ParseErrors` climbing | Something other than the bridge is sending to the port. |
| Shader effects do not react | No enabled `GazeShaderGlobals` in the scene. For world-space effects, its `World Pointer` is empty or the gaze ray hits no collider. |
| Spectator cursor is hard to see | White on a light background. Enable `Show Outline` or change `Cursor Color`. |
| Spectator view missing from a recording | The recorder is capturing a targeted camera. Capture the Game view. |
