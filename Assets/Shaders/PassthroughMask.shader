// Covers the whole view (both eyes) after all VR content, except where the MR portal marked stencil bit 1.
// Two materials use it:
//   Passthrough Mask: ColorMask RGBA, colour (0,0,0,0) → everything outside the portal becomes passthrough.
//   Passthrough Fill: ColorMask A,    colour (0,0,0,1) → everything outside the portal becomes opaque (the empty
//                     VR background turns black instead of showing passthrough); colours are kept.
// The mesh's own shape is ignored: every vertex is mapped straight to the screen, so any quad works as long as
// its renderer is in view (the Passthrough Mask component keeps it in front of the camera).
Shader "TRINL/Passthrough Mask"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+999" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ColorMask [_ColorMask]
        ZWrite Off
        ZTest Always
        Cull Off

        Stencil
        {
            Ref 1
            ReadMask 1
            Comp NotEqual
        }

        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Unity quad corners are ±0.5; scale them to cover the screen generously.
                float2 xy = sign(input.positionOS.xy) * 1.5;
                output.positionCS = float4(xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
