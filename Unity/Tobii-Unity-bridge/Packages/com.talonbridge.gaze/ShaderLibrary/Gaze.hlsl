#ifndef TALONBRIDGE_GAZE_INCLUDED
#define TALONBRIDGE_GAZE_INCLUDED

// Gaze as published by the GazeShaderGlobals component. Plain HLSL with no
// render pipeline dependency: include it from URP, built-in or UI shaders.
//
// Screen UV has 0,0 at the bottom-left, like Unity's screen-space UVs.
// Distances on screen are in pixels of _GazeScreenSize, so a radius does not
// change with render scale.

float4 _GazeScreenUV;    // xy: gaze, 0..1
float4 _GazeScreenPos;   // xy: gaze, pixels
float4 _GazeScreenSize;  // xy: screen size, pixels
float _GazeRadius;       // pixels
float _GazeWeight;       // 0..1, 0 when gaze is lost
float4 _GazeWorldPos;    // xyz: world position, w: 0..1 weight

// 1 at the centre, 0 at radius. softness is the fraction of the radius the
// falloff covers: 0 is a hard edge, 1 fades all the way from the centre.
float GazeFalloff(float dist, float radius, float softness)
{
    float inner = radius * (1.0 - saturate(softness));
    return 1.0 - smoothstep(inner, max(radius, inner + 0.0001), dist);
}

// Distance from a screen UV to the gaze point, in pixels.
float GazeScreenDistance(float2 screenUV)
{
    return length((screenUV - _GazeScreenUV.xy) * _GazeScreenSize.xy);
}

// 1 inside _GazeRadius of the gaze point, 0 outside, 0 when gaze is lost.
float GazeScreenMask(float2 screenUV, float softness)
{
    return GazeFalloff(GazeScreenDistance(screenUV), _GazeRadius, softness) * _GazeWeight;
}

// 1 within radiusWS world units of the 3D gaze position, 0 outside, 0 when
// there is no 3D position. Needs a GazeWorldPointer assigned to the component.
float GazeWorldMask(float3 positionWS, float radiusWS, float softness)
{
    float dist = distance(positionWS, _GazeWorldPos.xyz);
    return GazeFalloff(dist, radiusWS, softness) * _GazeWorldPos.w;
}

#endif
