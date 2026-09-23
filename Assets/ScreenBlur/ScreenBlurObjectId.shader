Shader "Hidden/TRINL/ScreenBlurObjectId"
{
    Properties
    {
        _NoiseTexture("Noise",2D)="gray"{}
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
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D_X_FLOAT(_ScreenBlurLayerDepth);
            TEXTURE2D(_NoiseTexture);
            CBUFFER_START(UnityPerMaterial)
            float4 _NoiseBounds, _NoiseSettings, _NoiseVelocity;
            float4 _MaskST;
            float _MaskCutoff;
            CBUFFER_END
            // Bound per draw, so one cached mask material can serve any group.
            float _ScreenBlurMaskObjectId;

            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 screenPosition:TEXCOORD1; float eyeDepth:TEXCOORD2; float3 noisePosition:TEXCOORD3; float3 normalOS:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };

            V Vert(A input)
            {
                V o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.screenPosition = ComputeScreenPos(o.positionCS);
                o.eyeDepth = -TransformWorldToView(world).z;
                o.uv = input.uv * _MaskST.xy + _MaskST.zw;
                o.noisePosition = (input.positionOS - _NoiseBounds.xyz) / max(_NoiseBounds.w, 0.000001);
                o.normalOS = input.normalOS;
                return o;
            }

            float NoiseHash(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x+p.y)*p.z); }
            float NoiseValue(float3 p)
            {
                float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(NoiseHash(a),NoiseHash(a+float3(1,0,0)),f.x),
                    lerp(NoiseHash(a+float3(0,1,0)),NoiseHash(a+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(NoiseHash(a+float3(0,0,1)),NoiseHash(a+float3(1,0,1)),f.x),
                    lerp(NoiseHash(a+float3(0,1,1)),NoiseHash(a+1),f.x),f.y),f.z);
            }
            struct MaskOutput { float id:SV_Target0; float noise:SV_Target1; };
            MaskOutput Frag(V input)
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
                float3 p = (input.noisePosition - _NoiseVelocity.xyz * (_Time.y * _NoiseSettings.w)) / max(_NoiseSettings.x, 0.01);
                float n;
                if (_NoiseSettings.z > 0.5)
                {
                    float3 weights=abs(input.normalOS); weights /= max(dot(weights,1),0.0001);
                    n = SAMPLE_TEXTURE2D(_NoiseTexture,sampler_LinearRepeat,p.yz).r*weights.x
                      + SAMPLE_TEXTURE2D(_NoiseTexture,sampler_LinearRepeat,p.xz).r*weights.y
                      + SAMPLE_TEXTURE2D(_NoiseTexture,sampler_LinearRepeat,p.xy).r*weights.z;
                }
                else n = NoiseValue(p);
                MaskOutput output;
                output.id = _ScreenBlurMaskObjectId;
                output.noise = max(0, 1 + (n*2-1)*_NoiseSettings.y);
                return output;
            }
            ENDHLSL
        }
    }
}
