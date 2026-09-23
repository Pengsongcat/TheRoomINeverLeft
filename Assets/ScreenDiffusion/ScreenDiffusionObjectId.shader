Shader "Hidden/TRINL/ScreenDiffusionObjectId"
{
    Properties
    {
        _BaseMap("Texture",2D)="white"{}
        _MaskST("UV",Vector)=(1,1,0,0)
        _MaskCutoff("Cutoff",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _MaskST;
            float _MaskCutoff;
            CBUFFER_END
            // Bound per draw, so one cached mask material can serve any group.
            float _ScreenDiffusionMaskObjectId;

            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            V Vert(A input)
            {
                V o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = input.uv * _MaskST.xy + _MaskST.zw;
                return o;
            }

            float Frag(V input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Reuse the capture depth attachment: hardware raster depth matches the
                // actual visible surface without far-plane linearization roundoff.
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - max(.0001, _MaskCutoff));
                return _ScreenDiffusionMaskObjectId;
            }
            ENDHLSL
        }
    }
}
