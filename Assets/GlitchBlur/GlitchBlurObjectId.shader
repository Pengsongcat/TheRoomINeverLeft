Shader "Hidden/TRINL/GlitchBlurObjectId"
{
    Properties
    {
        _BaseMap("Texture",2D)="white"{}
        _MaskST("UV",Vector)=(1,1,0,0)
        _ObjectId("Object",Float)=0
        _MaskCutoff("Cutoff",Float)=0
        _MaskCylinder("Cylinder",Float)=0
        _MaskPivot("Pivot",Vector)=(0,0,0,0)
        _MaskRight("Right",Vector)=(1,0,0,0)
        _MaskForward("Forward",Vector)=(0,0,1,0)
        _MaskShape("Shape",Vector)=(1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D_X_FLOAT(_GlitchBlurLayerDepth);
            CBUFFER_START(UnityPerMaterial)
            float4 _MaskST;
            float _ObjectId, _MaskCutoff;
            float _MaskCylinder;
            float4 _MaskPivot,_MaskRight,_MaskForward,_MaskShape;
            CBUFFER_END
            float _GlitchMaskObjectId;
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 screenPosition:TEXCOORD1; float eyeDepth:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A input)
            {
                V o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(input.positionOS);
                if(_MaskCylinder>.5)
                {
                    float3 p=world-_MaskPivot.xyz;
                    float radius=max(.05,_MaskShape.x),sign=_MaskShape.y;
                    float angle=(dot(p,_MaskRight.xyz)*_MaskShape.z+_MaskShape.w)/radius;
                    float depth=dot(p,_MaskForward.xyz);
                    world=_MaskPivot.xyz+_MaskRight.xyz*((radius-sign*depth)*sin(angle))
                        +float3(0,p.y,0)+_MaskForward.xyz*(sign*radius*(1-cos(angle))+depth*cos(angle));
                }
                o.positionCS=TransformWorldToHClip(world);
                o.screenPosition=ComputeScreenPos(o.positionCS);
                o.eyeDepth=-TransformWorldToView(world).z;
                o.uv=input.uv*_MaskST.xy+_MaskST.zw;return o;
            }
            float Frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float raw=SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurLayerDepth,sampler_PointClamp,input.screenPosition.xy/input.screenPosition.w,0).r;
                float depth=LinearEyeDepth(raw,_ZBufferParams);
                if(unity_OrthoParams.w>.5)
                {
                    #if UNITY_REVERSED_Z
                    raw=1-raw;
                    #endif
                    depth=lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                }
                // The ownership and color shaders may round deformed vertices differently.
                // Compare in metres instead of requiring bit-identical device depth.
                clip(max(.001,depth*.00001)-abs(input.eyeDepth-depth));
                clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a-max(.0001,_MaskCutoff));
                return _GlitchMaskObjectId;
            }
            ENDHLSL
        }
    }
}
