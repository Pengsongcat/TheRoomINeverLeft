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
        [Tooltip("金字塔级数。每多一级有效半径翻倍，开销只增加上一级的四分之一。")]
        [Range(1, 6)] public int iterations = 4;
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
        Material material;
        DiffusionPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (diffusionShader == null) diffusionShader = Shader.Find("Hidden/TRINL/ScreenDiffusion");
            if (diffusionShader != null) material = CoreUtils.CreateEngineMaterial(diffusionShader);
            pass = new DiffusionPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType == CameraType.Preview) return;
            if (strength <= 0f) return;
            if (target == Target.Layer && layerMask.value == 0) return;
            var rt = renderingData.cameraData.camera.targetTexture;
            if (rt != null && rt.graphicsFormat == GraphicsFormat.None) return;
            pass.renderPassEvent = injectionPoint;
            pass.material = material;
            pass.targetMode = target;
            pass.layers = layerMask;
            pass.iterations = Mathf.Clamp(iterations, 1, 6);
            pass.spread = Mathf.Clamp(spread, 0.5f, 4f);
            pass.strength = Mathf.Clamp01(strength);
            pass.blendMode = (int)blendMode;
            pass.maxPyramid = maxPyramid;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        sealed class DiffusionPass : ScriptableRenderPass
        {
            internal Material material;
            internal Target targetMode;
            internal int layers, iterations, blendMode;
            internal bool maxPyramid;
            internal float spread, strength;

            const int PassCopy = 0, PassDown = 1, PassUp = 2, PassComposite = 3;
            const int PassCompositeLayer = 4, PassCompositeLayerAdditive = 5;
            const int PassDownMax = 6, PassUpMax = 7, PassCompositeLayerMax = 8;
            const int MinSize = 8;
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
                public Vector4 texel, parameters;
            }
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

                TextureHandle original = default;
                TextureHandle source;

                if (targetMode == Target.Layer)
                {
                    source = CaptureLayer(graph, frameData, desc, fullWidth, fullHeight);
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

                int levels = Mathf.Clamp(iterations, 1, 6);
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
                RenderTextureDescriptor desc, int width, int height)
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
                var depth = UniversalRenderer.CreateRenderGraphTexture(graph, depthDesc, "ScreenDiffusion Layer Depth", true);

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
                    builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.list);
                        ctx.cmd.DrawRendererList(d.transparentList);
                    });
                }
                return color;
            }

            void Fill(BlitData data, TextureHandle source, int shaderPass, int width, int height)
            {
                data.source = source;
                data.material = material;
                data.shaderPass = shaderPass;
                data.texel = new Vector4(0.5f / width, 0.5f / height, width, height);
                data.parameters = new Vector4(spread, strength, blendMode, 0);
            }

            static void Execute(BlitData data, RasterGraphContext context)
            {
                context.cmd.SetGlobalVector(TexelId, data.texel);
                context.cmd.SetGlobalVector(ParamsId, data.parameters);
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.shaderPass);
            }
        }
    }
}
