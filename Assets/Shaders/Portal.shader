// MR portal parts. Every visible pixel marks stencil bit 1, so the Passthrough Mask leaves it alone:
//   Opening: ColorMask A, colour (0,0,0,1), ZWrite off, drawn after opaques. It writes only alpha 1, so the VR
//            world behind it stays as drawn and its empty background is black instead of passthrough.
//            Culled from the back, so the portal only shows the VR world from its front side.
//   Frame:   ColorMask RGBA, unlit colour, a normal opaque.
// Everything above world height _CutY is cut away, so moving _CutY down dissolves the portal from the top.
Shader "TRINL/Portal"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _CutY ("Cut Height (world Y)", Float) = 10000
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 15
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        ColorMask [_ColorMask]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Stencil
        {
            Ref 1
            WriteMask 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            Name "Portal"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _CutY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  worldY     : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.worldY = positionWS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_CutY - input.worldY);
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
