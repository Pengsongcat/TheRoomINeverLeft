Shader "TRINL/Art Directed Sunset Sky"
{
    Properties { _Exposure("Exposure", Float)=1 }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Zenith, _Blue, _Haze, _Orange, _Rim, _Ground;
            float4 _Shape; // horizon, band width, transition height, edge softness (degrees)
            float4 _Orientation; // yaw, tilt, curve, directional warmth
            float _Exposure;
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.direction=v.positionOS.xyz; return o; }
            float4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction);
                float sy,cy,st,ct; sincos(radians(_Orientation.x),sy,cy); sincos(radians(_Orientation.y),st,ct);
                d=float3(cy*d.x-sy*d.z,d.y,sy*d.x+cy*d.z);
                d=float3(d.x,ct*d.y-st*d.z,st*d.y+ct*d.z);
                float facing=d.z/max(length(d.xz),0.0001);
                float h=degrees(asin(clamp(d.y,-1,1)))-_Shape.x-_Orientation.z*(facing-1);
                float band=max(_Shape.y,0.01), height=max(_Shape.z,band*2.5);
                float3 c=lerp(_Rim.rgb,_Orange.rgb,smoothstep(0,band*0.5,h));
                c=lerp(c,_Haze.rgb,smoothstep(band*0.5,band*2,h));
                c=lerp(c,_Blue.rgb,smoothstep(band*1.4,height,h));
                c=lerp(c,_Zenith.rgb,smoothstep(height,height*2.5,h));
                float warm=saturate(_Orientation.w)*(1-facing)*0.5;
                c=lerp(c,_Blue.rgb,warm*(1-smoothstep(band,height,h)));
                c=lerp(_Ground.rgb,c,smoothstep(-max(_Shape.w,0.001),max(_Shape.w,0.001),h));
                return float4(c*max(_Exposure,0),1);
            }
            ENDHLSL
        }
    }
}
