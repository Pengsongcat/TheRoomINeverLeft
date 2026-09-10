Shader "Hidden/TRINL/ScreenBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X_FLOAT(_ScreenBlurLayerDepth);
        TEXTURE2D_X_FLOAT(_ScreenBlurHorizontalDepth);
        TEXTURE2D_X_FLOAT(_ScreenBlurSceneDepth);
        float4 _ScreenBlurParameters;
        float4 _ScreenBlurTexel;

        float EyeDepth(float raw)
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

        float SceneEyeDepth(float2 uv)
        {
            return EyeDepth(SAMPLE_TEXTURE2D_X_LOD(_ScreenBlurSceneDepth, sampler_PointClamp, uv, 0).r);
        }

        struct BlurOutput { float4 color : SV_Target0; float depth : SV_Target1; };
        BlurOutput Horizontal(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            BlurOutput output;
            output.color = 0;
            output.depth = _ProjectionParams.z;
            float sceneDepth = SceneEyeDepth(input.texcoord);
            float total = 0;
            // Contiguous pixel taps avoid stripes at silhouettes. Radius is sigma in pixels.
            float radius = clamp(_ScreenBlurParameters.x, 0.0, 100.0);
            float sigma = max(radius, 0.001);
            int support = (int)ceil(radius * 3.0);
            [loop] for (int i = -support; i <= support; i++)
            {
                float weight = exp(-0.5 * (i * i) / (sigma * sigma));
                total += weight;
                float2 uv = input.texcoord + float2(i * _ScreenBlurTexel.x, 0);
                if (any(uv < 0) || any(uv > 1)) continue;
                float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float depth = EyeDepth(SAMPLE_TEXTURE2D_X_LOD(_ScreenBlurLayerDepth, sampler_PointClamp, uv, 0).r);
                // Reject both hidden source pixels and spread across foreground occluders.
                if (color.a < 0.0001 || depth > min(sceneDepth, SceneEyeDepth(uv)) + _ScreenBlurParameters.z) continue;
                output.color += float4(color.rgb * color.a, color.a) * weight;
                output.depth = min(output.depth, depth);
            }
            output.color /= total;
            return output;
        }

        float4 Vertical(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = 0;
            float total = 0;
            float sceneDepth = SceneEyeDepth(input.texcoord);
            float radius = clamp(_ScreenBlurParameters.x, 0.0, 100.0);
            float sigma = max(radius, 0.001);
            int support = (int)ceil(radius * 3.0);
            [loop] for (int i = -support; i <= support; i++)
            {
                float weight = exp(-0.5 * (i * i) / (sigma * sigma));
                total += weight;
                float2 uv = input.texcoord + float2(0, i * _ScreenBlurTexel.y);
                if (any(uv < 0) || any(uv > 1)) continue;
                float depth = SAMPLE_TEXTURE2D_X_LOD(_ScreenBlurHorizontalDepth, sampler_PointClamp, uv, 0).r;
                if (depth > sceneDepth + _ScreenBlurParameters.z) continue;
                color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0) * weight;
            }
            return color * (_ScreenBlurParameters.y / total);
        }
        ENDHLSL
        Pass
        {
            Name "Horizontal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Horizontal
            ENDHLSL
        }
        Pass
        {
            Name "VerticalComposite"
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Vertical
            ENDHLSL
        }
    }
}
