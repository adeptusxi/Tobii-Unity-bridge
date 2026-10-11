# Gaze shaders sample

Three URP shaders that react to gaze, one for each place a shader can run. Each
comes with a material.

| Material | Type | Effect |
| --- | --- | --- |
| `GazeFocusColor` | Fullscreen | The image is greyscale, with colour in a soft circle around the gaze point. |
| `GazeGlow` | 3D object | A lit surface that glows around the 3D gaze position. |
| `GazeUISpotlight` | UI / sprite | The image is dimmed, and brightens under the gaze point. |

All three need a `GazeShaderGlobals` in the scene
(`GameObject ▸ Talon Gaze Bridge ▸ Gaze Shader Globals`), which publishes gaze to
shaders. Its `Radius` sets the size of the screen-space effects.

## Setup

### Fullscreen

1. Select the URP renderer asset in use (the Renderer List of the active
   Universal Render Pipeline asset).
2. `Add Renderer Feature ▸ Full Screen Pass Renderer Feature`.
3. Set `Pass Material` to `GazeFocusColor`, leave `Fetch Color Buffer` on, and
   leave `Injection Point` at `After Rendering Post Processing`.

The pass also runs in the Scene view, where the gaze point means nothing.
Disable the feature to edit without it.

### 3D object

1. Add a `GazeWorldPointer` (`GameObject ▸ Talon Gaze Bridge ▸ Gaze World
   Pointer`) and assign it to `World Pointer` on the `GazeShaderGlobals`.
2. Assign `GazeGlow` to objects that have colliders, so the gaze ray has
   something to land on.

`Glow Radius` on the material is in world units.

### UI / sprite

Assign `GazeUISpotlight` to the `Material` of an `Image` or `RawImage`, or to a
`SpriteRenderer`.

## Shader properties

`GazeShaderGlobals` sets these globally. Include
`Packages/com.talonbridge.gaze/ShaderLibrary/Gaze.hlsl` to declare them.

| Property | Meaning |
| --- | --- |
| `_GazeScreenUV` | xy: gaze in screen UV, 0..1, with 0,0 at the bottom-left |
| `_GazeScreenPos` | xy: gaze in screen pixels |
| `_GazeScreenSize` | xy: screen size in pixels |
| `_GazeRadius` | Radius in pixels |
| `_GazeWeight` | 1 while gaze is valid, fading to 0 when it is lost |
| `_GazeWorldPos` | xyz: 3D gaze position. w: 1 while there is one, fading to 0 |

The positions keep their last valid value when tracking is lost. Multiply an
effect by `_GazeWeight` (or `_GazeWorldPos.w`) so it fades out instead.

Helpers in `Gaze.hlsl`:

| Function | Returns |
| --- | --- |
| `GazeScreenDistance(screenUV)` | Distance to the gaze point in pixels |
| `GazeScreenMask(screenUV, softness)` | 1 inside `_GazeRadius`, 0 outside or when gaze is lost |
| `GazeWorldMask(positionWS, radiusWS, softness)` | 1 within `radiusWS` of the 3D gaze position, 0 outside or when there is none |

`softness` is the fraction of the radius the falloff covers, from 0 (hard edge)
to 1.

## Shader Graph

The same properties are available in Shader Graph. Add a property to the
Blackboard, set its `Reference` to the name above (for example
`_GazeScreenUV`, type Vector4), and untick `Exposed` so it reads the global
value. Compare it with the Screen Position node for screen-space effects, or
the Position node (World) for `_GazeWorldPos`.
