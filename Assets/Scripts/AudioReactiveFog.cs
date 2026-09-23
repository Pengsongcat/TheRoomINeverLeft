using System.Collections.Generic;
using TRINL.Rendering;
using UnityEngine;

// Put on a Mesh Fog, or on any parent: drives every Mesh Fog on this object and its children (inactive ones too,
// found once in Awake) from one AudioSource, the same way Audio Flicker Light drives a light. Each fog keeps its
// own value, as a fraction of what the Animator / Timeline set this frame:
//   value = animated × distance level × (Base + Boost × loudness)
// Distance level is the sound's own falloff at the AudioListener (1 up close, 0 out of range), or always 1 with
// Track Distance off. Loudness (0..1) follows the sound's volume + spectral flux.
// Reacts via:
//   - Glow: fog colour brightness (hue kept).   - Density: how thick / opaque the fog is.
//   - Light Intensity: how strongly scene lights brighten it (needs Enable Lighting on the fog).
//   - Dissolve: how broken up it is; louder = more dissolved (clamped to 0..1).
// In a script at the Assets/Scripts root because Mesh Fog compiles into Assembly-CSharp, which TRINL.* cannot see.
public class AudioReactiveFog : MonoBehaviour
{
    public enum Channel { Glow, Density, LightIntensity, Dissolve }

    [SerializeField] AudioSource _source;
    [Tooltip("Which Mesh Fog value the sound drives, on every fog under this object.")]
    [SerializeField] Channel _reactsVia = Channel.Glow;
    [Tooltip("Fade with the headset's distance from the sound (none out of its range). Off: distance level is " +
             "always 1, so the value is Base + Boost × loudness wherever the visitor stands.")]
    [SerializeField] bool _trackDistance = true;

    [Header("Level (fractions of the animated value)")]
    [Tooltip("Level from distance alone, with no sound. 0 = none in silence.")]
    [Min(0f)] [SerializeField] float _base = 0.3f;
    [Tooltip("Extra level added at full loudness, on top of the base; also fades with distance.")]
    [Min(0f)] [SerializeField] float _boost = 0.7f;

    [Header("Audio → loudness")]
    [Tooltip("Loudness weight. Typical speech / music RMS is 0.05–0.25.")]
    [Min(0f)] [SerializeField] float _gain = 4f;
    [Tooltip("Spectral flux weight: short spikes when the spectrum rises, which gives the flicker. 0 = volume only.")]
    [Min(0f)] [SerializeField] float _fluxWeight = 2f;
    [Tooltip("Flux weight of the lowest frequency bin; rises linearly to the high weight.")]
    [Min(0f)] [SerializeField] float _lowBandWeight = 0.5f;
    [Min(0f)] [SerializeField] float _highBandWeight = 1.5f;
    [Min(0f)] [SerializeField] float _attackSeconds = 0.02f;
    [Min(0f)] [SerializeField] float _releaseSeconds = 0.15f;

    // One driven fog: the value the Animator / Timeline set, and the value this script wrote last frame.
    class Driven
    {
        public MeshFog Fog;
        public Color   BaseColour, WrittenColour;
        public float   BaseValue,  WrittenValue;
    }

    readonly LoudnessMeter _meter = new();
    readonly List<Driven>  _fogs  = new();

    AudioListener  _listener;
    AnimationCurve _customRolloff; // cached: GetCustomCurve allocates
    Channel _driven;               // channel written last frame, so a change in the inspector restores the old one

    void Awake()
    {
        foreach (var fog in GetComponentsInChildren<MeshFog>(includeInactive: true))
            _fogs.Add(new Driven { Fog = fog });
        if (_fogs.Count == 0) Debug.LogWarning($"[AudioReactiveFog] {name}: no Mesh Fog on this object or its children.", this);

        _customRolloff = LoudnessMeter.CustomRolloff(_source);
    }

    void OnEnable()
    {
        _driven = _reactsVia;
        foreach (var d in _fogs) Capture(d);
        _meter.Reset();
    }

    // Leave every fog at its animated value.
    void OnDisable()
    {
        foreach (var d in _fogs) Restore(d);
    }

    void LateUpdate()
    {
        if (_driven != _reactsVia)
        {
            foreach (var d in _fogs) Restore(d);
            _driven = _reactsVia;
            foreach (var d in _fogs) Capture(d);
        }

        float loudness = _meter.Update(ListenTo.Source, _source, _gain, _fluxWeight, _lowBandWeight, _highBandWeight,
                                       _attackSeconds, _releaseSeconds, Time.deltaTime);
        float level = DistanceLevel() * (_base + _boost * loudness);

        foreach (var d in _fogs)
        {
            if (d.Fog == null) continue;

            // Changed since we wrote it → the Animator / Timeline set a new value this frame.
            if (_driven == Channel.Glow)
            {
                if (d.Fog.fogColor != d.WrittenColour) d.BaseColour = d.Fog.fogColor;
                var c = d.BaseColour * level;
                c.a = d.BaseColour.a;
                d.WrittenColour = d.Fog.fogColor = c;
            }
            else
            {
                if (Read(d.Fog) != d.WrittenValue) d.BaseValue = Read(d.Fog);
                Write(d, d.BaseValue * level);
            }

            // Show it this frame, whether Mesh Fog's own LateUpdate ran before or after this one. An inactive fog
            // picks up the value when it is enabled.
            if (d.Fog.isActiveAndEnabled) d.Fog.Apply();
        }
    }

    void Capture(Driven d)
    {
        if (d.Fog == null) return;
        d.BaseColour = d.WrittenColour = d.Fog.fogColor;
        d.BaseValue  = d.WrittenValue  = Read(d.Fog);
    }

    void Restore(Driven d)
    {
        if (d.Fog == null) return;
        if (_driven == Channel.Glow) d.Fog.fogColor = d.BaseColour;
        else Write(d, d.BaseValue);
        if (d.Fog.isActiveAndEnabled) d.Fog.Apply();
    }

    float Read(MeshFog fog) => _driven switch
    {
        Channel.Density        => fog.density,
        Channel.LightIntensity => fog.lightIntensity,
        Channel.Dissolve       => fog.dissolve,
        _                      => 0f,
    };

    void Write(Driven d, float value)
    {
        switch (_driven)
        {
            case Channel.Density:        d.Fog.density        = value;                break;
            case Channel.LightIntensity: d.Fog.lightIntensity = value;                break;
            case Channel.Dissolve:       d.Fog.dissolve       = Mathf.Clamp01(value); break;
        }
        d.WrittenValue = Read(d.Fog);
    }

    // The source's own falloff at the listener, as in Audio Flicker Light. 1 when Track Distance is off.
    float DistanceLevel() =>
        _trackDistance ? LoudnessMeter.DistanceLevel(_source, _customRolloff, ref _listener) : 1f;
}
