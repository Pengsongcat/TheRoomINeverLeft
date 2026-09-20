using System;
using UnityEngine;

[Serializable]
public class SoundLightSettings
{
    public Color color = new(1f, 0.82f, 0.62f); // warm white (~3000 K)
    [Min(0f)] public float maxIntensity = 6f;

    [Header("Audio → brightness")]
    [Tooltip("Loudness weight. Typical speech / music RMS is 0.05–0.25.")]
    [Min(0f)] public float gain = 4f;
    [Tooltip("Spectral flux weight: short spikes when the spectrum rises, which gives the flicker.")]
    [Min(0f)] public float fluxWeight = 2f;
    [Tooltip("Flux weight of the lowest frequency bin; rises linearly to the high weight.")]
    [Min(0f)] public float lowBandWeight = 0.5f;
    [Min(0f)] public float highBandWeight = 1.5f;
    [Min(0f)] public float attackSeconds = 0.02f;
    [Min(0f)] public float releaseSeconds = 0.15f;
    [Tooltip("Dim with the sound's own falloff at the listener, so it is dark when out of earshot.")]
    public bool heardDistance = true;

    [Header("Floor pool")]
    [Tooltip("Height of the light above the floor, straight below the sound. Metres, real size (not scaled).")]
    [Min(0.05f)] public float height = 1f;
    [Tooltip("Radius of the lit circle on the floor at room scale 1. Multiplied by the room's range factor.")]
    [Min(0f)] public float poolRadius = 2.5f;

    [Header("Glow")]
    [Tooltip("Glow diameter in metres at the sound itself (real size, not scaled with the room).")]
    [Min(0f)] public float glowSize = 0.12f;
    [Min(0f)] public float glowStrength = 1f;
}

// Per sound: a small glow at the sound itself, and a warm point light held at a fixed height above the floor
// straight below the sound, so it throws a pool of light on the floor at any sound height (moving narrators
// included). Both driven by the same brightness. Driven by SoundLightSpawner: Analyse → budget → Apply,
// on unscaled time.
public class SoundLight : MonoBehaviour
{
    const int   Samples  = 256;
    const float OffBelow = 0.01f; // intensity under which light and glow are switched off

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    readonly float[] _samples      = new float[Samples];
    readonly float[] _spectrum     = new float[Samples];
    readonly float[] _prevSpectrum = new float[Samples];

    AudioSource           _source;
    SoundLightSettings    _settings;
    AnimationCurve        _customRolloff; // cached: GetCustomCurve allocates
    Light                 _light;
    Renderer              _glow;
    MaterialPropertyBlock _block;
    bool                  _wasPlaying;

    public AudioSource Source  => _source;
    public float       Desired { get; private set; } // target brightness this frame, before the budget
    public float       Current { get; private set; }
    public bool        IsOn    => _light.enabled;
    public float       Range   => _light.range;

    public void Init(AudioSource source, SoundLightSettings settings, Material glowMaterial)
    {
        _source   = source;
        _settings = settings;
        if (source.rolloffMode == AudioRolloffMode.Custom)
            _customRolloff = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);

        _light = gameObject.AddComponent<Light>();
        _light.type      = LightType.Point;
        _light.shadows   = LightShadows.None;
        _light.intensity = 0f;
        _light.enabled   = false;

        if (glowMaterial != null)
        {
            // Sibling of the light, not a child: the light sits below the sound, the glow at the sound.
            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = $"{name} Glow";
            Destroy(glow.GetComponent<Collider>());
            glow.transform.SetParent(transform.parent, worldPositionStays: false);
            _glow = glow.GetComponent<Renderer>();
            _glow.sharedMaterial       = glowMaterial;
            _glow.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glow.receiveShadows       = false;
            _glow.lightProbeUsage      = UnityEngine.Rendering.LightProbeUsage.Off;
            _glow.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _glow.enabled              = false;
            _block = new MaterialPropertyBlock();
        }
    }

    bool IsPlaying => _source != null && _source.isActiveAndEnabled && _source.isPlaying;

    // Brightness this sound asks for: loudness + flicker, times how loud it is at the listener.
    public void Analyse(Vector3 listenerPosition)
    {
        if (!IsPlaying)
        {
            Desired     = 0f;
            _wasPlaying = false;
            return;
        }

        _source.GetOutputData(_samples, 0);
        _source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        if (!_wasPlaying) Array.Copy(_spectrum, _prevSpectrum, Samples); // no flux spike from silence on the first frame
        _wasPlaying = true;

        float rms   = SoundLightMath.Rms(_samples);
        float flux  = SoundLightMath.SpectralFlux(_spectrum, _prevSpectrum, _settings.lowBandWeight, _settings.highBandWeight);
        float heard = _settings.heardDistance ? Heard(Vector3.Distance(_source.transform.position, listenerPosition)) : 1f;
        Desired = SoundLightMath.Target(rms, flux, _settings.gain, _settings.fluxWeight, heard, _settings.maxIntensity);
    }

    // The source's own falloff at the listener, using its current (room-scaled) min / max distance.
    float Heard(float distance)
    {
        float rolloff = _source.rolloffMode switch
        {
            AudioRolloffMode.Custom      => _customRolloff != null && _source.maxDistance > 0f
                                            ? _customRolloff.Evaluate(distance / _source.maxDistance) : 1f,
            AudioRolloffMode.Linear      => SoundLightMath.LinearRolloff(distance, _source.minDistance, _source.maxDistance),
            _                            => SoundLightMath.LogarithmicRolloff(distance, _source.minDistance),
        };
        return SoundLightMath.Heard(rolloff, _source.spatialBlend);
    }

    // allowed = inside the light budget. Lights outside it ease out like a stopped sound.
    public void Apply(bool allowed, float rangeFactor, float floorY, float deltaTime, Vector3 viewerPosition)
    {
        float target = allowed ? Desired : 0f;
        Current = SoundLightMath.Smooth(Current, target, _settings.attackSeconds, _settings.releaseSeconds, deltaTime);
        bool on = Current > OffBelow;

        _light.enabled = on;
        if (_glow != null) _glow.enabled = on;
        if (!on || _source == null) return;

        var soundPosition = _source.transform.position;
        transform.position = new Vector3(soundPosition.x, floorY + _settings.height, soundPosition.z);
        _light.color     = _settings.color;
        _light.intensity = Current;
        _light.range     = SoundLightMath.RangeForPool(_settings.poolRadius * rangeFactor, _settings.height);

        if (_glow == null) return;
        var glowTransform = _glow.transform;
        glowTransform.position   = soundPosition;
        glowTransform.localScale = Vector3.one * _settings.glowSize;
        var toGlow = soundPosition - viewerPosition;
        if (toGlow.sqrMagnitude > 1e-6f) glowTransform.rotation = Quaternion.LookRotation(toGlow); // billboard
        float level = _settings.maxIntensity > 0f ? Current / _settings.maxIntensity : 0f;
        var c = _settings.color * (level * _settings.glowStrength);
        c.a = Mathf.Clamp01(level * _settings.glowStrength); // alpha shows the glow over fading passthrough
        _block.SetColor(BaseColorId, c);
        _glow.SetPropertyBlock(_block);
    }
}
