Shader "Hidden/TRINL/GlitchBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X_FLOAT(_GlitchBlurLayerDepth);
        TEXTURE2D_X_FLOAT(_GlitchBlurHorizontalDepth);
        TEXTURE2D_X_FLOAT(_GlitchBlurSceneDepth);
        float4 _GlitchBlurParameters; // horizontal half-width, opacity, depth bias, vertical sigma
        float4 _GlitchBlurTexel;
        float4 _GlitchBlurShape; // original weight, row jitter, row count, drift speed
        float4 _GlitchBlurBands; // elapsed time
        float4 _GlitchBlurOuter; // outer half-width, outer original weight
        TEXTURE2D_X(_GlitchBlurCylinderSource);
        float4 _GlitchCylinderRange, _GlitchCylinderShape, _GlitchCylinderGrid, _GlitchCylinderPlayer;
        TEXTURE2D_X_FLOAT(_GlitchBlurObjectIds);
        float4 _GlitchBlurGroup;
        float4 _GlitchGaussian;
        float4 _GlitchGaussianResponse; // enabled, distance threshold, near strength, far strength
        float4 _GlitchUpward, _GlitchUpwardWeights;
        TEXTURE2D_X(_GlitchSurfaceNormals);
        float UpMask(float2 uv)
        {
            float4 n=SAMPLE_TEXTURE2D_X_LOD(_GlitchSurfaceNormals,sampler_PointClamp,uv,0);
            float ny=_GlitchUpward.w>.5 ? n.y : n.x;
            return n.a>.5 ? smoothstep(_GlitchUpward.y-_GlitchUpward.z*.5,_GlitchUpward.y+_GlitchUpward.z*.5,ny) : 0;
        }
        float2 UpFactors(float2 uv)
        {
            if(_GlitchUpward.x<.5)return float2(1,1);
            float m=UpMask(uv);
            return float2(lerp(_GlitchUpwardWeights.y,_GlitchUpwardWeights.x,m),lerp(_GlitchUpwardWeights.w,_GlitchUpwardWeights.z,m));
        }
        TEXTURE2D_X(_GlitchGaussianOriginal);
        bool BelongsToGroup(float2 uv)
        {
            if (_GlitchBlurGroup.y < .5) return true;
            float id = SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurObjectIds,sampler_PointClamp,uv,0).r;
            return abs(id-_GlitchBlurGroup.x)<.25;
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

        float SceneDepth(float2 uv)
        {
            return EyeDepth(SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurSceneDepth, sampler_PointClamp, uv, 0).r);
        }

        float4 ReadVisible(float2 uv, float destinationDepth, out float depth)
        {
            depth = _ProjectionParams.z;
            if (any(uv < 0) || any(uv > 1)) return 0;
            if (!BelongsToGroup(uv)) return 0;
            float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
            float sourceDepth = EyeDepth(SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurLayerDepth, sampler_PointClamp, uv, 0).r);
            if (color.a < 0.0001 || sourceDepth > min(destinationDepth, SceneDepth(uv)) + _GlitchBlurParameters.z) return 0;
            depth = sourceDepth;
            return color;
        }

        struct SmearOutput { float4 color : SV_Target0; float depth : SV_Target1; };
        SmearOutput Horizontal(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            SmearOutput output;
            output.color = 0;
            output.depth = _ProjectionParams.z;
            float sceneDepth = SceneDepth(input.texcoord);
            float width = clamp(_GlitchBlurParameters.x, 0, 256);
            float outerWidth = clamp(_GlitchBlurOuter.x, 0, 256);
            float maxWidth = max(width, outerWidth);
            float coreDepth;
            float4 core = ReadVisible(input.texcoord, sceneDepth, coreDepth);
            if(_GlitchUpward.x>1.5)
            {
                output.color=float4(UpMask(input.texcoord).xxx*core.a,core.a);
                output.depth=coreDepth;return output;
            }
            float nearest = core.a > 0.0001 ? 0 : maxWidth + 1;
            float band = floor(input.texcoord.y * _GlitchBlurShape.z);
            float rowShift = sin(band * 12.9898 + _GlitchBlurBands.x * _GlitchBlurShape.w)
                * _GlitchBlurShape.y * width;
            int support = (int)ceil(maxWidth);
            float2 total = 0;
            float4 nearColor = 0, outerColor = 0;
            if(_GlitchUpward.x>.5)
            {
                support=(int)ceil(maxWidth*max(_GlitchUpwardWeights.x,_GlitchUpwardWeights.y));
                [loop]for(int j=-support;j<=support;j++)
                {
                    float2 uv=input.texcoord+float2((j+rowShift)*_GlitchBlurTexel.x,0);
                    float d;float4 c=ReadVisible(uv,sceneDepth,d);
                    float2 factors=UpFactors(uv);
                    float2 widths=float2(width,outerWidth)*factors.x;
                    float2 w=float2(abs(j)<=ceil(widths.x) ? exp(-1.5*j*j/max(widths.x*widths.x,.001))/max(1,1.3267*widths.x) : 0,
                        abs(j)<=ceil(widths.y) ? exp(-1.5*j*j/max(widths.y*widths.y,.001))/max(1,1.3267*widths.y) : 0);
                    nearColor+=c*w.x*factors.y;outerColor+=c*w.y*factors.y;
                    if(c.a>.0001){nearest=min(nearest,abs(j+rowShift));output.depth=min(output.depth,d);}
                }
                float t=smoothstep(0,max(maxWidth*.5,1),nearest);
                float keep=saturate(lerp(_GlitchBlurShape.x,_GlitchBlurOuter.y,t));
                float contribution=UpFactors(input.texcoord).y;
                output.color=lerp(nearColor,outerColor,t)*(1-keep)+core*(1-contribution*(1-keep));
                output.depth=min(output.depth,coreDepth);
                return output;
            }
            // Contiguous taps preserve thin stems; length changes coverage, not sample spacing.
            [loop] for (int i = -support; i <= support; i++)
            {
                float2 weight = float2(
                    abs(i) <= ceil(width) ? exp(-1.5 * (i * i) / max(width * width, 0.001)) : 0,
                    abs(i) <= ceil(outerWidth) ? exp(-1.5 * (i * i) / max(outerWidth * outerWidth, 0.001)) : 0);
                float2 uv = input.texcoord + float2((i + rowShift) * _GlitchBlurTexel.x, 0);
                float depth;
                float4 source = ReadVisible(uv, sceneDepth, depth);
                nearColor += source * weight.x;
                outerColor += source * weight.y;
                if (source.a > 0.0001) nearest = min(nearest, abs(i + rowShift));
                output.depth = min(output.depth, depth);
                total += weight;
            }
            // Source-relative transition, independently for each eye and each scanline.
            // Geometry pixels use near settings; exterior trails approach outer settings.
            // Half the maximum support is the transition span; equal endpoints reproduce
            // the former uniform kernel without changing the object's material or layer.
            float transition = smoothstep(0, max(maxWidth * 0.5, 1), nearest);
            output.color = lerp(nearColor / max(total.x, 0.0001), outerColor / max(total.y, 0.0001), transition);
            float keep = saturate(lerp(_GlitchBlurShape.x, _GlitchBlurOuter.y, transition));
            output.color = lerp(output.color, core, keep);
            if (keep > 0) output.depth = min(output.depth, coreDepth);
            return output;
        }

        SmearOutput Gaussian(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            SmearOutput output;
            output.color=0; output.depth=_ProjectionParams.z;
            bool vertical=_GlitchGaussian.w>.5;
            float sigma=vertical ? _GlitchGaussian.y : _GlitchGaussian.x;
            float2 direction=vertical ? float2(0,_GlitchBlurTexel.y) : float2(_GlitchBlurTexel.x,0);
            int support=(int)ceil(3*sigma);
            float total=0, sceneDepth=SceneDepth(input.texcoord);
            [loop] for(int i=-support;i<=support;i++)
            {
                float weight=exp(-.5*i*i/max(sigma*sigma,.0001));
                total+=weight;
                float2 uv=input.texcoord+direction*i;
                if(any(uv<0)||any(uv>1))continue;
                float depth=SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurHorizontalDepth,sampler_PointClamp,uv,0).r;
                if(depth>sceneDepth+_GlitchBlurParameters.z)continue;
                float4 sampleColor=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv,0);
                output.color+=sampleColor*weight;
                if(sampleColor.a>.00001)output.depth=min(output.depth,depth);
            }
            output.color/=max(total,.0001);
            if(vertical)
            {
                float4 original=SAMPLE_TEXTURE2D_X_LOD(_GlitchGaussianOriginal,sampler_LinearClamp,input.texcoord,0);
                float strength=_GlitchGaussian.z;
                if(_GlitchGaussianResponse.x>.5)
                {
                    // Reconstruct the visible trail surface, using a shared player position
                    // for Scene View, game cameras and both XR eyes.
                    float eyeDepth=max(output.depth,.0001);
                    float raw;
                    if(unity_OrthoParams.w>.5)
                    {
                        raw=saturate((eyeDepth-_ProjectionParams.y)/(_ProjectionParams.z-_ProjectionParams.y));
                        #if UNITY_REVERSED_Z
                        raw=1-raw;
                        #endif
                    }
                    else raw=(rcp(eyeDepth)-_ZBufferParams.w)/_ZBufferParams.z;
                    #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                    #endif
                    float3 world=ComputeWorldSpacePosition(input.texcoord,raw,UNITY_MATRIX_I_VP);
                    float t=saturate(distance(world,_GlitchCylinderPlayer.xyz)/max(.01,_GlitchGaussianResponse.y));
                    t=t*t*(3-2*t);
                    strength=lerp(_GlitchGaussianResponse.z,_GlitchGaussianResponse.w,t);
                }
                output.color=lerp(original,output.color,strength);
            }
            return output;
        }

        float4 Composite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = 0;
            float total = 0;
            float sceneDepth = SceneDepth(input.texcoord);
            float sigma = clamp(_GlitchBlurParameters.w, 0, 16);
            int support = (int)ceil(sigma * 3);
            [loop] for (int i = -support; i <= support; i++)
            {
                float weight = exp(-0.5 * (i * i) / max(sigma * sigma, 0.001));
                total += weight;
                float2 uv = input.texcoord + float2(0, i * _GlitchBlurTexel.y);
                if (any(uv < 0) || any(uv > 1)) continue;
                float depth = SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurHorizontalDepth, sampler_PointClamp, uv, 0).r;
                if (depth > sceneDepth + _GlitchBlurParameters.z) continue;
                color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0) * weight;
            }
            color *= (_GlitchBlurParameters.y / max(total, 0.0001));
            color.a = saturate(color.a);
            return color;
        }

        struct CylinderAttributes { uint vertexID : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct CylinderVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 screenPosition : TEXCOORD1;
            float eyeDepth : TEXCOORD2;
            nointerpolation float weight : TEXCOORD3;
            nointerpolation float sourceDepth : TEXCOORD4;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        CylinderVaryings CylinderVert(CylinderAttributes input)
        {
            CylinderVaryings output = (CylinderVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            uint width = (uint)_GlitchCylinderGrid.x;
            uint cells = width * (uint)_GlitchCylinderGrid.y;
            uint cell = (input.vertexID / 6) % cells;
            uint tap = (input.vertexID / 6) / cells;
            uint corner = input.vertexID % 6;
            float2 corners[6] = {float2(0,0),float2(1,0),float2(1,1),float2(0,0),float2(1,1),float2(0,1)};
            float2 centerUV = (float2(cell % width, cell / width) + .5) / _GlitchCylinderGrid.xy;
            float2 uv = (float2(cell % width, cell / width) + corners[corner]) / _GlitchCylinderGrid.xy;
            float raw = SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurLayerDepth,sampler_PointClamp,centerUV,0).r;
            output.sourceDepth = EyeDepth(raw);
            output.uv = centerUV;
            if (!BelongsToGroup(centerUV) || SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurCylinderSource,sampler_PointClamp,centerUV,0).a < .0001
                || output.sourceDepth > SceneDepth(centerUV)+_GlitchBlurParameters.z)
            {
                output.positionCS = float4(0,0,0,1);
                return output;
            }
            #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
            #endif
            float3 center = ComputeWorldSpacePosition(centerUV,raw,UNITY_MATRIX_I_VP);
            float3 world = ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
            float3 toward = _GlitchCylinderPlayer.xyz-center; toward.y=0;
            float distance = length(toward);
            toward = distance > .0001 ? toward / distance : float3(0,0,1);
            float3 right = cross(float3(0,1,0),toward);
            float blend = smoothstep(_GlitchCylinderRange.x,_GlitchCylinderRange.y,distance);
            float radius = lerp(_GlitchCylinderRange.z,_GlitchCylinderRange.w,blend);
            float t = 2.0 * tap / max(1,_GlitchCylinderGrid.z-1) - 1;
            float transition = smoothstep(0,1,abs(t));
            float lengthMeters = lerp(_GlitchCylinderShape.x,_GlitchCylinderShape.y,transition);
            float2 upward=UpFactors(centerUV);
            lengthMeters*=upward.x;
            float row = floor(centerUV.y*_GlitchBlurShape.z);
            float shift = sin(row*12.9898+_GlitchBlurBands.x*_GlitchBlurShape.w)*_GlitchBlurShape.y*lengthMeters;
            // Keep the central source in place; jitter only the trail samples.
            float arc = t*lengthMeters + (abs(t)>.0001 ? shift : 0);
            float angle = arc/max(.05,radius);
            world += right*(radius*sin(angle)) + toward*(_GlitchCylinderShape.z*radius*(1-cos(angle)));
            output.positionCS = TransformWorldToHClip(world);
            output.screenPosition = ComputeScreenPos(output.positionCS);
            output.eyeDepth = -TransformWorldToView(world).z;
            output.uv = centerUV;
            float keep = lerp(_GlitchBlurShape.x,_GlitchBlurOuter.y,transition);
            output.weight = ((1-keep)/_GlitchCylinderGrid.z + (abs(t)<.0001 ? _GlitchBlurShape.x : 0)) / _GlitchCylinderGrid.w;
            output.weight=output.weight*upward.y + (abs(t)<.0001 ? 1-upward.y : 0);
            return output;
        }
        SmearOutput CylinderFrag(CylinderVaryings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = SAMPLE_TEXTURE2D_X_LOD(_GlitchBlurCylinderSource,sampler_PointClamp,input.uv,0);
            clip(color.a-.0001);
            clip(input.weight-.000001);
            clip(SceneDepth(input.uv)+_GlitchBlurParameters.z-input.sourceDepth);
            float2 destinationUV = input.screenPosition.xy/input.screenPosition.w;
            clip(SceneDepth(destinationUV)+_GlitchBlurParameters.z-input.eyeDepth);
            SmearOutput output;
            output.color = color * input.weight;
            output.depth = input.eyeDepth;
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "HorizontalSmear"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Horizontal
            ENDHLSL
        }
        Pass
        {
            Name "SoftnessComposite"
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
        Pass
        {
            Name "CylinderDepthReprojection"
            Blend 0 One One
            BlendOp 0 Add
            Blend 1 One One
            BlendOp 1 Min
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma vertex CylinderVert
            #pragma fragment CylinderFrag
            ENDHLSL
        }
        Pass
        {
            Name "GaussianX"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Gaussian
            ENDHLSL
        }
        Pass
        {
            Name "GaussianY"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Gaussian
            ENDHLSL
        }
    }
}
