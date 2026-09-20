// Distance blur (DistanceBlurFeature). Dual-filtering chain plus a depth-driven composite: each pixel blends
// the sharp image and blur levels 1..N (radius roughly doubling per level) by its distance.
Shader "Hidden/TRINL/Distance Blur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // xy = half a destination texel in UV, zw = destination size in pixels
        float4 _DistanceBlurTexel;
        // x = near, y = far (metres, room-scaled), z = min blur, w = blur radius at full blur (pixels)
        float4 _DistanceBlurParams;
        float  _DistanceBlurLevelCount;

        TEXTURE2D_X(_DistanceBlurLevel1);
        TEXTURE2D_X(_DistanceBlurLevel2);
        TEXTURE2D_X(_DistanceBlurLevel3);
        TEXTURE2D_X(_DistanceBlurLevel4);
        TEXTURE2D_X(_DistanceBlurLevel5);
        TEXTURE2D_X(_DistanceBlurLevel6);
        TEXTURE2D_X(_DistanceBlurLevel7);
        TEXTURE2D_X(_DistanceBlurLevel8);

        float4 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0);
        }

        // Level 0 = the sharp image (the blit source in the composite).
        float3 SampleLevel(int level, float2 uv)
        {
            float4 c;
            [branch] switch (level)
            {
                case 0:  c = SampleSource(uv); break;
                case 1:  c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel1, sampler_LinearClamp, uv, 0); break;
                case 2:  c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel2, sampler_LinearClamp, uv, 0); break;
                case 3:  c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel3, sampler_LinearClamp, uv, 0); break;
                case 4:  c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel4, sampler_LinearClamp, uv, 0); break;
                case 5:  c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel5, sampler_LinearClamp, uv, 0); break;
                case 6:  c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel6, sampler_LinearClamp, uv, 0); break;
                case 7:  c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel7, sampler_LinearClamp, uv, 0); break;
                default: c = SAMPLE_TEXTURE2D_X_LOD(_DistanceBlurLevel8, sampler_LinearClamp, uv, 0); break;
            }
            return c.rgb;
        }

        float4 FragCopy(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SampleSource(input.texcoord);
        }

        float4 FragDownsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 h = _DistanceBlurTexel.xy;
            float4 sum = SampleSource(uv) * 4.0;
            sum += SampleSource(uv - h);
            sum += SampleSource(uv + h);
            sum += SampleSource(uv + float2(h.x, -h.y));
            sum += SampleSource(uv - float2(h.x, -h.y));
            return sum * 0.125;
        }

        float4 FragUpsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 h = _DistanceBlurTexel.xy;
            float4 sum = SampleSource(uv + float2(-h.x * 2.0, 0.0));
            sum += SampleSource(uv + float2(-h.x, h.y)) * 2.0;
            sum += SampleSource(uv + float2(0.0, h.y * 2.0));
            sum += SampleSource(uv + float2(h.x, h.y)) * 2.0;
            sum += SampleSource(uv + float2(h.x * 2.0, 0.0));
            sum += SampleSource(uv + float2(h.x, -h.y)) * 2.0;
            sum += SampleSource(uv + float2(0.0, -h.y * 2.0));
            sum += SampleSource(uv + float2(-h.x, -h.y)) * 2.0;
            return sum * (1.0 / 12.0);
        }

        float4 FragComposite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float4 original = SampleSource(uv);

            // Same curve as SoundLightMath.Blur: min blur up to near, smoothstep to 1 at far.
            float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            float t = saturate((depth - _DistanceBlurParams.x) / (_DistanceBlurParams.y - _DistanceBlurParams.x));
            float blur = lerp(saturate(_DistanceBlurParams.z), 1.0, t * t * (3.0 - 2.0 * t));

            // Radius in pixels → level (radius roughly doubles per level).
            float level = min(log2(1.0 + blur * _DistanceBlurParams.w), _DistanceBlurLevelCount);
            int   i = (int)floor(level);
            float f = level - i;
            float3 a = SampleLevel(i, uv);
            float3 b = SampleLevel(min(i + 1, (int)_DistanceBlurLevelCount), uv);
            return float4(lerp(a, b, f), original.a);
        }
        ENDHLSL

        Pass
        {
            Name "Copy"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopy
            ENDHLSL
        }
        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDownsample
            ENDHLSL
        }
        Pass
        {
            Name "Upsample"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragUpsample
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
}
