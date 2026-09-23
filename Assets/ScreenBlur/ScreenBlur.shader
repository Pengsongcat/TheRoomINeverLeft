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
        TEXTURE2D_X_FLOAT(_ScreenBlurSurfaceNoise);
        TEXTURE2D_X_FLOAT(_ScreenBlurHorizontalNoise);
        float4 _ScreenBlurCameraGrain, _ScreenBlurCameraExposure;
        float _ScreenBlurNoiseMode;
        float4 _ScreenBlurParameters;
        float4 _ScreenBlurTexel;

        TEXTURE2D_X_FLOAT(_ScreenBlurObjectIds);
        // x = this pass's group id, y = 1 when at least one override group exists.
        float4 _ScreenBlurGroup;

        // The id buffer clears to 0, so the global group owns every pixel that no
        // override claimed. Guarded by .y so the texture stays unbound when unused.
        bool BelongsToGroup(float2 uv)
        {
            if (_ScreenBlurGroup.y < 0.5) return true;
            float id = SAMPLE_TEXTURE2D_X_LOD(_ScreenBlurObjectIds, sampler_PointClamp, uv, 0).r;
            return abs(id - _ScreenBlurGroup.x) < 0.25;
        }

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

        struct BlurOutput { float4 color : SV_Target0; float depth : SV_Target1; float noise : SV_Target2; };
        BlurOutput Horizontal(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            BlurOutput output;
            output.color = 0;
            output.noise = 1;
            float nearestNoise = 1e9;
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
                if (color.a < 0.0001 || !BelongsToGroup(uv) || depth > min(sceneDepth, SceneEyeDepth(uv)) + _ScreenBlurParameters.z) continue;
                // Alpha-blended captures already contain premultiplied RGB over the clear target.
                // Multiplying again would darken translucent spatial ghosts twice.
                if (_ScreenBlurNoiseMode > 0.5)
                {
                    float n = SAMPLE_TEXTURE2D_X_LOD(_ScreenBlurSurfaceNoise,sampler_PointClamp,uv,0).r;
                    if (_ScreenBlurNoiseMode < 1.5) color.rgb *= n;
                    if (abs(i) < nearestNoise) { nearestNoise=abs(i); output.noise=n; }
                }
                output.color += color * weight;
                output.depth = min(output.depth, depth);
            }
            output.color /= total;
            return output;
        }

        float GrainHash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float4 ApplyCameraGrain(float4 color, float2 pixel)
        {
            if (_ScreenBlurCameraExposure.z < 0.5 || color.a <= 0.00001) return color;
            // Work on straight RGB, then re-premultiply. Never inject noise into uncovered pixels.
            float3 rgb = color.rgb / color.a;
            rgb *= exp2(_ScreenBlurCameraExposure.x);
            if (_ScreenBlurCameraExposure.y > 0.5) rgb = saturate(rgb);
            float2 cell = floor(pixel / max(_ScreenBlurCameraGrain.y, 1));
            float frame = floor(_Time.y * _ScreenBlurCameraGrain.w);
            float mono = GrainHash(float3(cell, frame));
            float3 chroma = float3(
                GrainHash(float3(cell + 19.1, frame + 7)),
                GrainHash(float3(cell + 73.7, frame + 23)),
                GrainHash(float3(cell + 41.3, frame + 59)));
            float3 grain = lerp(mono.xxx, chroma, _ScreenBlurCameraGrain.z) * 2 - 1;
            rgb = max(0, rgb + grain * _ScreenBlurCameraGrain.x);
            if (_ScreenBlurCameraExposure.y > 0.5) rgb = saturate(rgb);
            color.rgb = rgb * color.a;
            return color;
        }

        float4 Vertical(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = 0;
            float postNoise=1, nearestNoise=1e9;
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
                float4 sampleColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                color += sampleColor * weight;
                if (_ScreenBlurNoiseMode > 1.5 && sampleColor.a > 0.0001 && abs(i) < nearestNoise)
                {
                    nearestNoise=abs(i);
                    postNoise=SAMPLE_TEXTURE2D_X_LOD(_ScreenBlurHorizontalNoise,sampler_PointClamp,uv,0).r;
                }
            }
            color.rgb *= postNoise;
            color /= total;
            color = ApplyCameraGrain(color, input.positionCS.xy);
            return color * _ScreenBlurParameters.y;
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
