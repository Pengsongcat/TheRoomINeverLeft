// Soft round additive glow for SoundLight. Colour (and brightness) comes from _BaseColor per renderer.
// Alpha is added too, so the glow stays visible over passthrough while it fades out.
Shader "TRINL/Sound Glow"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.82, 0.62, 1)
        _Falloff ("Falloff", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Glow"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = length(input.uv * 2.0 - 1.0);       // 0 at centre, 1 at the edge
                half  a = pow(saturate(1.0 - d), _Falloff);   // soft round falloff
                return half4(_BaseColor.rgb * a, _BaseColor.a * a);
            }
            ENDHLSL
        }
    }
}
