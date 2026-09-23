Shader "Hidden/TRINL/ScreenDiffusion"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_ScreenDiffusionOriginal);
        TEXTURE2D_X_FLOAT(_ScreenDiffusionObjectIds);
        float4 _ScreenDiffusionGroup;
        TEXTURE2D_X_FLOAT(_ScreenDiffusionLayerDepth);
        TEXTURE2D_X_FLOAT(_ScreenDiffusionOccluderDepth);
        float4 _ScreenDiffusionOcclusion;

        float OcclusionEyeDepth(float raw)
        {
            if (unity_OrthoParams.w > 0.5)
            {
                #if UNITY_REVERSED_Z
                raw = 1.0 - raw;
                #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
            }
            return LinearEyeDepth(raw, _ZBufferParams);
        }

        float LayerVisibility(float2 uv)
        {
            if (_ScreenDiffusionOcclusion.x < 0.5) return 1;
            float groundRaw = SAMPLE_TEXTURE2D_X_LOD(_ScreenDiffusionOccluderDepth, sampler_PointClamp, uv, 0).r;
            #if UNITY_REVERSED_Z
            if (groundRaw <= 0.000001) return 1;
            #else
            if (groundRaw >= 0.999999) return 1;
            #endif
            float layerRaw = SAMPLE_TEXTURE2D_X_LOD(_ScreenDiffusionLayerDepth, sampler_PointClamp, uv, 0).r;
            return OcclusionEyeDepth(layerRaw) <= OcclusionEyeDepth(groundRaw) + _ScreenDiffusionOcclusion.y ? 1 : 0;
        }
        // xy = half a destination texel in UV, zw = destination size in pixels
        float4 _ScreenDiffusionTexel;
        // x = spread, y = strength, z = blend mode
        float4 _ScreenDiffusionParams;

        // Bilinear taps are the whole point of dual filtering, so never point sample here.
        float4 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0);
        }

        float4 FragCopy(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SampleSource(input.texcoord);
        }

        // Dual filtering (Kawase). Each level costs a fixed number of taps, so the
        // reachable radius grows with the level count instead of with the tap count.
        float4 FragDownsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 h = _ScreenDiffusionTexel.xy * _ScreenDiffusionParams.x;
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
            float2 h = _ScreenDiffusionTexel.xy * _ScreenDiffusionParams.x;
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
            float4 original = SAMPLE_TEXTURE2D_X_LOD(_ScreenDiffusionOriginal, sampler_LinearClamp, uv, 0);
            // The last upsample to full resolution is the bilinear fetch itself.
            float3 blurred = SampleSource(uv).rgb;
            float strength = saturate(_ScreenDiffusionParams.y);
            int mode = (int)(_ScreenDiffusionParams.z + 0.5);
            float3 result;
            if (mode == 1)
            {
                // Glow: keep the sharp image and add the diffused light over it.
                result = original.rgb + blurred * strength;
            }
            else if (mode == 2)
            {
                // Screen: brightens without clipping as hard as adding does.
                // Saturating first keeps this defined on HDR input.
                result = 1.0 - (1.0 - saturate(original.rgb)) * (1.0 - saturate(blurred * strength));
            }
            else
            {
                // Mix: straight softening, the whole screen goes blurry.
                result = lerp(original.rgb, blurred, strength);
            }
            return float4(result, original.a);
        }
        // Max variants. Taking the per-channel maximum instead of the weighted mean keeps
        // each blob at its own peak instead of averaging two hues into a desaturated middle.
        // The trade-off is that a pure max filter is a dilation: it spreads without a soft
        // falloff, so the result has flatter plateaus than the mean pyramid.
        float4 FragDownsampleMax(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 h = _ScreenDiffusionTexel.xy * _ScreenDiffusionParams.x;
            float4 m = SampleSource(uv);
            m = max(m, SampleSource(uv - h));
            m = max(m, SampleSource(uv + h));
            m = max(m, SampleSource(uv + float2(h.x, -h.y)));
            m = max(m, SampleSource(uv - float2(h.x, -h.y)));
            return m;
        }

        float4 FragUpsampleMax(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 h = _ScreenDiffusionTexel.xy * _ScreenDiffusionParams.x;
            float4 m = SampleSource(uv + float2(-h.x * 2.0, 0.0));
            m = max(m, SampleSource(uv + float2(-h.x, h.y)));
            m = max(m, SampleSource(uv + float2(0.0, h.y * 2.0)));
            m = max(m, SampleSource(uv + float2(h.x, h.y)));
            m = max(m, SampleSource(uv + float2(h.x * 2.0, 0.0)));
            m = max(m, SampleSource(uv + float2(h.x, -h.y)));
            m = max(m, SampleSource(uv + float2(0.0, -h.y * 2.0)));
            m = max(m, SampleSource(uv + float2(-h.x, -h.y)));
            return m;
        }

        // Layer mode. The capture cleared to transparent, so blurring RGBA linearly
        // already produces a premultiplied result: no renormalising, and the edges
        // fade out instead of going faint the way a rejected-tap gaussian does.
        float4 FragCompositeLayer(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SampleSource(input.texcoord) * (saturate(_ScreenDiffusionParams.y) * LayerVisibility(input.texcoord));
        }
        float4 FragOcclusion(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            if (_ScreenDiffusionGroup.y > 0.5)
            {
                float id = SAMPLE_TEXTURE2D_X_LOD(_ScreenDiffusionObjectIds, sampler_PointClamp, input.texcoord, 0).r;
                if (abs(id - _ScreenDiffusionGroup.x) > 0.25) return 0;
            }
            return SampleSource(input.texcoord) * LayerVisibility(input.texcoord);
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
        Pass
        {
            Name "CompositeLayer"
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCompositeLayer
            ENDHLSL
        }
        Pass
        {
            Name "CompositeLayerAdditive"
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCompositeLayer
            ENDHLSL
        }
        Pass
        {
            Name "DownsampleMax"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDownsampleMax
            ENDHLSL
        }
        Pass
        {
            Name "UpsampleMax"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragUpsampleMax
            ENDHLSL
        }
        Pass
        {
            // Hardware max blend: the layer never adds onto the scene, it only lifts
            // channels that are already darker than the blob.
            Name "CompositeLayerMax"
            Blend One One
            BlendOp Max
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCompositeLayer
            ENDHLSL
        }
        Pass
        {
            Name "RejectHiddenSources"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragOcclusion
            ENDHLSL
        }
    }
}
