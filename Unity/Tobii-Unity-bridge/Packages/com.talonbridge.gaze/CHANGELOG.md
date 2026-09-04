# Changelog

## [0.1.0] - Unreleased

### Added
- `IGazeProvider`, `GazeSample` and `GazeService` as the gaze access layer.
- `GazeProviderBase` with provider registration, last-valid-position retention
  and timeout-based invalidation.
- `GazeCoordinates` conversions between normalized gaze and Unity screen pixels.
- `GazeDebugCursor` IMGUI marker and coordinate readout.
