using UnityEngine;
using UnityEngine.Rendering;

namespace TRINL.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Mesh Fog")]
    public sealed class MeshFog : MonoBehaviour
    {
        public MeshFilter source;
        public MeshFogShape shape;
        [Range(16, 96)] public int bakeResolution = 48;
        [Min(0.01f), Tooltip("Padding on each side, in source mesh local units.")]
        public float bakePadding = 0.25f;
        public bool showFog = true;
        [ColorUsage(false, true)] public Color fogColor = new Color(0.6f, 0.78f, 0.95f);
        [Range(0, 20), Tooltip("Extinction per world-space unit.")] public float density = 3;
        [Min(0.001f), Tooltip("Soft edge width in source mesh local units.")] public float edgeSoftness = 0.12f;
        [Range(0, 1)] public float noiseStrength = 0.75f;
        [Min(0.01f)] public float noiseScale = 4;
        public Vector3 flowSpeed = new Vector3(0.07f, 0.12f, 0.03f);
        [Min(0)] public float contourNoise = 0.045f;
        [Range(0, 1)] public float dissolve;
        [Range(16, 128)] public int steps = 64;
        [Header("Lighting / 雾光照")]
        [Tooltip("Enable scene lights inside the fog. Off preserves the original appearance.")]
        public bool enableLighting;
        [Range(0, 2), Tooltip("Constant fill light; 0 makes unlit regions dark. Not baked ambient lighting.")]
        public float ambientBrightness = 0.2f;
        [Range(0, 10)] public float lightIntensity = 1;
        [Range(-0.8f, 0.8f), Tooltip("0 scatters equally in all directions; positive values emphasize looking toward the light.")]
        public float anisotropy;
        [Tooltip("Sample scene shadow maps inside the fog. Requires shadows enabled on the light and URP asset; adds GPU cost.")]
        public bool receiveLightShadows = true;
        [HideInInspector] public MeshRenderer volumeRenderer;
        [HideInInspector] public Material volumeMaterial;
        [SerializeField, HideInInspector] bool capturedSourceState;
        [SerializeField, HideInInspector] bool sourceWasEnabled;
        [SerializeField, HideInInspector] Renderer capturedRenderer;
        MaterialPropertyBlock properties;

        void Reset() { source = GetComponent<MeshFilter>(); }
        void OnEnable() { Apply(); }
        void LateUpdate() { Apply(); }
        void OnDisable() { RestoreSource(); if (volumeRenderer) volumeRenderer.enabled = false; }

        public void RestoreSource()
        {
            if (capturedSourceState && capturedRenderer) capturedRenderer.enabled = sourceWasEnabled;
            capturedSourceState = false;
            capturedRenderer = null;
        }

        public void Apply()
        {
            bool ready = isActiveAndEnabled && showFog && source && shape && shape.sdf && volumeRenderer && volumeMaterial;
            var renderer = source ? source.GetComponent<Renderer>() : null;
            if (capturedSourceState && capturedRenderer != renderer) RestoreSource();
            if (ready && renderer)
            {
                if (!capturedSourceState)
                {
                    capturedRenderer = renderer;
                    sourceWasEnabled = renderer.enabled;
                    capturedSourceState = true;
                }
                renderer.enabled = false;
            }
            else RestoreSource();
            if (!volumeRenderer) return;
            volumeRenderer.enabled = ready;
            volumeRenderer.receiveShadows = enableLighting && receiveLightShadows;
            if (!ready) return;
            properties ??= new MaterialPropertyBlock();
            properties.Clear();
            properties.SetTexture("_ShapeSDF", shape.sdf);
            properties.SetMatrix("_FogWorldToLocal", source.transform.worldToLocalMatrix);
            properties.SetVector("_BoundsMin", shape.localBounds.min);
            properties.SetVector("_BoundsSize", shape.localBounds.size);
            properties.SetColor("_FogColor", fogColor);
            properties.SetVector("_FogParams", new Vector4(density, Mathf.Max(0.001f, edgeSoftness), noiseStrength, dissolve));
            properties.SetVector("_NoiseParams", new Vector4(noiseScale, contourNoise, Mathf.Clamp(steps, 16, 128), 0));
            properties.SetVector("_Flow", flowSpeed);
            properties.SetVector("_FogLighting", new Vector4(enableLighting ? 1 : 0, ambientBrightness, lightIntensity, anisotropy));
            properties.SetFloat("_FogReceiveShadows", receiveLightShadows ? 1 : 0);
            volumeRenderer.SetPropertyBlock(properties);
        }
    }
}
