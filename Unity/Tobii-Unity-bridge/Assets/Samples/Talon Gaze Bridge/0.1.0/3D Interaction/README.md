# 3D interaction sample

`GazeWorldMarker` moves an object to the position reported by `GazeWorldPointer`. Provides a quick way to check the 3D estimate against what you are actually looking at.

## Setup

1. `GameObject ▸ Talon Gaze Bridge ▸ Talon Gaze Rig` for the gaze source.
2. Add a `GazeWorldPointer` to any object and leave `Source Camera` empty to use
   `Camera.main`.
3. Make a small marker — a sphere at scale 0.1 with its collider removed, so it
   cannot block its own gaze ray — and add `GazeWorldMarker` to it.
4. Put some colliders in the scene to look at.

Set `Layer Mask` on the pointer to exclude anything the gaze ray should pass
through.

## Limitations
- The tracker reports a direction, not a depth. This estimate does not incorporate focal depth.
- When the ray hits nothing, `GazeWorldPointer` falls back to a point
`Fallback Distance` along the gaze ray, so `GazePositionWorld` stays usable
against the sky or an empty scene. `HasWorldHit` distinguishes the two;
`HasWorldPosition` is true for either.
