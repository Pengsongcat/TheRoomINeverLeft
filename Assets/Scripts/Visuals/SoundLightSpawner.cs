using System.Collections.Generic;
using UnityEngine;

// Creates one SoundLight per AudioSource under Sound Layout at startup (inactive sounds included), except ambient
// beds (visualizer uses the ambient material) and sounds marked NoSoundLight. Each frame: every light analyses its
// sound, the loudest N are allowed on (light budget), the rest ease out. Unscaled time, so fast-forward does
// not change the flicker. Each light glows at its sound and lights a pool on the floor below it; the pool size
// follows the room scale like the sounds' audible ranges do.
public class SoundLightSpawner : MonoBehaviour
{
    [SerializeField] Transform _soundLayout;
    [Tooltip("Sounds whose visualizer uses this material are ambient beds and get no light.")]
    [SerializeField] Material  _ambientMaterial;
    [Tooltip("Additive glow (TRINL/Sound Glow). Empty = no glow.")]
    [SerializeField] Material  _glowMaterial;
    [Tooltip("At most this many lights on at once, loudest first.")]
    [SerializeField, Min(0)] int _maxLights = 8;
    [SerializeField] SoundLightSettings _settings = new();

    readonly List<SoundLight>  _lights  = new();
    readonly List<AudioSource> _unlit   = new();
    readonly List<SoundLight>  _wanting = new(); // reused each frame for the budget sort
    readonly HashSet<SoundLight> _allowed = new();
    AudioListener _listener;

    static readonly System.Comparison<SoundLight> ByDesiredDescending = (a, b) => b.Desired.CompareTo(a.Desired);

    public IReadOnlyList<SoundLight>  Lights => _lights;
    public IReadOnlyList<AudioSource> Unlit  => _unlit; // sounds skipped above (ambient / NoSoundLight); filled in Awake

    void Awake()
    {
        if (_soundLayout == null)
        {
            Debug.LogWarning("[SoundLightSpawner] No Sound Layout assigned.");
            return;
        }

        foreach (var source in _soundLayout.GetComponentsInChildren<AudioSource>(includeInactive: true))
        {
            if (source.GetComponent<NoSoundLight>() != null || IsAmbient(source)) { _unlit.Add(source); continue; }

            var go = new GameObject($"Sound Light - {source.name}");
            go.transform.SetParent(transform, worldPositionStays: false);
            var light = go.AddComponent<SoundLight>();
            light.Init(source, _settings, _glowMaterial);
            _lights.Add(light);
        }
        Debug.Log($"[SoundLightSpawner] {_lights.Count} sound lights, {_unlit.Count} sounds without a light.");
    }

    // The sound's own visualizer: renderers below it whose nearest AudioSource is this one (not a nested sound).
    bool IsAmbient(AudioSource source)
    {
        if (_ambientMaterial == null) return false;
        foreach (var r in source.GetComponentsInChildren<Renderer>(includeInactive: true))
            if (r.sharedMaterial == _ambientMaterial && r.GetComponentInParent<AudioSource>(includeInactive: true) == source)
                return true;
        return false;
    }

    void Update()
    {
        if (_lights.Count == 0) return;
        if (_listener == null) _listener = FindFirstObjectByType<AudioListener>();
        if (_listener == null) return;
        var listenerPosition = _listener.transform.position;

        _wanting.Clear();
        foreach (var light in _lights)
        {
            light.Analyse(listenerPosition);
            if (light.Desired > 0f) _wanting.Add(light);
        }

        _allowed.Clear();
        if (_wanting.Count > _maxLights) _wanting.Sort(ByDesiredDescending);
        for (int i = 0; i < _wanting.Count && i < _maxLights; i++) _allowed.Add(_wanting[i]);

        float rangeFactor = RangeFactor();
        float floorY = transform.position.y; // Sound Lights sits under Room Root, which is on the floor
        float dt = Time.unscaledDeltaTime;
        foreach (var light in _lights)
            light.Apply(_allowed.Contains(light), rangeFactor, floorY, dt, listenerPosition);
    }

    // Current floor-pool radius in metres (room-scaled).
    public float PoolRadius => _settings.poolRadius * RangeFactor();

    float RangeFactor() => SoundLightMath.RangeFactor(_soundLayout.lossyScale);
}
