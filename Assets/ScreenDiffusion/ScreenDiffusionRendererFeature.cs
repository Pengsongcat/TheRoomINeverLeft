using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering
{
    // Dual filtering (Kawase) softening. The radius comes from the level count instead
    // of the tap count, so a very wide blur costs about the same as a narrow one.
    //
    // Full Screen mode blurs the finished camera colour.
    // Layer mode draws only the chosen layers into their own transparent target, blurs
    // that, and composites it back, leaving every other object untouched. In Layer mode
    // the renderer's Opaque and Transparent Layer Masks must EXCLUDE those layers, and
    // the camera culling mask must include them.
    public sealed class ScreenDiffusionRendererFeature : ScriptableRendererFeature
    {
        public enum Target { FullScreen = 0, Layer = 1 }
        public enum BlendMode { Mix = 0, Glow = 1, Screen = 2, Max = 3 }

        [Tooltip("Full Screen 柔化整个画面；Layer 只柔化指定 Layer 上的物体。")]
        public Target target = Target.FullScreen;
        [Tooltip("Layer 模式使用的层。这些层必须从 Renderer 的 Opaque / Transparent Layer Mask 中排除。")]
        public LayerMask layerMask = 1 << 9;
        [Tooltip("Layer 模式遮挡层；仅这些层可以遮挡模糊。Nothing 保留原来的最前叠加。")]
        public LayerMask occluderLayerMask;
        [Min(0), Tooltip("遮挡深度容差，单位为米。")]
        public float occlusionDepthBias = 0.005f;
        [Tooltip("金字塔级数。每多一级有效半径翻倍，开销只增加上一级的四分之一。")]
        [Range(1, 20)] public int iterations = 4;
        [Tooltip("每级的采样偏移倍率。用来在整数级数之间做微调。")]
        [Range(0.5f, 4f)] public float spread = 1f;
        [Tooltip("Full Screen 模式下是与原图的混合强度；Layer 模式下是合成不透明度。")]
        [Range(0, 1)] public float strength = 1f;
        [Tooltip("Layer 模式下 Mix = 正常叠加，Glow = 相加发光，Max = 逐通道取最大值（不往背景上加），Screen 回退为正常叠加。")]
        public BlendMode blendMode = BlendMode.Mix;
        [Tooltip("模糊时取逐通道最大值而不是加权平均。保住各自的峰值颜色，但扩散没有柔和的衰减，边缘会更硬。")]
        public bool maxPyramid;
        [Tooltip("默认在后处理之前，这样已有的 Bloom / 色调映射仍然作用在柔化之后的画面上。")]
        public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
        [SerializeField] Shader diffusionShader;
        [SerializeField] Shader objectIdShader;
        readonly List<DiffusionPass> localPasses = new();
        Material material;
        DiffusionPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (diffusionShader == null) diffusionShader = Shader.Find("Hidden/TRINL/ScreenDiffusion");
            if (diffusionShader != null) material = CoreUtils.CreateEngineMaterial(diffusionShader);
            pass = new DiffusionPass();
            localPasses.Clear();
            if (!objectIdShader) objectIdShader = Shader.Find("Hidden/TRINL/ScreenDiffusionObjectId");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType == CameraType.Preview) return;
            if (target == Target.FullScreen && strength <= 0f) return;
            if (target == Target.Layer && layerMask.value == 0) return;
            var rt = renderingData.cameraData.camera.targetTexture;
            if (rt != null && rt.graphicsFormat == GraphicsFormat.None) return;
            pass.renderPassEvent = injectionPoint;
            pass.material = material;
            pass.targetMode = target;
            pass.layers = layerMask;
            pass.occluderLayers = occluderLayerMask.value & ~layerMask.value;
            pass.occlusionDepthBias = Mathf.Max(0, occlusionDepthBias);
            pass.iterations = Mathf.Clamp(iterations, 1, 20);
            pass.spread = Mathf.Clamp(spread, 0.5f, 4f);
            pass.strength = Mathf.Clamp01(strength);
            pass.blendMode = (int)blendMode;
            pass.maxPyramid = maxPyramid;
            pass.groupId = 0;
            pass.maskDraws = null;
            var owners = target == Target.Layer ? GlobleBlurOverride.Active.ToArray() : new GlobleBlurOverride[0];
            var masks = new List<GlobleBlurOverride.MaskDraw>();
            for (int i = 0; i < owners.Length; i++)
                if (owners[i] && owners[i].isActiveAndEnabled)
                    owners[i].Collect(masks, objectIdShader, i + 1, renderingData.cameraData.camera, layerMask);
            if (masks.Count > 0) pass.maskDraws = masks.ToArray();
            if (strength > 0f) renderer.EnqueuePass(pass);
            for (int i = 0; i < owners.Length; i++)
            {
                var owner = owners[i];
                int id = i + 1;
                if (!owner || !owner.isActiveAndEnabled || owner.strength <= 0 || !masks.Exists(m => m.objectId == id)) continue;
                while (localPasses.Count <= i) localPasses.Add(new DiffusionPass());
                var local = localPasses[i];
                local.renderPassEvent = injectionPoint;
                local.material = material;
                local.targetMode = Target.Layer;
                local.layers = layerMask;
                local.occluderLayers = pass.occluderLayers;
                local.occlusionDepthBias = Mathf.Max(0, owner.occlusionDepthBias);
                local.iterations = Mathf.Clamp(owner.iterations, 1, 20);
                local.spread = Mathf.Clamp(owner.spread, 0.5f, 4f);
                local.strength = Mathf.Clamp01(owner.strength);
                local.blendMode = (int)owner.blendMode;
                local.maxPyramid = owner.maxPyramid;
                local.groupId = id;
                local.maskDraws = pass.maskDraws;
                renderer.EnqueuePass(local);
            }
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        sealed class DiffusionPass : ScriptableRenderPass
        {
            internal Material material;
            internal int groupId;
            internal GlobleBlurOverride.MaskDraw[] maskDraws;
            static readonly int ObjectIds = Shader.PropertyToID("_ScreenDiffusionObjectIds");
            static readonly int MaskObjectId = Shader.PropertyToID("_ScreenDiffusionMaskObjectId");
            static readonly int GroupId = Shader.PropertyToID("_ScreenDiffusionGroup");
            sealed class MaskData { public GlobleBlurOverride.MaskDraw[] draws; }
            internal Target targetMode;
            internal int layers, iterations, blendMode, occluderLayers;
            internal float occlusionDepthBias;
            internal bool maxPyramid;
            internal float spread, strength;

            const int PassCopy = 0, PassDown = 1, PassUp = 2, PassComposite = 3;
            const int PassCompositeLayer = 4, PassCompositeLayerAdditive = 5;
            const int PassDownMax = 6, PassUpMax = 7, PassCompositeLayerMax = 8;
            const int PassOcclusion = 9;
            const int MinSize = 8;
            static readonly int LayerDepthTex = Shader.PropertyToID("_ScreenDiffusionLayerDepth");
            static readonly int OccluderDepthTex = Shader.PropertyToID("_ScreenDiffusionOccluderDepth");
            static readonly int OcclusionId = Shader.PropertyToID("_ScreenDiffusionOcclusion");
            static readonly int OriginalTex = Shader.PropertyToID("_ScreenDiffusionOriginal");
            static readonly int TexelId = Shader.PropertyToID("_ScreenDiffusionTexel");
            static readonly int ParamsId = Shader.PropertyToID("_ScreenDiffusionParams");
            static readonly List<ShaderTagId> Tags = new()
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            sealed class BlitData
            {
                public TextureHandle source;
                public Material material;
                public int shaderPass;
                public Vector4 texel, parameters, occlusion, group;
            }
            sealed class DepthData { public RendererListHandle list; }
            sealed class DrawData { public RendererListHandle list, transparentList; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>();

                var desc = camera.cameraTargetDescriptor;
                // Preserve XR dimension, volumeDepth, dynamic resolution and VR usage.
                desc.msaaSamples = 1;
                desc.bindMS = false;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                int fullWidth = desc.width, fullHeight = desc.height;

                TextureHandle layerDepth = default, occluderDepth = default;
                TextureHandle original = default;
                TextureHandle source;

                if (targetMode == Target.Layer)
                {
                    source = CaptureLayer(graph, frameData, desc, fullWidth, fullHeight, out layerDepth);
                    TextureHandle ids = default;
                    if (maskDraws != null)
                    {
                        var idDesc = desc;
                        idDesc.graphicsFormat = GraphicsFormat.R32_SFloat;
                        ids = graph.CreateTexture(new TextureDesc(idDesc) { name = "ScreenDiffusion Object IDs", clearBuffer = true, clearColor = Color.clear });
                        using (var builder = graph.AddRasterRenderPass<MaskData>("ScreenDiffusion: Object Ownership", out var data))
                        {
                            data.draws = maskDraws;
                            builder.SetRenderAttachmentDepth(layerDepth, AccessFlags.Read);
                            builder.SetRenderAttachment(ids, 0, AccessFlags.Write);
                            builder.SetGlobalTextureAfterPass(ids, ObjectIds);
                            builder.AllowGlobalStateModification(true);
                            builder.SetRenderFunc((MaskData d, RasterGraphContext ctx) =>
                            {
                                foreach (var draw in d.draws)
                                {
                                    ctx.cmd.SetGlobalFloat(MaskObjectId, draw.objectId);
                                    if (draw.renderer && draw.material) ctx.cmd.DrawRenderer(draw.renderer, draw.material, draw.submesh, 0);
                                }
                            });
                        }
                    }
                    if (occluderLayers != 0) occluderDepth = CaptureOccluders(graph, frameData, desc);
                    if (occluderLayers != 0 || ids.IsValid())
                    {
                        var visible = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenDiffusion Visible Layer", false);
                        using (var builder = graph.AddRasterRenderPass<BlitData>("ScreenDiffusion: Reject Hidden Sources", out var data))
                        {
                            Fill(data, source, PassOcclusion, fullWidth, fullHeight);
                            builder.UseTexture(source);
                            builder.UseTexture(layerDepth);
                            if (occluderDepth.IsValid()) builder.UseTexture(occluderDepth);
                            if (ids.IsValid()) builder.UseTexture(ids);
                            builder.SetRenderAttachment(visible, 0, AccessFlags.Write);
                            builder.AllowGlobalStateModification(true);
                            builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) => Execute(d, ctx));
                        }
                        source = visible;
                    }
                    if (!source.IsValid()) return;
                }
                else
                {
                    // The composite has to read the camera colour while writing it, which a
                    // single pass cannot do, so keep one full resolution copy.
                    original = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenDiffusion Original", false);
                    using (var builder = graph.AddRasterRenderPass<BlitData>("ScreenDiffusion: Copy", out var data))
                    {
                        Fill(data, resources.activeColorTexture, PassCopy, fullWidth, fullHeight);
                        builder.UseTexture(resources.activeColorTexture);
                        builder.SetRenderAttachment(original, 0, AccessFlags.Write);
                        builder.SetGlobalTextureAfterPass(original, OriginalTex);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) => Execute(d, ctx));
                    }
                    source = original;
                }

                int levels = Mathf.Clamp(iterations, 1, 20);
                var sizes = new Vector2Int[levels];
                int width = fullWidth, height = fullHeight;
                for (int i = 0; i < levels; i++)
                {
                    int nextWidth = Mathf.Max(MinSize, width >> 1);
                    int nextHeight = Mathf.Max(MinSize, height >> 1);
                    // Stop adding levels once halving no longer buys anything.
                    if (nextWidth == width && nextHeight == height) { levels = i; break; }
                    width = nextWidth; height = nextHeight;
                    desc.width = width; desc.height = height;
                    sizes[i] = new Vector2Int(width, height);
                    var downTexture = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenDiffusion Down " + i, false);
                    using (var builder = graph.AddRasterRenderPass<BlitData>("ScreenDiffusion: Downsample " + i, out var data))
                    {
                        Fill(data, source, maxPyramid ? PassDownMax : PassDown, width, height);
                        builder.UseTexture(source);
                        builder.SetRenderAttachment(downTexture, 0, AccessFlags.Write);
                        // Execute sets shader globals, which every pass has to opt into.
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) => Execute(d, ctx));
                    }
                    source = downTexture;
                }
                if (levels == 0) return;

                // Separate targets for the way back up, so no texture is both read and
                // rewritten inside the same chain.
                for (int i = levels - 1; i > 0; i--)
                {
                    var size = sizes[i - 1];
                    desc.width = size.x; desc.height = size.y;
                    var up = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenDiffusion Up " + i, false);
                    using (var builder = graph.AddRasterRenderPass<BlitData>("ScreenDiffusion: Upsample " + i, out var data))
                    {
                        Fill(data, source, maxPyramid ? PassUpMax : PassUp, size.x, size.y);
                        builder.UseTexture(source);
                        builder.SetRenderAttachment(up, 0, AccessFlags.Write);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) => Execute(d, ctx));
                    }
                    source = up;
                }

                int compositePass = PassComposite;
                if (targetMode == Target.Layer)
                {
                    if (blendMode == (int)BlendMode.Glow) compositePass = PassCompositeLayerAdditive;
                    else if (blendMode == (int)BlendMode.Max) compositePass = PassCompositeLayerMax;
                    else compositePass = PassCompositeLayer;
                }
                using (var builder = graph.AddRasterRenderPass<BlitData>("ScreenDiffusion: Composite", out var data))
                {
                    Fill(data, source, compositePass, fullWidth, fullHeight);
                    builder.UseTexture(source);
                    if (original.IsValid()) builder.UseTexture(original);
                    if (occluderDepth.IsValid())
                    {
                        builder.UseTexture(layerDepth);
                        builder.UseTexture(occluderDepth);
                    }
                    // Layer mode blends onto the existing colour, so it must not discard it.
                    builder.SetRenderAttachment(resources.activeColorTexture, 0,
                        targetMode == Target.Layer ? AccessFlags.ReadWrite : AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) => Execute(d, ctx));
                }
            }

            // Draws the selected layers on their own, over a transparent clear, so the blur
            // spreads into empty space instead of being clipped against the rest of the scene.
            TextureHandle CaptureLayer(RenderGraph graph, ContextContainer frameData,
                RenderTextureDescriptor desc, int width, int height, out TextureHandle depth)
            {
                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();

                desc.width = width; desc.height = height;
                var color = graph.CreateTexture(new TextureDesc(desc)
                { name = "ScreenDiffusion Layer Color", clearBuffer = true, clearColor = Color.clear });
                var depthDesc = desc;
                depthDesc.graphicsFormat = GraphicsFormat.None;
                depthDesc.depthStencilFormat = GraphicsFormat.D32_SFloat;
                depth = UniversalRenderer.CreateRenderGraphTexture(graph, depthDesc, "ScreenDiffusion Layer Depth", true);

                var drawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing,
                    new FilteringSettings(RenderQueueRange.opaque, layers)));
                var transparentDrawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, lights, SortingCriteria.CommonTransparent);
                // Keep each shader's own blending, including separate RGB and alpha factors.
                var tags = new NativeArray<ShaderTagId>(1, Allocator.Temp);
                tags[0] = ShaderTagId.none;
                var states = new NativeArray<RenderStateBlock>(1, Allocator.Temp);
                states[0] = new RenderStateBlock(RenderStateMask.Depth)
                { depthState = new DepthState(true, CompareFunction.LessEqual) };
                var transparentList = graph.CreateRendererList(new RendererListParams(rendering.cullResults, transparentDrawing,
                    new FilteringSettings(RenderQueueRange.transparent, layers))
                { tagValues = tags, stateBlocks = states, isPassTagName = false });

                using (var builder = graph.AddRasterRenderPass<DrawData>("ScreenDiffusion: Capture Layer", out var data))
                {
                    data.list = list;
                    data.transparentList = transparentList;
                    builder.UseRendererList(list);
                    builder.UseRendererList(transparentList);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(depth, LayerDepthTex);
                    builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.list);
                        ctx.cmd.DrawRendererList(d.transparentList);
                    });
                }
                return color;
            }

            // Use material DepthOnly passes so opaque Ground cutouts retain their alpha test.
            // Transparent materials without a DepthOnly pass do not act as occluders.
            TextureHandle CaptureOccluders(RenderGraph graph, ContextContainer frameData, RenderTextureDescriptor desc)
            {
                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();
                desc.graphicsFormat = GraphicsFormat.None;
                desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
                var depth = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "ScreenDiffusion Occluder Depth", true);
                var drawing = RenderingUtils.CreateDrawingSettings(new ShaderTagId("DepthOnly"), rendering, camera, lights, camera.defaultOpaqueSortFlags);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing,
                    new FilteringSettings(RenderQueueRange.opaque, occluderLayers)));
                using (var builder = graph.AddRasterRenderPass<DepthData>("ScreenDiffusion: Occluder Depth", out var data))
                {
                    data.list = list;
                    builder.UseRendererList(list);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(depth, OccluderDepthTex);
                    builder.SetRenderFunc((DepthData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.list));
                }
                return depth;
            }

            void Fill(BlitData data, TextureHandle source, int shaderPass, int width, int height)
            {
                data.source = source;
                data.material = material;
                data.shaderPass = shaderPass;
                data.texel = new Vector4(0.5f / width, 0.5f / height, width, height);
                data.parameters = new Vector4(spread, strength, blendMode, 0);
                data.group = new Vector4(groupId, maskDraws != null ? 1 : 0, 0, 0);
                data.occlusion = new Vector4(targetMode == Target.Layer && occluderLayers != 0 ? 1 : 0, occlusionDepthBias, 0, 0);
            }

            static void Execute(BlitData data, RasterGraphContext context)
            {
                context.cmd.SetGlobalVector(GroupId, data.group);
                context.cmd.SetGlobalVector(OcclusionId, data.occlusion);
                context.cmd.SetGlobalVector(TexelId, data.texel);
                context.cmd.SetGlobalVector(ParamsId, data.parameters);
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.shaderPass);
            }
        }
    }
}
