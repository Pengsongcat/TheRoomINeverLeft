Shader "TRINL/Door Portal Depth"
{
 Properties { [HDR] _BaseColor("Background",Color)=(1.2,1.2,1.2,1) _PortalPlane("Plane",Vector)=(0,0,-1,3.23) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" }
 Pass {
 Tags { "LightMode"="DoorPortalOnly" }
 Cull Off ZTest Always ZWrite On
 Stencil { Ref 3 ReadMask 2 WriteMask 0 Comp Equal Pass Keep }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _PortalPlane;half4 _BaseColor;
 CBUFFER_END
 struct A {float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);
 #if UNITY_REVERSED_Z
 o.p.z=0;
 #else
 o.p.z=o.p.w;
 #endif
 return o;}
 half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);clip(dot(_PortalPlane.xyz,GetCameraPositionWS())+_PortalPlane.w-0.002);return half4(_BaseColor.rgb,1);}
 ENDHLSL
 }
 }
}
