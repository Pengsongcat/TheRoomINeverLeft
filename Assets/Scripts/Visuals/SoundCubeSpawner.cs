using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A lit cube on the floor below every sound that has no sound light (ambient beds and NoSoundLight sounds,
// taken from SoundLightSpawner). The cubes have no light or glow of their own: in the black void they only
// show where a nearby sound light reaches them, so they appear partly lit and flicker with that sound.
// Real size (not scaled with the room), aligned with Room Root, sitting on the floor straight below the sound.
// Shown after the passthrough fade, only while its sound is playing and a sound light that is on reaches it.
public class SoundCubeSpawner : MonoBehaviour
{
    [SerializeField] SoundLightSpawner _lights;
    [SerializeField] VisualsConnector  _visuals;
    [Tooltip("URP Lit, no emission: black until a sound light reaches it.")]
    [SerializeField] Material _material;
    [Tooltip("Cube side in metres, real size (not scaled with the room).")]
    [SerializeField, Min(0.01f)] float _size = 0.3f;

    readonly List<(AudioSource source, Renderer cube)> _cubes = new();

    void Start() // after SoundLightSpawner.Awake has filled its unlit list
    {
        if (_lights == null || _material == null)
        {
            Debug.LogWarning("[SoundCubeSpawner] Sound Light Spawner or material not assigned; no cubes.");
            return;
        }

        foreach (var source in _lights.Unlit)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Sound Cube - {source.name}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, worldPositionStays: false);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial       = _material;
            r.shadowCastingMode    = ShadowCastingMode.Off;
            r.receiveShadows       = false;
            r.lightProbeUsage      = LightProbeUsage.Off;      // no baked ambient: only sound lights
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled              = false;
            _cubes.Add((source, r));
        }
        Debug.Log($"[SoundCubeSpawner] {_cubes.Count} sound cubes.");

        if (_visuals != null) _visuals.OnFadeComplete += LogUnreachable;
    }

    void OnDestroy()
    {
        if (_visuals != null) _visuals.OnFadeComplete -= LogUnreachable;
    }

    void LateUpdate()
    {
        bool shown = _visuals == null || _visuals.FadeComplete;
        float floorY = transform.position.y; // Sound Cubes sits under Room Root, which is on the floor
        var rotation = transform.rotation;

        foreach (var (source, cube) in _cubes)
        {
            bool playing = shown && source != null && source.isActiveAndEnabled && source.isPlaying;
            if (!playing) { cube.enabled = false; continue; }

            var p      = source.transform.position;
            var centre = new Vector3(p.x, floorY + _size * 0.5f, p.z);
            // Unlit, a cube would still block lit floor behind it and show as a black silhouette.
            cube.enabled = LitNearby(centre);
            if (!cube.enabled) continue;

            var t = cube.transform;
            t.SetPositionAndRotation(centre, rotation);
            t.localScale = Vector3.one * _size;
        }
    }

    // True if a sound light that is on reaches this point.
    bool LitNearby(Vector3 point)
    {
        foreach (var light in _lights.Lights)
            if (light.IsOn && (light.transform.position - point).sqrMagnitude <= light.Range * light.Range)
                return true;
        return false;
    }

    // Cubes with no lit sound within one floor-pool radius can never be lit. Logged once the void is shown,
    // using the calibrated positions, so staff can see which ones will stay dark.
    void LogUnreachable()
    {
        float radius = _lights.PoolRadius;
        var dark = new List<string>();
        foreach (var (source, _) in _cubes)
        {
            if (source == null) continue;
            var p = source.transform.position;
            bool reachable = false;
            foreach (var light in _lights.Lights)
            {
                if (light.Source == null) continue;
                var d = light.Source.transform.position - p;
                d.y = 0f;
                if (d.sqrMagnitude <= radius * radius) { reachable = true; break; }
            }
            if (!reachable) dark.Add(source.name);
        }
        if (dark.Count > 0)
            Debug.Log($"[SoundCubeSpawner] {dark.Count} cubes have no sound light within {radius:0.##} m " +
                      $"(start positions; moving sounds may still pass): {string.Join(", ", dark)}");
    }
}
