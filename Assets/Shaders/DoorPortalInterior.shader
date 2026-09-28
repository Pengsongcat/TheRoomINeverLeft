Shader "TRINL/Door Portal Interior"
{
 Properties {
  [HDR] _BaseColor("White Emission",Color)=(2,2,2,1)
  _SurfaceDefinition("Surface Definition",Range(0,0.5))=0.12
  _InteriorBaseBrightness("Interior Base Brightness",Range(0,1))=1
  _PortalPlane("Front Plane",Vector)=(0,0,-1,3.23)
 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-497" "RenderType"="Transparent" }
  Pass {
   Tags { "LightMode"="DoorPortalOnly" }
   Cull Off ZWrite On ZTest LEqual
   Stencil {
 Ref 3
 ReadMask 2
 WriteMask 0
 Comp Equal
 Pass Keep
 }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor; float4 _PortalPlane; float _SurfaceDefinition; float _InteriorBaseBrightness; float4 _InteriorLightPositionRange; half4 _InteriorLightColor;
   CBUFFER_END
   float _DoorPortalCameraOpacity;
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   V vert(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.normalOS);return o; }
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(dot(_PortalPlane.xyz,GetCameraPositionWS())+_PortalPlane.w-0.002);
    clip(0.01-(dot(_PortalPlane.xyz,i.positionWS)+_PortalPlane.w));
    float definition=1-_SurfaceDefinition*(1-abs(dot(normalize(i.normalWS),normalize(float3(0.35,0.8,0.48)))));
    float3 toLight = _InteriorLightPositionRange.xyz - i.positionWS;
    float distanceSquared = dot(toLight,toLight);
    float range = max(_InteriorLightPositionRange.w,0.001);
    float falloff = saturate(1.0 - distanceSquared/(range*range));
    falloff *= falloff;
    // Two-sided room shell: support imported meshes with inward or outward normals.
    float diffuse = abs(dot(normalize(i.normalWS),toLight*rsqrt(max(distanceSquared,0.0001))));
    half3 lighting = _InteriorLightColor.rgb * (diffuse*falloff/(1.0+distanceSquared));
    half opacity = saturate(_DoorPortalCameraOpacity);
    // Same as inside: premultiplied colour, alpha left for the passthrough underlay, so the real world shows through.
    return half4(_BaseColor.rgb*(definition*_InteriorBaseBrightness+lighting)*opacity,opacity);
   }
   ENDHLSL
  }
  Pass {
   Tags { "LightMode"="DoorPortalInside" }
   Cull Off ZWrite On ZTest LEqual
   Blend Off // Replace background with premultiplied virtual color + compositor alpha.
   Stencil {
 Ref 1
 ReadMask 1
 WriteMask 1
 Comp Always
 Pass Replace
 }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor; float4 _PortalPlane; float _SurfaceDefinition; float _InteriorBaseBrightness; float4 _InteriorLightPositionRange; half4 _InteriorLightColor;
   CBUFFER_END
   float _DoorPortalCameraOpacity;
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   V vert(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.normalOS);return o; }
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    clip(0.01-(dot(_PortalPlane.xyz,i.positionWS)+_PortalPlane.w));
    float definition=1-_SurfaceDefinition*(1-abs(dot(normalize(i.normalWS),normalize(float3(0.35,0.8,0.48)))));
    float3 toLight = _InteriorLightPositionRange.xyz - i.positionWS;
    float distanceSquared = dot(toLight,toLight);
    float range = max(_InteriorLightPositionRange.w,0.001);
    float falloff = saturate(1.0 - distanceSquared/(range*range));
    falloff *= falloff;
    // Two-sided room shell: support imported meshes with inward or outward normals.
    float diffuse = abs(dot(normalize(i.normalWS),toLight*rsqrt(max(distanceSquared,0.0001))));
    half3 lighting = _InteriorLightColor.rgb * (diffuse*falloff/(1.0+distanceSquared));
    half opacity = saturate(_DoorPortalCameraOpacity);
    // Keep depth occlusion, but leave alpha for the passthrough underlay compositor.
    return half4(_BaseColor.rgb*(definition*_InteriorBaseBrightness+lighting)*opacity,opacity);
   }
   ENDHLSL
  }
 }
}






