using UnityEngine;

// Put on any Light. Drives its brightness from an AudioSource in two parts, each a fraction of the light's current
// intensity (whatever the Animator / Timeline set this frame):
//   - Base: Base Brightness × distance level, where the distance level is the sound's own falloff at the
//     AudioListener (its rolloff at its current, room-scaled min / max distance). 1 up close, 0 out of range.
//   - Audio boost: Audio Boost × loudness × distance level, added on top; loudness (0..1) follows the sound's
//     volume + spectral flux, like SoundLight, so the light brightens as the sound gets louder.
// intensity = animated × distance level × (Base Brightness + Audio Boost × loudness). Out of the sound's range the
// light is black. Runs in LateUpdate, after animation has written the intensity; if nothing wrote it this frame,
// the last animated value is kept.
// The source is read whether or not isPlaying is set (audio played by a Timeline Audio Track does not set it).
// A Volumetric Light Beam on the same object that takes its intensity from the light is told to re-read it, since
// it otherwise copies the unmodified value when animation writes the light.
[RequireComponent(typeof(Light))]
public class AudioFlickerLight : MonoBehaviour
{
    const int    Samples           = 256;
    const string BeamReadFromLight = "AssignPropertiesFromAttachedSpotLight"; // VLB, in Assembly-CSharp so not referenced

    [SerializeField] AudioSource _source;

    [Header("Brightness (fractions of the animated intensity)")]
    [Tooltip("Brightness from distance alone: this much when right at the sound, fading to 0 at the edge of its range.")]
    [Min(0f)] [SerializeField] float _baseBrightness = 0.3f;
    [Tooltip("Extra brightness added at full loudness, on top of the base; also fades with distance.")]
    [Min(0f)] [SerializeField] float _audioBoost = 0.7f;

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

    readonly float[] _samples      = new float[Samples];
    readonly float[] _spectrum     = new float[Samples];
    readonly float[] _prevSpectrum = new float[Samples];

    Light          _light;
    AudioListener  _listener;
    AnimationCurve _customRolloff; // cached: GetCustomCurve allocates
    float _base;     // intensity the Animator / Timeline set
    float _written;  // intensity this script wrote last frame
    float _loudness; // smoothed, 0..1
    bool  _primed;   // _prevSpectrum holds a real frame

    void Awake()
    {
        _light = GetComponent<Light>();
        if (_source != null && _source.rolloffMode == AudioRolloffMode.Custom)
            _customRolloff = _source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
    }

    void OnEnable()
    {
        _base = _written = _light.intensity;
        _loudness = 0f;
        _primed = false;
    }

    // Leave the light at its animated intensity.
    void OnDisable() => _light.intensity = _base;

    void LateUpdate()
    {
        // Changed since we wrote it → the Animator / Timeline set a new value this frame.
        if (_light.intensity != _written) _base = _light.intensity;

        _loudness = SoundLightMath.Smooth(_loudness, Loudness(), _attackSeconds, _releaseSeconds, Time.deltaTime);
        _written = _light.intensity = _base * DistanceLevel() * (_baseBrightness + _audioBoost * _loudness);
        SendMessage(BeamReadFromLight, SendMessageOptions.DontRequireReceiver);
    }

    // 0..1 loudness of the sound this frame; silence or not playing reads as 0.
    float Loudness()
    {
        if (_source == null || !_source.isActiveAndEnabled) return 0f;

        _source.GetOutputData(_samples, 0);
        _source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        if (!_primed) System.Array.Copy(_spectrum, _prevSpectrum, Samples); // no flux spike on the first frame
        _primed = true;

        float rms  = SoundLightMath.Rms(_samples);
        float flux = SoundLightMath.SpectralFlux(_spectrum, _prevSpectrum, _lowBandWeight, _highBandWeight);
        return SoundLightMath.Target(rms, flux, _gain, _fluxWeight, heard: 1f, maxIntensity: 1f);
    }

    // The source's own falloff at the listener, like SoundLight: 1 up close, 0 at and beyond its max distance
    // (checked explicitly, since the Logarithmic curve never reaches 0 on its own).
    float DistanceLevel()
    {
        if (_source == null) return 0f;
        if (_listener == null) _listener = FindFirstObjectByType<AudioListener>();
        if (_listener == null) return 0f;

        float distance = Vector3.Distance(_source.transform.position, _listener.transform.position);
        float rolloff = distance >= _source.maxDistance ? 0f : _source.rolloffMode switch
        {
            AudioRolloffMode.Custom => _customRolloff != null && _source.maxDistance > 0f
                                       ? _customRolloff.Evaluate(distance / _source.maxDistance) : 1f,
            AudioRolloffMode.Linear => SoundLightMath.LinearRolloff(distance, _source.minDistance, _source.maxDistance),
            _                       => SoundLightMath.LogarithmicRolloff(distance, _source.minDistance),
        };
        return SoundLightMath.Heard(rolloff, _source.spatialBlend);
    }
}
