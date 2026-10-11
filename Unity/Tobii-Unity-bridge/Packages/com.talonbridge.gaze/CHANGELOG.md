# Changelog

## [0.1.0] - Unreleased

### Added
- `IGazeProvider`, `GazeSample` and `GazeService` as the gaze access layer.
- `GazeProviderBase` with provider registration, last-valid-position retention
  and timeout-based invalidation.
- `GazeCoordinates` conversions between normalized gaze and Unity screen pixels.
- `UdpGazeProvider` receiving the Talon bridge stream on a background thread,
  with retry, clean shutdown and newest-sample-wins publishing.
- `GazePacketParser`, an allocation-free decoder for the bridge's wire format.
- `GazeCoordinateMapper` with automatic, window, screen and Game view coordinate
  sources; the Game view rect is resolved from the editor.
- `GazeDiagnostics` counters: packet rate, latency, parse errors, socket status.
- `GazePointer` resolving the interactables under the gaze each frame and
  driving dwell, with configurable duration, cooldown, progress decay and
  region radius.
- `GazeInteractable` opting a UI element into gaze, with enter, exit, dwell
  progress and activation events. 
- `GazeUIInteractor` hit-testing gaze against uGUI through the EventSystem's
  raycasters, with ring sampling for the region radius.
- `IGazeHitSource`, the seam between the pointer and the hit test.
- `GazeDwellTracker` holding per-target dwell timing (several targets can
  dwell at once). Progress accumulates only while gaze is valid.
- `IGazeFeedback` for visual feedback on gazed-at objects. Example implementation in `GazeTintFeedback`
- `GazeDebugCursor`, an IMGUI marker at the gaze position.
- `GazeWorldPointer` estimating the 3D gaze position by raycasting from the
  camera, with layer mask, max distance and a fallback plane. 
- `GazeCanvasCursor`, a uGUI cursor for when the gaze marker is part of the
  game's presentation. 
- Samples: UI Interaction Feedback (`RadialFillGazeFeedback`,
  `ScaleGazeFeedback`, `OutlineGazeFeedback`) and 3D Interaction (`GazeWorldMarker`).
- `GazeMonitorWindow` (`Window ▸ Talon Gaze Monitor`), a live monitor for gaze
  state, the current target, the 3D position and transport diagnostics.
- `GazeInteractableInspector` showing live dwell progress and warning about
  interactables that are wired to nothing.
- `IGazeFilter` with `OneEuroGazeFilter` and `MovingAverageGazeFilter`, optional
  gaze smoothing that is off unless a filter component is added to the provider.
  Filter history survives a blink and is reset once gaze has been invalid for
  longer than `Validity Timeout`.
- `GazeProviderBase.RawSample`, the newest sample before filtering.
- `MouseGazeProvider` documented as a supported provider for building gaze
  systems without a tracker.
- `GazeShaderGlobals`, which publishes gaze as global shader properties, and
  `ShaderLibrary/Gaze.hlsl` with helpers for reading them.
- Gaze Shaders sample: URP fullscreen, 3D object and UI shaders that react to
  gaze.
- `GazeSpectatorView`, a minimal cursor and trail overlay for recorded
  gameplay, and `GazeTrail`, the point history behind it.
- `Gaze Shader Globals` and `Gaze Spectator View` entries in the
  `GameObject ▸ Talon Gaze Bridge` menu.

### Fixed
- `GazePointer`, `GazeWorldPointer` and `GazeCanvasCursor` repeated a component
  or `Camera.main` lookup every frame when the thing they needed was missing.
  Each now caches the result, retries once a second while it is missing, and
  warns once.
- `GazeWorldPointer` and `GazeCanvasCursor` kept using a main camera that had
  been destroyed or disabled. They now look `Camera.main` up again.
- `GazeUIInteractor` looked up each candidate's `Canvas` on every hit test;
  `GazeInteractable` now caches it.
- `UdpGazeProvider` allocated a string on every `ProviderName` read and on every
  malformed datagram.

