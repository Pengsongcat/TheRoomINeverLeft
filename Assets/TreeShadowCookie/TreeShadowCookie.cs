using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TRINL.Rendering
{
    // Procedural dappled tree light (komorebi) rendered into a texture that URP light cookies
    // and VLB volumetric cookies can both use. Two parts, multiplied together:
    //
    // Far canopy, drawn in the shader. Every small gap in it is a pinhole that projects an image
    // of the sun, so its light is the canopy transmission averaged over the sun's disc. That is
    // why real leaf light shows round, similar-sized spots whatever the gap shape, and why "blur"
    // here is a disc radius rather than a gaussian.
    //
    // A tree in front of it: limbs, branches and twigs carrying leaves, generated and animated on
    // the CPU and drawn as geometry, so every leaf hangs off a twig and moves with it. A shadow's
    // penumbra grows with distance from the wall, so each branch has a depth and the tree is
    // split into near / mid / far bands, each blurred by its own sun disc.
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("TRINL/Tree Shadow Cookie")]
    public sealed class TreeShadowCookie : MonoBehaviour
    {
        public enum Size { _256 = 256, _512 = 512, _1024 = 1024 }
        public enum LeafShape { Maple = 1, Ovate = 2, Mixed = 3 }

        // Output
        public Size resolution = Size._512;
        [Tooltip("留空则自动创建。指定 RenderTexture 资产时，所有引用它的地方都会直接用上程序化树影；不要同时让 VideoPlayer 往里写。")]
        public RenderTexture targetTexture;
        [Tooltip("每秒更新次数，0 = 每帧。")]
        [Range(0f, 90f)] public float updateRate = 30f;
        [Tooltip("启用时把它们的 Cookie 换成本贴图，停用组件时还原原来的 Cookie。")]
        public Light[] lights = new Light[0];
        [Tooltip("启用时把它们的 Cookie Texture 换成本贴图，停用组件时还原。")]
        public VLB.VolumetricCookieHD[] volumetricCookies = new VLB.VolumetricCookieHD[0];
        public bool animateInEditMode = true;

        // Far canopy (the round sun spots)
        public int seed = 1;
        [Range(0.3f, 3f)] public float leafSize = 1f;
        [Range(0f, 1f)] public float canopyDensity = 0.2f;
        [Range(0f, 1f)] public float clumping = 0.7f;

        // Tree (branches carrying leaves)
        [Range(0, 4000)] public int leafCount = 600;
        public LeafShape leafShape = LeafShape.Maple;
        [Range(0.3f, 2.5f)] public float treeLeafSize = 1f;
        [Range(0, 5)] public int limbCount = 3;
        [Range(0.3f, 2.5f)] public float branchWidth = 1f;
        [Range(0f, 360f)] public float treeDirection = 60f;
        [Range(0f, 1f)] public float treeDistance = 0.4f;
        [Range(0f, 1f)] public float depthSpread = 0.8f;

        // Wind
        [Tooltip("目标风力。实际风力按 Wind Ramp 秒数平滑过渡到这个值，Timeline 直接 K 这个值即可。")]
        [Range(0f, 1f)] public float windStrength;
        [Min(0f)] public float windRamp = 1.5f;
        [Range(0.1f, 4f)] public float windSpeed = 1f;
        [Range(0f, 360f)] public float windDirection = 20f;
        [Range(0f, 1f)] public float gustiness = 0.6f;
        [Range(0f, 1f)] public float flutter = 0.5f;
        [Range(0f, 1f)] public float branchSway = 0.5f;
        [Range(0f, 0.2f)] public float idleSway = 0.03f;
        [Range(0f, 1f)] public float windLean = 0.4f;
        [Range(0f, 1f)] public float windBlurJitter = 0.5f;
        [Range(0f, 1f)] public float windMotionBlur = 0.3f;

        // Sun
        [Range(0f, 4f)] public float sunIntensity = 1f;
        [Range(0f, 1f)] public float shadowBrightness = 0.06f;
        [Range(0.5f, 2f)] public float contrast = 1f;
        [Range(0f, 0.1f)] public float blur = 0.03f;
        [Range(0f, 0.05f)] public float nearBlur = 0.004f;
        [Range(0f, 0.1f)] public float branchBlur = 0.035f;
        [Range(0f, 2f)] public float halation = 0.4f;
        [Range(1, 5)] public int halationRadius = 3;
        [Range(0f, 1f)] public float edgeFade;

        [SerializeField] Shader shader;
        [SerializeField, HideInInspector] Texture[] originalLightCookies = new Texture[0];
        [SerializeField, HideInInspector] Texture[] originalVolumetricCookies = new Texture[0];

        const int PassCanopy = 0, PassDown = 1, PassUp = 2, PassDisc = 3, PassCompose = 4, PassBright = 5;
        const int PassTree = 6, PassCombine = 7;
        // A single small gap passes only part of the sun disc; photos of leaf light are exposed
        // for the spots, so sunIntensity 1 should already make an isolated spot read clearly.
        const float ExposureGain = 5f;
        static readonly int DomainId = Shader.PropertyToID("_Domain");
        static readonly int CanopyId = Shader.PropertyToID("_Canopy");
        static readonly int WindAId = Shader.PropertyToID("_WindA");
        static readonly int WindBId = Shader.PropertyToID("_WindB");
        static readonly int WindCId = Shader.PropertyToID("_WindC");
        static readonly int ToneId = Shader.PropertyToID("_Tone");
        static readonly int Tone2Id = Shader.PropertyToID("_Tone2");
        static readonly int DiscRadiusId = Shader.PropertyToID("_DiscRadius");
        static readonly int DiscTapsId = Shader.PropertyToID("_DiscTaps");
        static readonly int ChannelMaskId = Shader.PropertyToID("_ChannelMask");
        static readonly int DiscJitterId = Shader.PropertyToID("_DiscJitter");
        static readonly int FarLightId = Shader.PropertyToID("_FarLight");
        static readonly int GlowTexId = Shader.PropertyToID("_GlowTex");
        static readonly int BandNearId = Shader.PropertyToID("_BandNear");
        static readonly int BandMidId = Shader.PropertyToID("_BandMid");
        static readonly int BandFarId = Shader.PropertyToID("_BandFar");
        static readonly Vector4 Red = new(1, 0, 0, 0), Green = new(0, 1, 0, 0), Blue = new(0, 0, 1, 0);

        Material material;
        RenderTexture ownTexture;
        Texture boundTexture;
        readonly List<RenderTexture> temps = new();
        float windTime, gustTime, currentWind, sinceRender;
        bool dirty = true, needsBind = true;
#if UNITY_EDITOR
        double lastEditorTime;
#endif

        /// <summary>The texture being rendered into. Assign it anywhere a cookie is accepted.</summary>
        public RenderTexture Output => targetTexture != null ? targetTexture : ownTexture;
        /// <summary>The wind actually applied this frame, after ramping toward windStrength.</summary>
        public float CurrentWind => currentWind;

        /// <summary>Ramp the wind to <paramref name="strength"/> over <paramref name="rampSeconds"/>.</summary>
        public void SetWind(float strength, float rampSeconds)
        {
            windStrength = Mathf.Clamp01(strength);
            windRamp = Mathf.Max(0f, rampSeconds);
        }

        public void StartWind(float strength = 1f, float rampSeconds = 1.5f) => SetWind(strength, rampSeconds);
        public void StopWind(float rampSeconds = 2f) => SetWind(0f, rampSeconds);
        /// <summary>Jump straight to the target wind, skipping the ramp.</summary>
        public void SnapWind() => currentWind = windStrength;
        public void RenderNow() => Render();

        void Reset() => FindShader();

        void OnValidate()
        {
            FindShader();
            dirty = true;
            // Pick up lights or beams added to the lists while enabled.
            needsBind = true;
        }

        void FindShader()
        {
            if (shader == null) shader = Shader.Find("Hidden/TRINL/TreeShadowCookie");
        }

        void OnEnable()
        {
            FindShader();
            currentWind = windStrength;
            Render();
#if UNITY_EDITOR
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            Unbind();
            DestroySafe(ownTexture);
            DestroySafe(material);
            DestroySafe(mesh);
            ownTexture = null;
            material = null;
            mesh = null;
            treeHash = 0;
            commands?.Release();
            commands = null;
        }

        void Update()
        {
            if (Application.isPlaying) Advance(Time.deltaTime);
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (Application.isPlaying || this == null) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastEditorTime), 0.1f);
            lastEditorTime = now;
            if (!animateInEditMode)
            {
                if (dirty) { Render(); SceneView.RepaintAll(); }
                return;
            }
            if (Advance(dt)) SceneView.RepaintAll();
        }
