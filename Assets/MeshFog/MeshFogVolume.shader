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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // Volume samples must use world-space shadows, never opaque screen-space shadows.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            TEXTURE3D(_ShapeSDF); SAMPLER(sampler_ShapeSDF);
            float4x4 _FogWorldToLocal;
            float4 _BoundsMin, _BoundsSize, _FogColor, _FogParams, _NoiseParams, _Flow;
            float4 _FogLighting;
            float _FogReceiveShadows;
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
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
            float3 FogLightContribution(Light light, float3 rayDirection)
            {
                #if defined(_LIGHT_LAYERS)
                    if ((light.layerMask & GetMeshRenderingLayer()) == 0) return 0;
                #endif
                // Henyey-Greenstein relative to isotropic scattering (g = 0 gives 1).
                float g = clamp(_FogLighting.w, -0.8, 0.8);
                float d = max(0.04, 1 + g*g - 2*g*dot(light.direction, rayDirection));
                float phase = (1 - g*g) / (d * sqrt(d));
                return light.color * light.distanceAttenuation * light.shadowAttenuation * phase;
            }
            float3 FogIllumination(float3 positionWS, float3 rayDirection, float2 screenUV)
            {
                Light mainLight = GetMainLight();
                if (_FogReceiveShadows > 0.5)
                {
                    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                    shadowCoord.w = 1; // Directional shadow coordinates are already orthographic.
                    mainLight.shadowAttenuation = MainLightShadow(shadowCoord, positionWS, half4(1,1,1,1), _MainLightOcclusionProbes);
                }
                #if defined(_LIGHT_COOKIES)
                    if (IsMainLightCookieEnabled())
                    {
                        float2 cookieUV = ComputeLightCookieUVDirectional(_MainLightWorldToLight, positionWS, float4(1,1,0,0), URP_TEXTURE_WRAP_MODE_NONE);
                        // Explicit LOD: derivatives are undefined inside an early-exit volume loop.
                        real4 cookie = SAMPLE_TEXTURE2D_LOD(_MainLightCookieTexture, sampler_MainLightCookieTexture, cookieUV, 0);
                        mainLight.color *= IsMainLightCookieTextureRGBFormat() ? cookie.rgb : (IsMainLightCookieTextureAlphaFormat() ? cookie.aaa : cookie.rrr);
                    }
                #endif
                float3 lighting = FogLightContribution(mainLight, rayDirection);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
                    // Forward+ clusters are selected at each volume sample, not at the proxy surface.
                    InputData inputData = (InputData)0;
                    inputData.positionWS = positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1));
                            if (_FogReceiveShadows < 0.5) light.shadowAttenuation = 1;
                            lighting += FogLightContribution(light, rayDirection);
                        }
                    #endif
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light;
                        if (_FogReceiveShadows > 0.5)
                            light = GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1));
                        else
                        {
                            light = GetAdditionalLight(lightIndex, positionWS);
                            #if defined(_LIGHT_COOKIES)
                                #if USE_CLUSTER_LIGHT_LOOP
                                    int cookieIndex = lightIndex;
                                #else
                                    int cookieIndex = GetPerObjectLightIndex(lightIndex);
                                #endif
                                light.color *= SampleAdditionalLightCookie(cookieIndex, positionWS);
                            #endif
                        }
                        lighting += FogLightContribution(light, rayDirection);
                    LIGHT_LOOP_END
                #endif
                return _FogLighting.y + lighting * _FogLighting.z;
            }
            struct FogOutput { float4 color : SV_Target; float depth : SV_Depth; };
            FogOutput Frag(Varyings input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                UNITY_SETUP_INSTANCE_ID(input);
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
                    float3 illumination = 1;
                    if (_FogLighting.x > 0.5 && alpha > 0.00001)
                        illumination = FogIllumination(origin + direction * (start + (i + 0.5) * stepLength), direction, uv);
                    accumulated += transmission * alpha * _FogColor.rgb * lerp(0.65, 1.15, noise) * illumination;
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
