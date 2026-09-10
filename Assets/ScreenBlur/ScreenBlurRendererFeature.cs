using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TRINL.Rendering
{
    // The renderer's Opaque Layer Mask must exclude these layers; camera culling must include them.
    public sealed class ScreenBlurRendererFeature : ScriptableRendererFeature
    {
        public LayerMask layerMask = 1 << 7;
        [Range(0, 100)] public float radius = 4;
        [Range(0, 1)] public float opacity = 0.85f;
        [Min(0)] public float depthBias = 0.005f;
        [SerializeField] Shader blurShader;
        Material material;
        BlurPass pass;

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
            pass.material = material;
            pass.layers = layerMask;
            pass.radius = Mathf.Clamp(radius, 0f, 100f);
            pass.opacity = opacity;
            pass.depthBias = depthBias;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        sealed class BlurPass : ScriptableRenderPass
        {
            internal Material material;
            internal int layers;
            internal float radius, opacity, depthBias;
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

            sealed class DrawData { public RendererListHandle list; }
            sealed class FilterData
            {
                public TextureHandle source;
                public Material material;
                public int shaderPass;
                public Vector4 parameters, texel;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
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
                using (var builder = graph.AddRasterRenderPass<DrawData>("ScreenBlur: Original Materials", out var data))
                {
                    data.list = list;
                    builder.UseRendererList(list);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(depth, LayerDepth);
                    builder.SetGlobalTextureAfterPass(resources.cameraDepthTexture, SceneDepth);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.list));
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
            }

            static void ExecuteFilter(FilterData data, RasterGraphContext context)
            {
                context.cmd.SetGlobalVector(Parameters, data.parameters);
                context.cmd.SetGlobalVector(Texel, data.texel);
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.shaderPass);
            }
        }
    }
}
