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
    public sealed class ScreenBlurRendererFeature : ScriptableRendererFeature
    {
        public LayerMask layerMask = 1 << 7;
        [Range(0, 100)] public float radius = 4;
        [Range(0, 1)] public float opacity = 0.85f;
        [Min(0)] public float depthBias = 0.005f;
        [SerializeField] Shader blurShader;
        [SerializeField] Shader objectIdShader;
        Material material;
        BlurPass pass;
        readonly List<BlurPass> localPasses = new();

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (blurShader != null) material = CoreUtils.CreateEngineMaterial(blurShader);
            if (objectIdShader == null) objectIdShader = Shader.Find("Hidden/TRINL/ScreenBlurObjectId");
            localPasses.Clear();
            pass = CreatePass();
        }

        static BlurPass CreatePass()
        {
            var created = new BlurPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            created.ConfigureInput(ScriptableRenderPassInput.Depth);
            return created;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType == CameraType.Preview) return;
            var target = renderingData.cameraData.camera.targetTexture;
            if (target != null && target.graphicsFormat == GraphicsFormat.None) return;

            var camera = renderingData.cameraData.camera;
            // Group 0 is the global settings; each active override becomes group i+1.
            var owners = ScreenBlurOverride.Active.ToArray();
            var masks = new List<ScreenBlurOverride.MaskDraw>();
            for (int i = 0; i < owners.Length; i++)
                if (owners[i] && owners[i].isActiveAndEnabled)
                    owners[i].Collect(masks, objectIdShader, i + 1, camera, layerMask);

            pass.material = material;
            pass.layers = layerMask;
            pass.radius = Mathf.Clamp(radius, 0f, 100f);
            pass.opacity = Mathf.Clamp01(opacity);
            pass.depthBias = Mathf.Max(0f, depthBias);
            pass.groupId = 0;
            pass.maskDraws = masks.Count > 0 ? masks.ToArray() : null;
            renderer.EnqueuePass(pass);

            if (masks.Count == 0) return;
            for (int i = 0; i < owners.Length; i++)
            {
                var owner = owners[i];
                if (!owner || !owner.isActiveAndEnabled || owner.opacity <= 0) continue;
                int ownerId = i + 1;
                if (!masks.Exists(draw => draw.objectId == ownerId)) continue;
                while (localPasses.Count <= i) localPasses.Add(CreatePass());
                var local = localPasses[i];
                local.material = material;
                local.layers = layerMask;
                local.radius = Mathf.Clamp(owner.radius, 0f, 100f);
                local.opacity = Mathf.Clamp01(owner.opacity);
                local.depthBias = Mathf.Max(0f, owner.depthBias);
                local.groupId = ownerId;
                local.maskDraws = pass.maskDraws;
                renderer.EnqueuePass(local);
            }
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        sealed class BlurPass : ScriptableRenderPass
        {
            internal Material material;
            internal int layers;
            internal float radius, opacity, depthBias;
            internal int groupId;
            internal ScreenBlurOverride.MaskDraw[] maskDraws;
            static readonly List<ShaderTagId> Tags = new()
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };
            static readonly int LayerDepth = Shader.PropertyToID("_ScreenBlurLayerDepth");
            static readonly int HorizontalDepth = Shader.PropertyToID("_ScreenBlurHorizontalDepth");
            static readonly int SceneDepth = Shader.PropertyToID("_ScreenBlurSceneDepth");
            static readonly int Parameters = Shader.PropertyToID("_ScreenBlurParameters");
            static readonly int Texel = Shader.PropertyToID("_ScreenBlurTexel");
            static readonly int ObjectIds = Shader.PropertyToID("_ScreenBlurObjectIds");
            static readonly int Group = Shader.PropertyToID("_ScreenBlurGroup");
            static readonly int MaskObjectId = Shader.PropertyToID("_ScreenBlurMaskObjectId");

            sealed class DrawData { public RendererListHandle list, transparentList; }
            sealed class MaskData { public ScreenBlurOverride.MaskDraw[] draws; }
            sealed class FilterData
            {
                public TextureHandle source;
                public Material material;
                public int shaderPass;
                public Vector4 parameters, texel, group;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();
                var desc = camera.cameraTargetDescriptor;
                // Preserve XR dimension, volumeDepth, dynamic resolution and VR usage.
                desc.msaaSamples = 1;
                desc.bindMS = false;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                var colorDesc = new TextureDesc(desc) { name = "ScreenBlur Layer Color", clearBuffer = true, clearColor = Color.clear };
                var color = graph.CreateTexture(colorDesc);
                var horizontal = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenBlur Horizontal", false);
                desc.graphicsFormat = GraphicsFormat.R32_SFloat;
                var horizontalDepth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenBlur Horizontal Depth", false);
                desc.graphicsFormat = GraphicsFormat.None;
                desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
                var depth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenBlur Layer Depth", true);

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
                using (var builder = graph.AddRasterRenderPass<DrawData>("ScreenBlur: Original Materials", out var data))
                {
                    data.list = list;
                    data.transparentList = transparentList;
                    builder.UseRendererList(list);
                    builder.UseRendererList(transparentList);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(depth, LayerDepth);
                    builder.SetGlobalTextureAfterPass(resources.cameraDepthTexture, SceneDepth);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.list);
                        ctx.cmd.DrawRendererList(d.transparentList);
                    });
                }

                // Per-object ownership. The buffer clears to 0, so any renderer without an
                // override stays in the global group and keeps the feature's own parameters.
                if (maskDraws != null && maskDraws.Length > 0)
                {
                    desc.depthStencilFormat = GraphicsFormat.None;
                    desc.graphicsFormat = GraphicsFormat.R16_SFloat;
                    var ids = graph.CreateTexture(new TextureDesc(desc)
                    { name = "ScreenBlur Object Ids", clearBuffer = true, clearColor = Color.clear });
                    using (var builder = graph.AddRasterRenderPass<MaskData>("ScreenBlur: Object Ownership", out var data))
                    {
                        data.draws = maskDraws;
                        builder.SetRenderAttachment(ids, 0, AccessFlags.Write);
                        builder.UseTexture(depth, AccessFlags.Read);
                        builder.SetGlobalTextureAfterPass(ids, ObjectIds);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((MaskData d, RasterGraphContext ctx) =>
                        {
                            if (d.draws == null) return;
                            foreach (var draw in d.draws)
                            {
                                // Bind ownership per draw, independently of cached mask materials.
                                ctx.cmd.SetGlobalFloat(MaskObjectId, draw.objectId);
                                if (draw.renderer && draw.material)
                                    ctx.cmd.DrawRenderer(draw.renderer, draw.material, draw.submesh, 0);
                            }
                        });
                    }
                }

                using (var builder = graph.AddRasterRenderPass<FilterData>("ScreenBlur: Horizontal Gaussian", out var data))
                {
                    Fill(data, color, 0, camera.cameraTargetDescriptor);
                    builder.UseTexture(color);
                    builder.UseTexture(depth);
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.SetRenderAttachment(horizontal, 0, AccessFlags.Write);
                    builder.SetRenderAttachment(horizontalDepth, 1, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(horizontalDepth, HorizontalDepth);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((FilterData d, RasterGraphContext ctx) => ExecuteFilter(d, ctx));
                }
                using (var builder = graph.AddRasterRenderPass<FilterData>("ScreenBlur: Vertical Gaussian + Composite", out var data))
                {
                    Fill(data, horizontal, 1, camera.cameraTargetDescriptor);
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
                data.parameters = new Vector4(radius, opacity, depthBias, 0);
                data.texel = new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height);
                data.group = new Vector4(groupId, maskDraws != null && maskDraws.Length > 0 ? 1 : 0, 0, 0);
            }

            static void ExecuteFilter(FilterData data, RasterGraphContext context)
            {
                context.cmd.SetGlobalVector(Parameters, data.parameters);
                context.cmd.SetGlobalVector(Texel, data.texel);
                context.cmd.SetGlobalVector(Group, data.group);
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.shaderPass);
            }
        }
    }
}
