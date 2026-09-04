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
- `GazeDebugCursor` IMGUI marker and coordinate readout.
