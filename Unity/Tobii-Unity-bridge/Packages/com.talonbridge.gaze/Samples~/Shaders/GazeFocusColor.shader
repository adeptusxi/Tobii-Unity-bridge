// Fullscreen example: the image is greyscale except in a soft circle around
// the gaze point.
//
// Use it as the Pass Material of URP's Full Screen Pass Renderer Feature, with
// Fetch Color Buffer on.
Shader "Talon Gaze Bridge/Fullscreen/Gaze Focus Color"
{
    Properties
    {
        _Softness ("Softness", Range(0, 1)) = 0.5
        _Desaturation ("Desaturation", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "GazeFocusColor"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.talonbridge.gaze/ShaderLibrary/Gaze.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Softness;
                float _Desaturation;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                half grey = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                half focus = GazeScreenMask(uv, _Softness);

                color.rgb = lerp(color.rgb, grey.xxx, _Desaturation * (1.0 - focus));
                return color;
            }
            ENDHLSL
        }
    }
}
