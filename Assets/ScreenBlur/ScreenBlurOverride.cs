using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TRINL.Rendering
{
    // Per-object ScreenBlur settings. The renderer still has to be on the feature's
    // ScreenBlur layer; this only replaces the parameters used for that renderer.
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("TRINL/Screen Blur Override")]
    public sealed class ScreenBlurOverride : MonoBehaviour
    {
        public bool includeChildren = true;
        [Range(0, 100)] public float radius = 4;
        [Range(0, 1)] public float opacity = 0.85f;
        [Min(0)] public float depthBias = 0.005f;

        internal static readonly List<ScreenBlurOverride> Active = new();
        readonly Dictionary<long, Material> masks = new();

        void Reset()
        {
#if UNITY_EDITOR
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/VR_Renderer.asset");
            if (data) CopyFrom(data.rendererFeatures.Find(f => f is ScreenBlurRendererFeature) as ScreenBlurRendererFeature);
#endif
        }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }

        void OnDisable()
        {
            Active.Remove(this);
            foreach (var m in masks.Values) CoreUtils.Destroy(m);
            masks.Clear();
        }

        public void CopyFrom(ScreenBlurRendererFeature source)
        {
            if (!source) return;
            radius = source.radius;
            opacity = source.opacity;
            depthBias = source.depthBias;
        }

        internal void Collect(List<MaskDraw> draws, Shader shader, int id, Camera camera, int layers)
        {
            CollectRenderers(includeChildren ? GetComponentsInChildren<Renderer>() : GetComponents<Renderer>(),
                this, masks, draws, shader, id, camera, layers);
        }

        internal static void CollectRenderers(Renderer[] renderers, ScreenBlurOverride expectedOwner,
            Dictionary<long, Material> cache, List<MaskDraw> draws, Shader shader, int id, Camera camera, int layers)
        {
            if (!shader) return;
            foreach (var r in renderers)
            {
                if (!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy ||
                    (layers & camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                // The nearest enabled override owns the renderer; disabling it restores parent/global settings.
                ScreenBlurOverride owner = null;
                for (var t = r.transform; t != null; t = t.parent)
                {
                    var candidate = t.GetComponent<ScreenBlurOverride>();
                    if (candidate && candidate.isActiveAndEnabled && (t == r.transform || candidate.includeChildren))
                    { owner = candidate; break; }
                }
                if (owner != expectedOwner) continue;
#if UNITY_EDITOR
                if (UnityEditor.SceneVisibilityManager.instance.IsHidden(r.gameObject)) continue;
#endif
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                var materials = r.sharedMaterials;
                for (int submesh = 0; submesh < materials.Length; submesh++)
                {
                    var source = materials[submesh];
                    if (!source) continue;
                    long key = ((long)r.GetInstanceID() << 32) | (uint)submesh;
                    if (!cache.TryGetValue(key, out var mask) || !mask)
                    { mask = CoreUtils.CreateEngineMaterial(shader); cache[key] = mask; }
                    // Match the source alpha so cutout foliage does not claim its whole quad.
                    string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                    mask.SetTexture("_BaseMap", source.HasProperty(map) ? source.GetTexture(map) : Texture2D.whiteTexture);
                    var scale = source.HasProperty(map) ? source.GetTextureScale(map) : Vector2.one;
                    var offset = source.HasProperty(map) ? source.GetTextureOffset(map) : Vector2.zero;
                    mask.SetVector("_MaskST", new Vector4(scale.x, scale.y, offset.x, offset.y));
                    float cutoff = source.IsKeywordEnabled("_ALPHATEST_ON")
                        ? (source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .5f) : 0;
                    mask.SetFloat("_MaskCutoff", cutoff);
                    draws.Add(new MaskDraw { renderer = r, material = mask, submesh = submesh, objectId = id });
                }
            }
        }

        internal struct MaskDraw { internal Renderer renderer; internal Material material; internal int submesh, objectId; }
    }
}
