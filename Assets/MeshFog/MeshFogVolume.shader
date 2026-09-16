Shader "TRINL/Mesh Fog Volume"
{
    Properties { _ShapeSDF("Shape SDF", 3D) = "" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "MeshFogVolume"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            Cull Front ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE3D(_ShapeSDF); SAMPLER(sampler_ShapeSDF);
            float4x4 _FogWorldToLocal;
            float4 _BoundsMin, _BoundsSize, _FogColor, _FogParams, _NoiseParams, _Flow;
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Interpolated clip coordinates are independent of the render-target size.
                // SV_POSITION / _ScaledScreenParams is incorrect in downsampled layer captures.
                o.screenPosition = ComputeScreenPos(o.positionCS);
                return o;
            }
            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell+float3(1,0,0)), f.x),
                                 lerp(Hash(cell+float3(0,1,0)), Hash(cell+float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(cell+float3(0,0,1)), Hash(cell+float3(1,0,1)), f.x),
                                 lerp(Hash(cell+float3(0,1,1)), Hash(cell+float3(1,1,1)), f.x), f.y), f.z);
            }
            struct FogOutput { float4 color : SV_Target; float depth : SV_Depth; };
            FogOutput Frag(Varyings input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.screenPosition.xy / input.screenPosition.w;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float nearDepth = 1, farDepth = 0.0001;
                #else
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                    float nearDepth = UNITY_NEAR_CLIP_VALUE, farDepth = 0.9999;
                #endif
                // Unprojection uses the current stereo eye and also handles orthographic cameras.
                float3 origin = ComputeWorldSpacePosition(uv, nearDepth, UNITY_MATRIX_I_VP);
                float3 farPoint = ComputeWorldSpacePosition(uv, farDepth, UNITY_MATRIX_I_VP);
                float3 direction = normalize(farPoint - origin);
                float3 scenePoint = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float maxDistance = max(0, dot(scenePoint - origin, direction));
                float3 ro = mul(_FogWorldToLocal, float4(origin, 1)).xyz;
                // Do not normalize: t remains a world-space distance under non-uniform scaling.
                float3 rd = mul((float3x3)_FogWorldToLocal, direction);
                float3 safeRd = float3(rd.x >= 0 ? 1 : -1, rd.y >= 0 ? 1 : -1, rd.z >= 0 ? 1 : -1) * max(abs(rd), 1e-7);
                float3 a = (_BoundsMin.xyz - ro) / safeRd;
                float3 b = (_BoundsMin.xyz + _BoundsSize.xyz - ro) / safeRd;
                float3 entry = min(a,b), leave = max(a,b);
                float start = max(0, max(entry.x, max(entry.y, entry.z)));
                float end = min(maxDistance, min(leave.x, min(leave.y, leave.z)));
                if (end <= start || _FogParams.w >= 1) discard;
                int count = (int)clamp(_NoiseParams.z, 16, 128);
                float stepLength = (end - start) / count;
                float transmission = 1;
                float3 accumulated = 0;
                float firstDistance = end;
                [loop] for (int i=0; i<count; i++)
                {
                    float3 p = ro + rd * (start + (i + 0.5) * stepLength);
                    float3 texcoord = (p - _BoundsMin.xyz) / _BoundsSize.xyz;
                    float sdf = SAMPLE_TEXTURE3D_LOD(_ShapeSDF, sampler_ShapeSDF, texcoord, 0).r;
                    float3 np = p * _NoiseParams.x - _Flow.xyz * _Time.y;
                    float noise = Noise(np) * 0.7 + Noise(np * 2.03 + 7.1) * 0.3;
                    sdf += (noise - 0.5) * 2 * _NoiseParams.y;
                    float shape = 1 - smoothstep(-_FogParams.y, _FogParams.y, sdf);
                    // Fade to zero at the baking boundary even if the chosen edge exceeds padding.
                    float3 border = min(texcoord, 1-texcoord) * _BoundsSize.xyz;
                    shape *= smoothstep(0, _FogParams.y, min(border.x, min(border.y, border.z)));
                    float variation = lerp(1, smoothstep(0.15, 0.8, noise), _FogParams.z);
                    float extinction = _FogParams.x * shape * variation * (1 - _FogParams.w);
                    float alpha = 1 - exp(-extinction * stepLength);
                    if (alpha > 0.00001) firstDistance = min(firstDistance, start + (i + 0.5) * stepLength);
                    accumulated += transmission * alpha * _FogColor.rgb * lerp(0.65, 1.15, noise);
                    transmission *= 1 - alpha;
                    if (transmission < 0.005) break;
                }
                // Empty proxy pixels must not write a box-surface depth into layer captures.
                if (1 - transmission < 0.0001) discard;
                float4 firstClip = TransformWorldToHClip(origin + direction * firstDistance);
                float deviceDepth = firstClip.z / firstClip.w;
                #if !UNITY_REVERSED_Z
                    deviceDepth = (deviceDepth - UNITY_NEAR_CLIP_VALUE) / (1 - UNITY_NEAR_CLIP_VALUE);
                #endif
                FogOutput output;
                output.color = float4(accumulated, 1 - transmission);
                // Normal fog keeps ZWrite Off. Capture passes that enable depth writes now
                // receive the first contributing fog sample rather than the proxy back face.
                output.depth = saturate(deviceDepth);
                return output;
            }
            ENDHLSL
        }
    }
}