#endif

        /// <summary>
        /// Advance wind and time by <paramref name="dt"/> seconds and re-render if due. Called every
        /// frame automatically; call it yourself to drive the animation manually, e.g. while
        /// Time.timeScale is 0. Returns true when the texture was re-rendered.
        /// </summary>
        public bool Advance(float dt)
        {
            currentWind = windRamp <= 0f
                ? windStrength
                : Mathf.MoveTowards(currentWind, windStrength, dt / windRamp);
            // Wrapped so the shader's sin() never loses precision on long sessions.
            windTime = (windTime + dt * windSpeed) % 4096f;
            gustTime = (gustTime + dt * windSpeed * 0.45f) % 4096f;
            sinceRender += dt;

            bool moving = currentWind > 1e-4f || windStrength > 0f || idleSway > 0f;
            // Still pattern but a target was reassigned by someone else: rebind without waiting.
            if (!moving && Output != null && !AllBound(Output)) Bind();
            if (!dirty)
            {
                if (!moving) return false;
                if (updateRate > 0f && sinceRender < 1f / updateRate) return false;
            }
            Render();
            return true;
        }

        void Render()
        {
            if (shader == null) return;
            if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            // Graphics.Blit leaves its destination active; put back whatever was there.
            var previousActive = RenderTexture.active;

            int res = (int)resolution;
            var output = EnsureOutput(res);
            if (output == null) return;
            // Other scripts may assign their own cookie after we bind (VideoLightCookieURP does it
            // in Start, which runs after OnEnable), so re-check every render instead of once.
            if (needsBind || output != boundTexture || !AllBound(output)) Bind();

            // Everything is generated past the edges so the disc blurs never read clamped pixels.
            float widest = Mathf.Max(blur, Mathf.Max(nearBlur, branchBlur));
            float margin = Mathf.Clamp(0.08f + widest * 1.3f, 0.08f, 0.3f);
            float domain = 1f + 2f * margin;
            int canopyRes = res;
            float seedOffset = Mathf.Repeat(seed * 0.618034f, 1f) * 97f;
            float angle = windDirection * Mathf.Deg2Rad;
            windDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            material.SetVector(DomainId, new Vector4(margin, domain, domain / canopyRes, seedOffset));
            material.SetVector(CanopyId, new Vector4(34f / leafSize, Mathf.Lerp(0.55f, 0.95f, canopyDensity), clumping, Mathf.Lerp(0.3f, 0.72f, canopyDensity)));
            material.SetVector(WindAId, new Vector4(windDir.x, windDir.y, currentWind, windTime));
            material.SetVector(WindBId, new Vector4(gustiness, gustTime, flutter, branchSway));
            material.SetVector(WindCId, new Vector4(idleSway, 0f, 0f, 0f));
            material.SetVector(ToneId, new Vector4(sunIntensity * ExposureGain, shadowBrightness, contrast, halation));
            material.SetVector(Tone2Id, new Vector4(0f, edgeFade, 0f, 0f));
            float jitterRadius = windBlurJitter * currentWind * 0.6f;
            float jitterStretch = windMotionBlur * currentWind * 0.6f;
            material.SetVector(DiscJitterId, new Vector4(jitterRadius, jitterStretch, 0f, 0f));

            var single = Supported(RenderTextureFormat.RHalf);
            var colour = Supported(RenderTextureFormat.ARGBHalf);

            var canopy = Temp(canopyRes, single);
            Graphics.Blit(null, canopy, material, PassCanopy);
            var far = Disc(canopy, blur, Red, domain);

            Texture nearBand = Texture2D.whiteTexture, midBand = Texture2D.whiteTexture, farBand = Texture2D.whiteTexture;
            if (limbCount > 0)
            {
                PoseTree();
                var tree = Temp(canopyRes, colour);
                DrawTree(tree, margin);
                nearBand = Disc(tree, nearBlur, Red, domain);
                midBand = Disc(tree, (nearBlur + branchBlur) * 0.5f, Green, domain);
                farBand = Disc(tree, branchBlur, Blue, domain);
            }
            material.SetTexture(FarLightId, far);
            material.SetTexture(BandNearId, nearBand);
            material.SetTexture(BandMidId, midBand);
            material.SetTexture(BandFarId, farBand);

            // Halation: a wide dual-filter blur of the bright spots only, added back on top.
            Texture glow = Texture2D.blackTexture;
            if (halation > 0f)
            {
                int size = Mathf.Max(8, canopyRes / 2);
                var combined = Temp(size, single);
                Graphics.Blit(null, combined, material, PassCombine);
                var bright = Temp(size, single);
                Graphics.Blit(combined, bright, material, PassBright);
                RenderTexture source = bright;
                var chain = new List<RenderTexture>();
                for (int i = 0; i < Mathf.Clamp(halationRadius, 1, 5); i++)
                {
                    size = Mathf.Max(4, size / 2);
                    var down = Temp(size, single);
                    Graphics.Blit(source, down, material, PassDown);
                    chain.Add(down);
                    source = down;
                }
                for (int i = chain.Count - 2; i >= 0; i--)
                {
                    var up = Temp(chain[i].width, single);
                    Graphics.Blit(source, up, material, PassUp);
                    source = up;
                }
                glow = source;
            }

            material.SetTexture(GlowTexId, glow);
            Graphics.Blit(null, output, material, PassCompose);
            // URP's cookie atlas only recopies a RenderTexture when its update count changes.
            output.IncrementUpdateCount();

            RenderTexture.active = previousActive;
            foreach (var t in temps) RenderTexture.ReleaseTemporary(t);
            temps.Clear();
            dirty = false;
            sinceRender = 0f;
        }

        // Average one channel of source over the sun disc of the given radius (output units).
        // Downsample just enough that the radius is at most ~7 source texels: the taps then cover
        // it without grain, and it is still wide enough in texels for the round edge to survive.
        RenderTexture Disc(RenderTexture source, float radius, Vector4 channel, float domain)
        {
            float discUv = radius / domain;
            // Level and tap count depend on the base radius only, never on the wind: switching
            // level mid-ramp would visibly pop, and a changing tap count reshuffles every sample
            // position. The headroom in the tap count covers the disc growing and stretching in wind.
            float discPx = discUv * source.width;
            int levels = discPx <= 7f ? 0 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(discPx / 7f, 2f)), 0, 4);
            int size = source.width;
            for (int i = 0; i < levels; i++)
            {
                size = Mathf.Max(8, size / 2);
                var down = Temp(size, source.format);
                Graphics.Blit(source, down, material, PassDown);
                source = down;
            }
            // About one tap per two texels of the base disc, times three for what wind can add.
            float texels = discPx / (1 << levels);
            int taps = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * texels * texels * 1.5f), 8, 96);
            material.SetFloat(DiscRadiusId, discUv);
            material.SetFloat(DiscTapsId, taps);
            material.SetVector(ChannelMaskId, channel);
            var result = Temp(size, Supported(RenderTextureFormat.RHalf));
            Graphics.Blit(source, result, material, PassDisc);
            return result;
        }

        RenderTexture Temp(int size, RenderTextureFormat format)
        {
            var desc = new RenderTextureDescriptor(size, size, format, 0) { sRGB = false, useMipMap = false, msaaSamples = 1 };
            var t = RenderTexture.GetTemporary(desc);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            temps.Add(t);
            return t;
        }

        static RenderTextureFormat Supported(RenderTextureFormat format) =>
            SystemInfo.SupportsRenderTextureFormat(format) ? format : RenderTextureFormat.ARGBHalf;

        // ---- Tree ------------------------------------------------------------------------------

        struct Branch
        {
            public int parent, order, segments, first;
            public float attach, restAngle, length, width0, width1, curvature, depth;
            public float freq, phase, phase2, amp;
            public Vector2 root;
        }

        struct Leaf
        {
            public int branch;
            public float attach, side, spread, size, freq, phase, shape;
        }

        // Thinner wood moves more and faster; limbs barely sway.
        static readonly float[] SwayByOrder = { 0.03f, 0.07f, 0.13f, 0.2f };
        static readonly float[] FreqByOrder = { 1.2f, 1.8f, 2.6f, 3.6f };
        static readonly float[] LeanByOrder = { 0.05f, 0.1f, 0.16f, 0.22f };
        static readonly int[] SegmentsByOrder = { 8, 6, 4, 3 };
        const int MaxBranches = 1500;
        const float LeafBase = 0.03f;
        const float BranchBase = 0.011f;

        readonly List<Branch> branches = new();
        readonly List<Leaf> leaves = new();
        readonly List<Vector3> vertices = new();
        Vector2[] points = new Vector2[0];
        float[] angles = new float[0];
        Mesh mesh;
        CommandBuffer commands;
        System.Random random;
        Vector2 windDir = Vector2.right;
        int treeHash;

        float Rand() => (float)random.NextDouble();

        // Rebuilt whenever a structural setting changes, including from Timeline or scripts.
        int TreeHash()
        {
            unchecked
            {
                int h = seed;
                h = h * 31 + leafCount;
                h = h * 31 + (int)leafShape;
                h = h * 31 + limbCount;
                h = h * 31 + Mathf.RoundToInt(treeLeafSize * 1000f);
                h = h * 31 + Mathf.RoundToInt(branchWidth * 1000f);
                h = h * 31 + Mathf.RoundToInt(treeDirection * 10f);
                h = h * 31 + Mathf.RoundToInt(treeDistance * 1000f);
                h = h * 31 + Mathf.RoundToInt(depthSpread * 1000f);
                return h == 0 ? 1 : h;
            }
        }

        void GenerateTree()
        {
            random = new System.Random(seed * 7919 + 17);
            branches.Clear();
            leaves.Clear();

            float a0 = treeDirection * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            var perp = new Vector2(-dir.y, dir.x);

            // Limbs enter from outside the frame on the side the tree stands, spread across it,
            // each at its own distance from the wall.
            var depths = new List<float>();
            for (int i = 0; i < limbCount; i++)
                depths.Add(limbCount == 1 ? 0.5f : (i + 0.2f + 0.6f * Rand()) / limbCount);
            for (int i = depths.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (depths[i], depths[j]) = (depths[j], depths[i]);
            }
            for (int i = 0; i < limbCount; i++)
            {
                float across = limbCount == 1 ? (Rand() - 0.5f) * 0.3f : ((i + Rand()) / limbCount - 0.5f) * 0.9f;
                var root = new Vector2(0.5f, 0.5f) - dir * 0.8f + perp * across;
                AddBranch(-1, 0, 0f, a0 + (Rand() - 0.5f) * 0.7f, Mathf.Lerp(1.15f, 1.45f, Rand()),
                    BranchBase * branchWidth * Mathf.Lerp(0.8f, 1.2f, Rand()), depths[i], root);
            }

            int total = 0;
            for (int i = 0; i < branches.Count; i++)
            {
                var b = branches[i];
                b.first = total;
                branches[i] = b;
                total += b.segments + 1;
            }
            points = new Vector2[total];
            angles = new float[total];

            // Leaves grow on the thin wood, weighted by length and bunched toward the tips.
            var bearing = new List<int>();
            var cumulative = new List<float>();
            float sum = 0f;
            for (int i = 0; i < branches.Count; i++)
            {
                if (branches[i].order < 2) continue;
                sum += branches[i].length;
                bearing.Add(i);
                cumulative.Add(sum);
            }
            if (bearing.Count > 0)
            {
                for (int n = 0; n < leafCount; n++)
                {
                    int k = cumulative.BinarySearch(Rand() * sum);
                    if (k < 0) k = ~k;
                    k = Mathf.Min(k, bearing.Count - 1);
                    float r = Rand();
                    leaves.Add(new Leaf
                    {
                        branch = bearing[k],
                        attach = 1f - r * r * 0.85f,
                        side = Rand() < 0.5f ? -1f : 1f,
                        spread = Mathf.Lerp(0.5f, 1.2f, Rand()),
                        size = LeafBase * treeLeafSize * Mathf.Lerp(0.7f, 1.25f, Rand()),
                        freq = Mathf.Lerp(5f, 9f, Rand()),
                        phase = Rand() * 6.2831853f,
                        shape = leafShape == LeafShape.Mixed ? (Rand() < 0.5f ? 1f : 2f) : (float)(int)leafShape
                    });
                }
            }
            BuildTreeMesh();
            treeHash = TreeHash();
        }

        void AddBranch(int parent, int order, float attach, float restAngle, float length, float width, float depth, Vector2 root)
        {
            if (branches.Count >= MaxBranches) return;
            var b = new Branch
            {
                parent = parent,
                order = order,
                attach = attach,
                restAngle = restAngle,
                length = length,
                width0 = width,
                width1 = width * (order >= 2 ? 0.35f : 0.55f),
                curvature = (Rand() - 0.5f) * (0.8f + order * 0.4f),
                depth = depth,
                segments = SegmentsByOrder[order],
                freq = FreqByOrder[order] * Mathf.Lerp(0.8f, 1.2f, Rand()),
                phase = Rand() * 6.2831853f,
                phase2 = Rand() * 6.2831853f,
                amp = SwayByOrder[order],
                root = root
            };
            int index = branches.Count;
            branches.Add(b);
            if (order >= 3 || length < 0.05f) return;

            float t = 0.12f + Rand() * 0.1f;
            float side = Rand() < 0.5f ? -1f : 1f;
            while (t < 0.92f)
            {
                float childLength = length * Mathf.Lerp(0.38f, 0.62f, Rand()) * (1f - 0.35f * t);
                float childAngle = side * Mathf.Lerp(0.45f, 1.0f, Rand());
                float childWidth = Mathf.Lerp(b.width0, b.width1, t) * 0.7f;
                float childDepth = Mathf.Clamp01(depth + (Rand() - 0.5f) * 0.3f);
                AddBranch(index, order + 1, t, childAngle, childLength, childWidth, childDepth, Vector2.zero);
                if (Rand() < 0.8f) side = -side;
                t += Mathf.Lerp(0.1f, 0.2f, Rand()) * (order == 0 ? 0.8f : 1.2f);
            }
        }

        // Where between the near (sharp) and far (blurred) bands this depth falls. Weights add to
        // one, so something between two bands is drawn partly into each and blurs in between.
        Color BandWeights(float rawDepth)
        {
            float d = Mathf.Clamp01(treeDistance + (rawDepth - 0.5f) * depthSpread) * 2f;
            return new Color(Mathf.Clamp01(1f - d), Mathf.Max(0f, 1f - Mathf.Abs(d - 1f)), Mathf.Clamp01(d - 1f), 0f);
        }

        // Topology, UVs and band colours never change between poses; only positions do.
        void BuildTreeMesh()
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "TreeShadowCookie Tree", hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            vertices.Clear();
            var uvs = new List<Vector4>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            foreach (var b in branches)
            {
                var c = BandWeights(b.depth);
                int start = vertices.Count;
                for (int k = 0; k <= b.segments; k++)
                {
                    float s = (float)k / b.segments;
                    uvs.Add(new Vector4(-1f, s, 0f, 0f));
                    uvs.Add(new Vector4(1f, s, 0f, 0f));
                    colors.Add(c);
                    colors.Add(c);
                    vertices.Add(Vector3.zero);
                    vertices.Add(Vector3.zero);
                    if (k == 0) continue;
                    int v = start + (k - 1) * 2;
                    triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                    triangles.Add(v + 1); triangles.Add(v + 2); triangles.Add(v + 3);
                }
            }
            foreach (var l in leaves)
            {
                var c = BandWeights(branches[l.branch].depth);
                int v = vertices.Count;
                // Leaf space: y = -1.4 at the attach point (petiole), +1 at the tip.
                uvs.Add(new Vector4(-1f, -1.4f, l.shape, 0f));
                uvs.Add(new Vector4(1f, -1.4f, l.shape, 0f));
                uvs.Add(new Vector4(-1f, 1f, l.shape, 0f));
                uvs.Add(new Vector4(1f, 1f, l.shape, 0f));
                for (int k = 0; k < 4; k++) { colors.Add(c); vertices.Add(Vector3.zero); }
                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                triangles.Add(v + 1); triangles.Add(v + 2); triangles.Add(v + 3);
            }

            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(10f, 10f, 1f));
        }

        // Forward kinematics: each branch starts wherever its parent currently is, so the whole
        // hierarchy moves together and leaves stay on their twigs.
        void PoseTree()
        {
            if (mesh == null || treeHash != TreeHash()) GenerateTree();

            float windAngle = windDirection * Mathf.Deg2Rad;
            int vi = 0;
            for (int i = 0; i < branches.Count; i++)
            {
                var b = branches[i];
                Vector2 start;
                float baseAngle;
                if (b.parent < 0)
                {
                    start = b.root;
                    baseAngle = b.restAngle;
                }
                else
                {
                    Sample(b.parent, b.attach, out start, out float parentAngle);
                    baseAngle = parentAngle + b.restAngle;
                }
                float gust = Mathf.Lerp(1f, Gust(start) * 2f, gustiness);
                float w = currentWind * gust + idleSway;
                float sway = b.amp * branchSway * 2f * w *
                             (Mathf.Sin(windTime * b.freq + b.phase) + 0.45f * Mathf.Sin(windTime * b.freq * 2.3f + b.phase2));
                // The wind also pushes the tree downwind; thin branches give way the most.
                float lean = windLean * currentWind * gust * LeanByOrder[b.order] * Mathf.Sin(windAngle - baseAngle);

                float step = b.length / b.segments;
                var p = start;
                for (int k = 0; k <= b.segments; k++)
                {
                    float s = (float)k / b.segments;
                    // Bend rather than rotate rigidly: the tip moves more than the base.
                    float a = baseAngle + b.curvature * b.length * s + (sway + lean) * (0.35f + 0.65f * s);
                    points[b.first + k] = p;
                    angles[b.first + k] = a;
                    var n = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Lerp(b.width0, b.width1, s);
                    vertices[vi++] = p - n;
                    vertices[vi++] = p + n;
                    p += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * step;
                }
            }

            foreach (var l in leaves)
            {
                Sample(l.branch, l.attach, out var pos, out float twigAngle);
                float w = currentWind * Mathf.Lerp(1f, Gust(pos) * 2f, gustiness) + idleSway;
                float a = twigAngle + l.side * l.spread + flutter * w * 0.5f * Mathf.Sin(windTime * l.freq + l.phase);
                // Turning edge-on and back; centred on the rest width so wind keeps the exposure.
                float widthScale = 1f + 0.4f * Mathf.Sin(windTime * l.freq * 1.3f + l.phase * 1.7f) * Mathf.Clamp01(w * flutter);
                var y = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * l.size;
                var x = new Vector2(-y.y, y.x) * widthScale;
                vertices[vi++] = pos - x;
                vertices[vi++] = pos + x;
                vertices[vi++] = pos - x + y * 2.4f;
                vertices[vi++] = pos + x + y * 2.4f;
            }
            mesh.SetVertices(vertices);
        }

        void Sample(int branch, float t, out Vector2 position, out float angle)
        {
            var b = branches[branch];
            float f = Mathf.Clamp01(t) * b.segments;
            int k = Mathf.Min((int)f, b.segments - 1);
            float u = f - k;
            position = Vector2.Lerp(points[b.first + k], points[b.first + k + 1], u);
            angle = Mathf.Lerp(angles[b.first + k], angles[b.first + k + 1], u);
        }

        void DrawTree(RenderTexture target, float margin)
        {
            commands ??= new CommandBuffer { name = "TreeShadowCookie Tree" };
            commands.Clear();
            commands.SetRenderTarget(target);
            // White = fully open; every branch and leaf multiplies its band down.
            commands.ClearRenderTarget(false, true, Color.white);
            commands.SetViewProjectionMatrices(Matrix4x4.identity,
                Matrix4x4.Ortho(-margin, 1f + margin, -margin, 1f + margin, -1f, 1f));
            commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, PassTree);
            Graphics.ExecuteCommandBuffer(commands);
        }

        // Same travelling gust field as the shader, so the tree and the far canopy move together.
        float Gust(Vector2 p)
        {
            var perp = new Vector2(-windDir.y, windDir.x);
            float n = ValueNoise(Vector2.Dot(p, windDir) * 2.5f - gustTime, Vector2.Dot(p, perp) * 1.2f + 7.3f);
            float x = Mathf.Clamp01((n - 0.3f) / 0.55f);
            return x * x * (3f - 2f * x);
        }

        static float ValueNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        // ---- Output and binding ------------------------------------------------------------

        RenderTexture EnsureOutput(int res)
        {
            if (targetTexture != null)
            {
                // Switched to an asset: the internal texture is no longer referenced by anything.
                if (ownTexture != null) { DestroySafe(ownTexture); ownTexture = null; }
                if (!targetTexture.IsCreated()) targetTexture.Create();
                return targetTexture;
            }
            if (ownTexture == null || ownTexture.width != res)
            {
                DestroySafe(ownTexture);
                ownTexture = new RenderTexture(res, res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "TreeShadowCookie",
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false
                };
            }
            if (!ownTexture.IsCreated()) ownTexture.Create();
            return ownTexture;
        }

        void Bind()
        {
            var tex = Output;
            if (tex == null) return;
            System.Array.Resize(ref originalLightCookies, lights.Length);
            System.Array.Resize(ref originalVolumetricCookies, volumetricCookies.Length);
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                // Only remember a real cookie: after a reload ours shows up as missing/null,
                // and overwriting the saved original with that would lose the video cookie.
                if (l.cookie != null && l.cookie != tex && l.cookie != boundTexture) originalLightCookies[i] = l.cookie;
                l.cookie = tex;
            }
            for (int i = 0; i < volumetricCookies.Length; i++)
            {
                var c = volumetricCookies[i];
                if (c == null) continue;
                if (c.cookieTexture != null && c.cookieTexture != tex && c.cookieTexture != boundTexture)
                    originalVolumetricCookies[i] = c.cookieTexture;
                c.cookieTexture = tex;
            }
            boundTexture = tex;
            needsBind = false;
        }

        bool AllBound(Texture tex)
        {
            foreach (var l in lights)
                if (l != null && l.cookie != tex) return false;
            foreach (var c in volumetricCookies)
                if (c != null && c.cookieTexture != tex) return false;
            return true;
        }

        void Unbind()
        {
            if (boundTexture == null) return;
            for (int i = 0; i < lights.Length && i < originalLightCookies.Length; i++)
                if (lights[i] != null && lights[i].cookie == boundTexture) lights[i].cookie = originalLightCookies[i];
            for (int i = 0; i < volumetricCookies.Length && i < originalVolumetricCookies.Length; i++)
                if (volumetricCookies[i] != null && volumetricCookies[i].cookieTexture == boundTexture)
                    volumetricCookies[i].cookieTexture = originalVolumetricCookies[i];
            boundTexture = null;
        }

        static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (o is RenderTexture rt)
            {
                if (RenderTexture.active == rt) RenderTexture.active = null;
                rt.Release();
            }
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
