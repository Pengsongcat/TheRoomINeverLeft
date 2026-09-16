using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering
{
    // Both the renderer's Opaque and Transparent Layer Masks must exclude these layers;
    // camera culling must include them. This feature draws both queues exactly once.
    public sealed class GlitchBlurRendererFeature : ScriptableRendererFeature
    {
        public LayerMask layerMask = 1 << 8;
        [Range(0,32)] public float gaussianRadius;
        [Range(0,1)] public float gaussianStrength = 1;
        public bool gaussianDistanceResponse;
        [Min(.01f)] public float gaussianResponseDistance = 5;
        [Range(0,1)] public float gaussianNearStrength = .6f;
        [Range(0,1)] public float gaussianFarStrength = 1;
        public bool cylinderTrails;
        public bool upwardResponse;
        [Range(-1,1)] public float upwardThreshold = .35f;
        [Range(.01f,1)] public float upwardSoftness = .3f;
        [Range(0,3)] public float upwardLength = 1.5f;
        [Range(0,3)] public float otherLength = .2f;
        [Range(0,1)] public float upwardContribution = 1;
        [Range(0,1)] public float otherContribution = .15f;
        public bool upwardTwoSided = true;
        public bool upwardPreview;
        public Vector2 cylinderDistances = new Vector2(.6f, 4f);
        public Vector2 cylinderRadii = new Vector2(.8f, 12f);
        public Vector2 cylinderLengths = new Vector2(.6f, 1.2f);
        public bool cylinderTowardPlayer = true;
        [Range(9, 65)] public int cylinderSamples = 33;
        [Range(64, 512)] public int cylinderResolution = 256;
        [Header("内圈：主体附近，较少拖影")]
        [Range(0, 256), InspectorName("横向拖影长度（像素）")]
        [Tooltip("Horizontal Length：源物体轮廓内及附近的拖影长度。")]
        public float horizontalLength = 100;
        [Range(0, 16), InspectorName("纵向柔化（像素）")] public float verticalSoftness = 2;
        [Range(0, 1), InspectorName("内圈原像保留（越高越清晰）")] public float originalWeight = 0.85f;
        [Header("外圈：左右外围，较强拖影")]
        [Range(0, 256), InspectorName("外围拖影长度（像素）")]
        [Tooltip("Outer Horizontal Length：远离源物体轮廓时的拖影长度。设为与横向拖影长度相同可取消长度渐变。")]
        public float outerHorizontalLength = 150;
        [Range(0, 1), InspectorName("外圈原像保留（越低拖影越强）")]
        [Tooltip("Outer Original Weight：外围与未拖影画面的混合比例。轮廓外没有原像，因此调高会收淡外围拖影。")]
        public float outerOriginalWeight = 0.05f;
        [Header("通用")]
        [Range(0, 0.5f), InspectorName("横向色带错位")] public float bandJitter = 0.08f;
        [Range(4, 128), InspectorName("色带数量")] public int bandCount = 40;
        [Range(0, 2), InspectorName("色带变化速度")] public float driftSpeed = 0;
        [Range(1, 4), InspectorName("降采样倍率")] public int downsample = 2;
        [Range(0, 1)] public float opacity = 1f;
        [Min(0)] public float depthBias = 0.005f;
        [SerializeField] Shader blurShader;
        [SerializeField] Shader objectIdShader;
        Material material;
        BlurPass pass;
        readonly List<BlurPass> localPasses = new();
        readonly Dictionary<long,Material> globalNormalMasks = new();

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (blurShader != null) material = CoreUtils.CreateEngineMaterial(blurShader);
            pass = new BlurPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType == CameraType.Preview) return;
            var target = renderingData.cameraData.camera.targetTexture;
            if (target != null && target.graphicsFormat == GraphicsFormat.None) return;
            pass.material = material;
            pass.layers = layerMask;
            pass.horizontalLength = Mathf.Clamp(horizontalLength, 0f, 256f);
            pass.verticalSoftness = Mathf.Clamp(verticalSoftness, 0f, 16f);
            pass.originalWeight = Mathf.Clamp01(originalWeight);
            pass.outerHorizontalLength = Mathf.Clamp(outerHorizontalLength, 0f, 256f);
            pass.outerOriginalWeight = Mathf.Clamp01(outerOriginalWeight);
            pass.bandJitter = Mathf.Clamp(bandJitter, 0f, 0.5f);
            pass.bandCount = Mathf.Clamp(bandCount, 4, 128);
            pass.driftSpeed = Mathf.Clamp(driftSpeed, 0f, 2f);
            pass.downsample = Mathf.Clamp(downsample, 1, 4);
            pass.opacity = opacity;
            pass.depthBias = depthBias;
            pass.gaussianRadius = Mathf.Clamp(gaussianRadius,0,32);
            pass.gaussianStrength = Mathf.Clamp01(gaussianStrength);
            pass.gaussianResponse = new Vector4(gaussianDistanceResponse ? 1 : 0, Mathf.Max(.01f,gaussianResponseDistance), Mathf.Clamp01(gaussianNearStrength), Mathf.Clamp01(gaussianFarStrength));
            pass.cylindrical = cylinderTrails;
            pass.cylinderDistances = cylinderDistances;
            pass.cylinderRadii = cylinderRadii;
            pass.cylinderLengths = cylinderLengths;
            pass.cylinderSign = cylinderTowardPlayer ? 1 : -1;
            pass.cylinderSamples = Mathf.Clamp(cylinderSamples, 9, 65) | 1;
            pass.cylinderResolution = Mathf.Clamp(cylinderResolution, 64, 512);
            var reference = Camera.main;
            pass.playerPosition = (reference != null ? reference : renderingData.cameraData.camera).transform.position;
            pass.groupId = 0;
            pass.upward = new Vector4(upwardResponse ? 1 : 0,upwardThreshold,Mathf.Max(.01f,upwardSoftness),upwardTwoSided ? 1 : 0);
            pass.upwardWeights = new Vector4(Mathf.Clamp(upwardLength,0,3),Mathf.Clamp(otherLength,0,3),Mathf.Clamp01(upwardContribution),Mathf.Clamp01(otherContribution));
            pass.upwardPreview=upwardResponse && upwardPreview;
            var owners = GlitchBlurOverride.Active.ToArray();
            var masks = new List<GlitchBlurOverride.MaskDraw>();
            if(upwardResponse)
                GlitchBlurOverride.CollectRenderers(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None),null,globalNormalMasks,masks,objectIdShader,0,renderingData.cameraData.camera,layerMask);
            for (int i=0; i<owners.Length; i++)
                if (owners[i] && owners[i].isActiveAndEnabled)
                    owners[i].Collect(masks, objectIdShader, i+1, renderingData.cameraData.camera, layerMask);
            pass.maskDraws = masks.ToArray();
            renderer.EnqueuePass(pass);
            if (masks.Count == 0) return;
            for (int i=0; i<owners.Length; i++)
            {
                var owner=owners[i];
                if (!owner || !owner.isActiveAndEnabled || owner.opacity <= 0) continue;
                int ownerId = i + 1;
                if (!masks.Exists(draw => draw.objectId == ownerId)) continue;
                while(localPasses.Count<=i)
                {
                    var item=new BlurPass {renderPassEvent=RenderPassEvent.BeforeRenderingTransparents};
                    item.ConfigureInput(ScriptableRenderPassInput.Depth);
                    localPasses.Add(item);
                }
                var local=localPasses[i];
                local.material=material; local.layers=layerMask;
                local.groupId=i+1; local.maskDraws=pass.maskDraws;
                local.playerPosition=pass.playerPosition;
                local.upward=new Vector4(owner.upwardResponse ? 1 : 0,owner.upwardThreshold,Mathf.Max(.01f,owner.upwardSoftness),owner.upwardTwoSided ? 1 : 0);
                local.upwardWeights=new Vector4(Mathf.Clamp(owner.upwardLength,0,3),Mathf.Clamp(owner.otherLength,0,3),Mathf.Clamp01(owner.upwardContribution),Mathf.Clamp01(owner.otherContribution));
                local.upwardPreview=owner.upwardResponse && owner.upwardPreview;
                local.cylindrical=owner.cylinderTrails;
                local.cylinderSign=owner.cylinderTowardPlayer ? 1 : -1;
                local.cylinderDistances = owner.cylinderDistances;
                local.cylinderRadii = owner.cylinderRadii;
                local.cylinderLengths = owner.cylinderLengths;
                local.cylinderSamples = owner.cylinderSamples;
                local.cylinderResolution = owner.cylinderResolution;
                local.horizontalLength = owner.horizontalLength;
                local.outerHorizontalLength = owner.outerHorizontalLength;
                local.originalWeight = owner.originalWeight;
                local.outerOriginalWeight = owner.outerOriginalWeight;
                local.verticalSoftness = owner.verticalSoftness;
                local.bandJitter = owner.bandJitter;
                local.bandCount = owner.bandCount;
                local.driftSpeed = owner.driftSpeed;
                local.downsample = owner.downsample;
                local.opacity = owner.opacity;
                local.depthBias = owner.depthBias;
                local.gaussianRadius = Mathf.Clamp(owner.gaussianRadius,0,32);
                local.gaussianStrength = Mathf.Clamp01(owner.gaussianStrength);
                local.gaussianResponse = new Vector4(owner.gaussianDistanceResponse ? 1 : 0, Mathf.Max(.01f,owner.gaussianResponseDistance), Mathf.Clamp01(owner.gaussianNearStrength), Mathf.Clamp01(owner.gaussianFarStrength));
                local.downsample=Mathf.Clamp(local.downsample,1,4);
                local.cylinderSamples=Mathf.Clamp(local.cylinderSamples,9,65)|1;
                local.cylinderResolution=Mathf.Clamp(local.cylinderResolution,64,512);
                renderer.EnqueuePass(local);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            foreach(var m in globalNormalMasks.Values)CoreUtils.Destroy(m);
            globalNormalMasks.Clear();
        }

        sealed class BlurPass : ScriptableRenderPass
        {
            internal Material material;
            internal int groupId;
            internal GlitchBlurOverride.MaskDraw[] maskDraws;
            internal int layers;
            internal float gaussianRadius, gaussianStrength;
            internal Vector4 gaussianResponse;
            internal Vector4 upward, upwardWeights;
            internal bool upwardPreview;
            internal float horizontalLength, verticalSoftness, originalWeight, bandJitter, driftSpeed, opacity, depthBias;
            internal float outerHorizontalLength, outerOriginalWeight;
            internal int bandCount, downsample;
            internal bool cylindrical;
            internal Vector2 cylinderDistances, cylinderRadii, cylinderLengths;
            internal float cylinderSign;
            internal int cylinderSamples, cylinderResolution;
            internal Vector3 playerPosition;
            static readonly List<ShaderTagId> Tags = new()
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };
            static readonly int LayerDepth = Shader.PropertyToID("_GlitchBlurLayerDepth");
            static readonly int HorizontalDepth = Shader.PropertyToID("_GlitchBlurHorizontalDepth");
            static readonly int SceneDepth = Shader.PropertyToID("_GlitchBlurSceneDepth");
            static readonly int Parameters = Shader.PropertyToID("_GlitchBlurParameters");
            static readonly int Shape = Shader.PropertyToID("_GlitchBlurShape");
            static readonly int Bands = Shader.PropertyToID("_GlitchBlurBands");
            static readonly int Outer = Shader.PropertyToID("_GlitchBlurOuter");
            static readonly int Texel = Shader.PropertyToID("_GlitchBlurTexel");
            static readonly int CylinderSource = Shader.PropertyToID("_GlitchBlurCylinderSource");
            static readonly int ObjectIds = Shader.PropertyToID("_GlitchBlurObjectIds");
            static readonly int GaussianOriginal = Shader.PropertyToID("_GlitchGaussianOriginal");
            static readonly int SurfaceNormals = Shader.PropertyToID("_GlitchSurfaceNormals");

            sealed class DrawData { public RendererListHandle list, transparentList; }
            sealed class MaskData { public GlitchBlurOverride.MaskDraw[] draws; }
            sealed class FilterData
            {
                public TextureHandle source;
                public Material material;
                public int shaderPass;
                public Vector4 group;
                public Vector4 gaussian;
                public Vector4 gaussianResponse;
                public Vector4 upward, upwardWeights;
                public Vector4 parameters, texel, shape, bands, outer;
                public Vector4 cylinderRange, cylinderShape, cylinderGrid, player;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();
                var desc = camera.cameraTargetDescriptor;
                desc.width = Mathf.Max(1, desc.width / downsample);
                desc.height = Mathf.Max(1, desc.height / downsample);
                if (cylindrical && desc.width > cylinderResolution)
                {
                    desc.height = Mathf.Max(1, Mathf.RoundToInt(desc.height * (float)cylinderResolution / desc.width));
                    desc.width = cylinderResolution;
                }
                // Preserve XR dimension, volumeDepth, dynamic resolution and VR usage.
                desc.msaaSamples = 1;
                desc.bindMS = false;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                var colorDesc = new TextureDesc(desc) { name = "GlitchBlur Layer Color", clearBuffer = true, clearColor = Color.clear };
                var color = graph.CreateTexture(colorDesc);
                var horizontal = graph.CreateTexture(new TextureDesc(desc) { name = "GlitchBlur Horizontal", clearBuffer = true, clearColor = Color.clear });
                desc.graphicsFormat = GraphicsFormat.R32_SFloat;
                var horizontalDepth = graph.CreateTexture(new TextureDesc(desc) { name = "GlitchBlur Horizontal Depth", clearBuffer = true, clearColor = new Color(camera.camera.farClipPlane,0,0,0) });
                desc.graphicsFormat = GraphicsFormat.None;
                desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
                var depth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "GlitchBlur Layer Depth", true);

                var drawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
                var filtering = new FilteringSettings(RenderQueueRange.opaque, layers);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                var transparentDrawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, lights, SortingCriteria.CommonTransparent);
                var transparentFiltering = new FilteringSettings(RenderQueueRange.transparent, layers);
                // Capture a representative front depth for the existing depth-aware blur.
                // Keep each shader's blending, including separate RGB/alpha blend factors.
                var tags = new NativeArray<ShaderTagId>(1, Allocator.Temp);
                tags[0] = ShaderTagId.none;
                var states = new NativeArray<RenderStateBlock>(1, Allocator.Temp);
                states[0] = new RenderStateBlock(RenderStateMask.Depth)
                {
                    depthState = new DepthState(true, CompareFunction.LessEqual)
                };
                var transparentList = graph.CreateRendererList(new RendererListParams(rendering.cullResults, transparentDrawing, transparentFiltering)
                {
                    tagValues = tags,
                    stateBlocks = states,
                    isPassTagName = false
                });
                using (var builder = graph.AddRasterRenderPass<DrawData>("GlitchBlur: Original Materials", out var data))
                {
                    data.list = list;
                    data.transparentList = transparentList;
                    builder.UseRendererList(list);
                    builder.UseRendererList(transparentList);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(depth, LayerDepth);
                    builder.SetGlobalTextureAfterPass(color, CylinderSource);
                    builder.SetGlobalTextureAfterPass(resources.cameraDepthTexture, SceneDepth);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.list);
                        ctx.cmd.DrawRendererList(d.transparentList);
                    });
                }

                var idDesc = new TextureDesc(desc) {name="GlitchBlur Object IDs",clearBuffer=true,clearColor=Color.clear};
                idDesc.depthBufferBits=DepthBits.None;
                idDesc.colorFormat=GraphicsFormat.R32_SFloat;
                var ids=graph.CreateTexture(idDesc);
                idDesc.colorFormat=GraphicsFormat.R16G16B16A16_SFloat;
                idDesc.name="GlitchBlur Surface Normals";
                var normals=graph.CreateTexture(idDesc);
                using(var builder=graph.AddRasterRenderPass<MaskData>("GlitchBlur: Object Ownership",out var data))
                {
                    data.draws=maskDraws;
                    builder.SetRenderAttachment(ids,0,AccessFlags.Write);
                    builder.SetRenderAttachment(normals,1,AccessFlags.Write);
                    builder.UseTexture(depth,AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(ids,ObjectIds);
                    builder.SetGlobalTextureAfterPass(normals,SurfaceNormals);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((MaskData d,RasterGraphContext ctx)=>
                    {
                        if(d.draws==null) return;
                        foreach(var draw in d.draws)
                        {
                            // Bind ownership per draw, independently of cached mask materials.
                            ctx.cmd.SetGlobalFloat("_GlitchMaskObjectId",draw.objectId);
                            if(draw.renderer && draw.material)
                                ctx.cmd.DrawRenderer(draw.renderer,draw.material,draw.submesh,0);
                            else if(draw.mesh && draw.material)
                                ctx.cmd.DrawMesh(draw.mesh,draw.matrix,draw.material,draw.submesh,0);
                        }
                    });
                }

                using (var builder = graph.AddRasterRenderPass<FilterData>("GlitchBlur: Horizontal Color Smear", out var data))
                {
                    Fill(data, color, cylindrical && !upwardPreview ? 2 : 0, camera.cameraTargetDescriptor);
                    if (cylindrical) data.texel = new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height);
                    builder.UseTexture(color);
                    builder.UseTexture(ids);
                    builder.UseTexture(normals);
                    builder.UseTexture(depth);
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.SetRenderAttachment(horizontal, 0, AccessFlags.Write);
                    builder.SetRenderAttachment(horizontalDepth, 1, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(horizontalDepth, HorizontalDepth);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((FilterData d, RasterGraphContext ctx) => ExecuteFilter(d, ctx));
                }
                if (!upwardPreview && gaussianRadius > 0 && (gaussianResponse.x > .5f ? Mathf.Max(gaussianResponse.z,gaussianResponse.w) > 0 : gaussianStrength > 0))
                {
                    var original = horizontal;
                    for (int axis = 0; axis < 2; axis++)
                    {
                        var blurDesc = new TextureDesc(colorDesc) { name = "GlitchBlur Gaussian Color" };
                        var filtered = graph.CreateTexture(blurDesc);
                        blurDesc.colorFormat = GraphicsFormat.R32_SFloat;
                        blurDesc.name = "GlitchBlur Gaussian Depth";
                        var filteredDepth = graph.CreateTexture(blurDesc);
                        using (var builder = graph.AddRasterRenderPass<FilterData>(axis == 0 ? "GlitchBlur: Gaussian X" : "GlitchBlur: Gaussian Y", out var data))
                        {
                            Fill(data, horizontal, 3 + axis, camera.cameraTargetDescriptor);
                            data.texel = new Vector4(1f/desc.width,1f/desc.height,desc.width,desc.height);
                            data.gaussian = new Vector4(gaussianRadius * desc.width / camera.cameraTargetDescriptor.width,
                                gaussianRadius * desc.height / camera.cameraTargetDescriptor.height, gaussianStrength, axis);
                            builder.UseTexture(horizontal);
                            builder.UseTexture(horizontalDepth);
                            builder.UseTexture(original);
                            builder.UseTexture(resources.cameraDepthTexture);
                            builder.SetRenderAttachment(filtered,0,AccessFlags.Write);
                            builder.SetRenderAttachment(filteredDepth,1,AccessFlags.Write);
                            builder.SetGlobalTextureAfterPass(filteredDepth,HorizontalDepth);
                            if (axis == 0) builder.SetGlobalTextureAfterPass(original,GaussianOriginal);
                            builder.AllowGlobalStateModification(true);
                            builder.SetRenderFunc((FilterData d,RasterGraphContext ctx)=>ExecuteFilter(d,ctx));
                        }
                        horizontal = filtered;
                        horizontalDepth = filteredDepth;
                    }
                }
                using (var builder = graph.AddRasterRenderPass<FilterData>("GlitchBlur: Softness + Composite", out var data))
                {
                    Fill(data, horizontal, 1, camera.cameraTargetDescriptor);
                    if (cylindrical) data.texel = new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height);
                    builder.UseTexture(horizontal);
                    builder.UseTexture(horizontalDepth);
                    builder.UseTexture(resources.cameraDepthTexture);
                    // Premultiplied blending preserves the existing camera color, including MSAA.
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((FilterData d, RasterGraphContext ctx) => ExecuteFilter(d, ctx));
                }
            }

            void Fill(FilterData data, TextureHandle source, int shaderPass, RenderTextureDescriptor desc)
            {
                data.source = source;
                data.material = material;
                data.shaderPass = shaderPass;
                data.group = new Vector4(groupId,maskDraws != null && maskDraws.Length>0 ? 1 : 0,0,0);
                data.parameters = new Vector4(horizontalLength / downsample, opacity, depthBias, verticalSoftness / downsample);
                int width = Mathf.Max(1, desc.width / downsample), height = Mathf.Max(1, desc.height / downsample);
                data.texel = new Vector4(1f / width, 1f / height, width, height);
                data.shape = new Vector4(originalWeight, bandJitter, bandCount, driftSpeed);
                data.bands = new Vector4(Time.realtimeSinceStartup, 0, 0, 0);
                data.outer = new Vector4(outerHorizontalLength / downsample, outerOriginalWeight, 0, 0);
                data.cylinderRange = new Vector4(Mathf.Max(0,cylinderDistances.x), Mathf.Max(cylinderDistances.x+.01f,cylinderDistances.y), Mathf.Max(.05f,cylinderRadii.x), Mathf.Max(.05f,cylinderRadii.y));
                data.cylinderShape = new Vector4(Mathf.Max(0,cylinderLengths.x), Mathf.Max(0,cylinderLengths.y), cylinderSign, 0);
                data.cylinderGrid = new Vector4(0,0,cylinderSamples,0);
                data.player = new Vector4(playerPosition.x,playerPosition.y,playerPosition.z,0);
                data.gaussianResponse = gaussianResponse;
                data.upward=upward;
                data.upwardWeights=upwardWeights;
                if(upwardPreview){ data.upward.x=2;data.parameters.y=1;data.parameters.w=0; }
            }

            static void ExecuteFilter(FilterData data, RasterGraphContext context)
            {
                context.cmd.SetGlobalVector(Parameters, data.parameters);
                context.cmd.SetGlobalVector(Texel, data.texel);
                context.cmd.SetGlobalVector(Shape, data.shape);
                context.cmd.SetGlobalVector(Bands, data.bands);
                context.cmd.SetGlobalVector(Outer, data.outer);
                context.cmd.SetGlobalVector("_GlitchBlurGroup", data.group);
                context.cmd.SetGlobalVector("_GlitchGaussian", data.gaussian);
                context.cmd.SetGlobalVector("_GlitchUpward", data.upward);
                context.cmd.SetGlobalVector("_GlitchUpwardWeights", data.upwardWeights);
                context.cmd.SetGlobalVector("_GlitchGaussianResponse", data.gaussianResponse);
                context.cmd.SetGlobalVector("_GlitchCylinderPlayer", data.player);
                if (data.shaderPass == 2)
                {
                    float normalization = data.shape.x;
                    int taps = (int)data.cylinderGrid.z;
                    for (int i = 0; i < taps; i++)
                    {
                        float t = Mathf.Abs(2f * i / (taps - 1) - 1);
                        normalization += (1 - Mathf.Lerp(data.shape.x, data.outer.y, t*t*(3-2*t))) / taps;
                    }
                    context.cmd.SetGlobalVector("_GlitchCylinderRange", data.cylinderRange);
                    context.cmd.SetGlobalVector("_GlitchCylinderShape", data.cylinderShape);
                    context.cmd.SetGlobalVector("_GlitchCylinderGrid", new Vector4(data.texel.z,data.texel.w,data.cylinderGrid.z,Mathf.Max(1,normalization)));
                    context.cmd.SetGlobalVector("_GlitchCylinderPlayer", data.player);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 2, MeshTopology.Triangles,
                        (int)(data.texel.z * data.texel.w * 6 * data.cylinderGrid.z), 1);
                    return;
                }
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.shaderPass);
            }
        }
    }
}
