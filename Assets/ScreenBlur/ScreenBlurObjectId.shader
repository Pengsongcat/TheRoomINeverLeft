Shader "Hidden/TRINL/ScreenBlurObjectId"
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
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D_X_FLOAT(_ScreenBlurLayerDepth);
            CBUFFER_START(UnityPerMaterial)
            float4 _MaskST;
            float _MaskCutoff;
            CBUFFER_END
            // Bound per draw, so one cached mask material can serve any group.
            float _ScreenBlurMaskObjectId;

            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 screenPosition:TEXCOORD1; float eyeDepth:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            V Vert(A input)
            {
                V o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.screenPosition = ComputeScreenPos(o.positionCS);
                o.eyeDepth = -TransformWorldToView(world).z;
                o.uv = input.uv * _MaskST.xy + _MaskST.zw;
                return o;
            }

            float Frag(V input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float raw = SAMPLE_TEXTURE2D_X_LOD(_ScreenBlurLayerDepth, sampler_PointClamp, input.screenPosition.xy / input.screenPosition.w, 0).r;
                float depth = LinearEyeDepth(raw, _ZBufferParams);
                if (unity_OrthoParams.w > .5)
                {
                    #if UNITY_REVERSED_Z
                    raw = 1 - raw;
                    #endif
                    depth = lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                // Only the surface that actually won the layer depth test owns the pixel.
                // Compare in metres; the capture and ownership shaders may round vertices differently.
                clip(max(.001, depth * .00001) - abs(input.eyeDepth - depth));
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - max(.0001, _MaskCutoff));
                return _ScreenBlurMaskObjectId;
            }
            ENDHLSL
        }
    }
}
