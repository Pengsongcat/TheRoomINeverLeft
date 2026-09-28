Shader "TRINL/Door Portal Mask"
{
 Properties {
  _StencilRef("Stencil", Float)=3
  _StencilRead("Read Mask", Float)=2
  _StencilWrite("Write Mask", Float)=3
  _StencilComp("Compare", Float)=8
  _StencilPass("Pass", Float)=2
  _ZTest("Depth Test", Float)=4
  _ZWrite("Depth Write", Float)=0
  _ColorMask("Color Mask", Float)=0
  _ClearDepth("Reset Portal Depth", Float)=0
  [HDR] _BaseColor("Background", Color)=(2,2,2,1)
  _PortalPlane("Front Plane", Vector)=(0,0,-1,3.23)
 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-499" "RenderType"="Transparent" }
  Pass {
   Tags { "LightMode"="DoorPortalOnly" }
   Cull Off ZTest [_ZTest] ZWrite [_ZWrite] ColorMask [_ColorMask]
   Stencil { Ref [_StencilRef] ReadMask [_StencilRead] WriteMask [_StencilWrite] Comp [_StencilComp] Pass [_StencilPass] }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _PortalPlane; half4 _BaseColor; float _ClearDepth;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
   V vert(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS=TransformObjectToHClip(a.positionOS.xyz); return o; }
   struct O { half4 color:SV_Target; float depth:SV_Depth; };
   O frag(V i) {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(dot(_PortalPlane.xyz,GetCameraPositionWS())+_PortalPlane.w-0.002);
    O o; o.color=half4(_BaseColor.rgb,1);
    #if UNITY_REVERSED_Z
     float farDepth=0;
    #else
     float farDepth=1;
    #endif
    o.depth=_ClearDepth>0.5?farDepth:i.positionCS.z;
    return o;
   }
   ENDHLSL
  }
 }
}

