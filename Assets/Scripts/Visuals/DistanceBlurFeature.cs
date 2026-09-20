using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Distance blur (plan §8.7, Step 4f): a depth-driven blur of the finished camera image, added to VR_Renderer.
// URP's Bokeh depth of field is capped at a 14 px radius, far too weak in VR, so this builds its own chain:
// the image is blurred into progressively softer, half-size copies (dual filtering, each level doubles the
// radius at a fixed cost), and every pixel blends the two copies whose blur matches its depth. Being a
// full-screen blur, every object mixes with what is behind it (no glow or outline around blurred cubes).
// Blur per pixel: SoundLightMath.Blur (min blur up to near, eased to 1 at far) × max blur angle.
// Settings come from the scene's DistanceBlur each frame (static, one scene at a time); off until it turns it
// on after the passthrough fade. Sharp UI is drawn after this, so text stays sharp.
public sealed class DistanceBlurFeature : ScriptableRendererFeature
{
    // Set by DistanceBlur.
    public static bool  On;
    public static float Near = 1f, Far = 6f, MinBlur = 0.2f, MaxAngle = 6f;

    public const int MaxLevels = 8;

    [SerializeField] RenderPassEvent _injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
    [SerializeField] Shader _shader;

    Material _material;
    BlurPass _pass;

    public override void Create()
    {
        CoreUtils.Destroy(_material);
        if (_shader == null) _shader = Shader.Find("Hidden/TRINL/Distance Blur");
        if (_shader != null) _material = CoreUtils.CreateEngineMaterial(_shader);
        _pass = new BlurPass { requiresIntermediateTexture = true };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!On || _material == null || MaxAngle <= 0f) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        _pass.renderPassEvent = _injectionPoint;
        _pass.material = _material;
        _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

    sealed class BlurPass : ScriptableRenderPass
    {
        internal Material material;

        const int PassCopy = 0, PassDown = 1, PassUp = 2, PassComposite = 3;
        const int MinSize = 8;

        static readonly int TexelId  = Shader.PropertyToID("_DistanceBlurTexel");
        static readonly int ParamsId = Shader.PropertyToID("_DistanceBlurParams");
        static readonly int LevelsId = Shader.PropertyToID("_DistanceBlurLevelCount");
        static readonly int[] LevelIds = new int[MaxLevels + 1];

        static BlurPass()
        {
            for (int i = 1; i <= MaxLevels; i++) LevelIds[i] = Shader.PropertyToID("_DistanceBlurLevel" + i);
        }

        sealed class BlitData
        {
            public TextureHandle source;
            public Material material;
            public int shaderPass;
            public Vector4 texel, parameters;
            public float levels;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.activeColorTexture.IsValid() || !resources.cameraDepthTexture.IsValid()) return;
            var camera = frameData.Get<UniversalCameraData>();

            var desc = camera.cameraTargetDescriptor; // keeps the XR slice count
            desc.msaaSamples = 1;
            desc.bindMS = false;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            int fullWidth = desc.width, fullHeight = desc.height;

            // Blur radius in pixels at full blur: the max angle on this eye's projection.
            float pixelsPerTan = camera.GetProjectionMatrix(0).m11 * fullHeight * 0.5f;
            float maxRadius = Mathf.Tan(Mathf.Clamp(MaxAngle, 0f, 60f) * Mathf.Deg2Rad) * pixelsPerTan;
            int wanted = Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(1f + maxRadius, 2f)), 1, MaxLevels);
            var parameters = new Vector4(Near, Mathf.Max(Far, Near + 0.01f), MinBlur, maxRadius);

            // The composite reads the camera colour while writing it, so keep a copy.
            var original = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "DistanceBlur Original", false);
            AddBlit(graph, "DistanceBlur: Copy", resources.activeColorTexture, original, PassCopy, fullWidth, fullHeight, parameters, 0);

            // Down chain: level k is 1/2^k size.
            var down = new TextureHandle[MaxLevels + 1];
            var sizes = new Vector2Int[MaxLevels + 1];
            sizes[0] = new Vector2Int(fullWidth, fullHeight);
            down[0] = original;
            int levels = 0;
            for (int k = 1; k <= wanted; k++)
            {
                var size = new Vector2Int(Mathf.Max(MinSize, sizes[k - 1].x >> 1), Mathf.Max(MinSize, sizes[k - 1].y >> 1));
                if (size == sizes[k - 1]) break;
                sizes[k] = size;
                desc.width = size.x; desc.height = size.y;
                down[k] = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "DistanceBlur Down " + k, false);
                AddBlit(graph, "DistanceBlur: Down " + k, down[k - 1], down[k], PassDown, size.x, size.y, parameters, 0);
                levels = k;
            }
            if (levels == 0) return;

            // Blur level k: level k's copy smoothed back up one size (a tent), so each is soft, not blocky, when
            // the composite stretches it to full size. Blur radius roughly doubles per level.
            var blurred = new TextureHandle[MaxLevels + 1];
            for (int k = 1; k <= levels; k++)
            {
                var size = sizes[k - 1];
                desc.width = size.x; desc.height = size.y;
                blurred[k] = UniversalRenderer.CreateRenderGraphTexture(graph, desc, "DistanceBlur Level " + k, false);
                AddBlit(graph, "DistanceBlur: Level " + k, down[k], blurred[k], PassUp, size.x, size.y, parameters, 0,
                        LevelIds[k]);
            }

            using (var builder = graph.AddRasterRenderPass<BlitData>("DistanceBlur: Composite", out var data))
            {
                Fill(data, original, PassComposite, fullWidth, fullHeight, parameters, levels);
                builder.UseTexture(original);
                for (int k = 1; k <= levels; k++) builder.UseTexture(blurred[k]);
                builder.UseTexture(resources.cameraDepthTexture);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) => Execute(d, ctx));
            }
        }

        void AddBlit(RenderGraph graph, string name, TextureHandle source, TextureHandle destination, int shaderPass,
                     int width, int height, Vector4 parameters, float levels, int globalId = 0)
        {
            using var builder = graph.AddRasterRenderPass<BlitData>(name, out var data);
            Fill(data, source, shaderPass, width, height, parameters, levels);
            builder.UseTexture(source);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            if (globalId != 0) builder.SetGlobalTextureAfterPass(destination, globalId);
            builder.AllowGlobalStateModification(true); // Execute sets shader globals
            builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) => Execute(d, ctx));
        }

        void Fill(BlitData data, TextureHandle source, int shaderPass, int width, int height, Vector4 parameters, float levels)
        {
            data.source = source;
            data.material = material;
            data.shaderPass = shaderPass;
            data.texel = new Vector4(0.5f / width, 0.5f / height, width, height);
            data.parameters = parameters;
            data.levels = levels;
        }

        static void Execute(BlitData data, RasterGraphContext context)
        {
            context.cmd.SetGlobalVector(TexelId, data.texel);
            context.cmd.SetGlobalVector(ParamsId, data.parameters);
            context.cmd.SetGlobalFloat(LevelsId, data.levels);
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.shaderPass);
        }
    }
}
