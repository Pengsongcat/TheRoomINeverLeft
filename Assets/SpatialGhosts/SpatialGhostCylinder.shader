Shader "TRINL/Spatial Ghost Cylinder"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _GhostOpacity("Opacity", Float) = 0.3
        _EdgeSoftness("Edge Softness", Float) = 0.65
        _GhostTiling("Tiling", Vector) = (1,1,0,0)
        _GhostOffset("Offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _GhostTiling, _GhostOffset;
                float _GhostOpacity, _EdgeSoftness;
                float4 _CylinderPivot, _CylinderRight, _CylinderForward, _CylinderShape;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p = TransformObjectToWorld(input.positionOS) - _CylinderPivot.xyz;
                float3 x = _CylinderRight.xyz, z = _CylinderForward.xyz;
                float r = max(.05, _CylinderShape.x), sign = _CylinderShape.y;
                float scale = _CylinderShape.z;
                float angle = (dot(p,x) * scale + _CylinderShape.w) / r;
                float sn, cs; sincos(angle, sn, cs);
                float depth = dot(p,z);
                output.positionWS = _CylinderPivot.xyz + x * ((r-sign*depth)*sn)
                    + float3(0,p.y,0) + z * (sign*r*(1-cs)+depth*cs);
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                float metric = 1-sign*depth/r;
                metric = (metric < 0 ? -1 : 1) * max(abs(metric), .001);
                output.normalWS = normalize((x*cs+z*sign*sn) * (dot(n,x)/(scale*metric))
                    + float3(0,n.y,0) + (-x*sign*sn+z*cs)*dot(n,z));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv * _GhostTiling.xy + _GhostOffset.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float edge = abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                color.a *= _GhostOpacity * lerp(1, saturate(edge), _EdgeSoftness);
                return color;
            }
            ENDHLSL
        }
    }
}
