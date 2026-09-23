using UnityEngine;

// Put on any Light. Drives its brightness from an AudioSource in two parts, each a fraction of the light's current
// intensity (whatever the Animator / Timeline set this frame):
//   - Base: Base Brightness × distance level, where the distance level is the sound's own falloff at the
//     AudioListener (its rolloff at its current, room-scaled min / max distance). 1 up close, 0 out of range.
//   - Audio boost: Audio Boost × loudness × distance level, added on top; loudness (0..1) follows the sound's
//     volume + spectral flux (Loudness Meter), so the light brightens as the sound gets louder.
// intensity = animated × distance level × (Base Brightness + Audio Boost × loudness). Out of the sound's range the
// light is black. With Track Distance off, the distance level is always 1.
// Listen To Headset: loudness is the AudioListener's whole mix (every sound the visitor hears, after Steam Audio's
// distance falloff) instead of one source, and the distance level is 1.
// Runs in LateUpdate, after animation has written the intensity; if nothing wrote it this frame, the last animated
// value is kept.
// A Volumetric Light Beam on the same object that takes its intensity from the light is told to re-read it, since
// it otherwise copies the unmodified value when animation writes the light.
[RequireComponent(typeof(Light))]
public class AudioFlickerLight : MonoBehaviour
{
    const string BeamReadFromLight = "AssignPropertiesFromAttachedSpotLight"; // VLB, in Assembly-CSharp so not referenced

    [Tooltip("Source: the loudness of the Source below. Headset: the loudness of everything the headset hears (the " +
             "AudioListener's mix, all sounds together, already quieter with distance); Source and Track Distance are " +
             "then not used.")]
    [SerializeField] ListenTo _listenTo = ListenTo.Source;
    [SerializeField] AudioSource _source;
    [Tooltip("Fade with the headset's distance from the sound (black out of its range). Off: distance level is " +
             "always 1, so the light is Base Brightness + Audio Boost × loudness wherever the visitor stands.")]
    [SerializeField] bool _trackDistance = true;

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

    readonly LoudnessMeter _meter = new();

    Light          _light;
    AudioListener  _listener;
    AnimationCurve _customRolloff; // cached: GetCustomCurve allocates
    float _base;     // intensity the Animator / Timeline set
    float _written;  // intensity this script wrote last frame

    void Awake()
    {
        _light = GetComponent<Light>();
        _customRolloff = LoudnessMeter.CustomRolloff(_source);
    }

    void OnEnable()
    {
        _base = _written = _light.intensity;
        _meter.Reset();
    }

    // Leave the light at its animated intensity.
    void OnDisable() => _light.intensity = _base;

    void LateUpdate()
    {
        // Changed since we wrote it → the Animator / Timeline set a new value this frame.
        if (_light.intensity != _written) _base = _light.intensity;

        float loudness = _meter.Update(_listenTo, _source, _gain, _fluxWeight, _lowBandWeight, _highBandWeight,
                                       _attackSeconds, _releaseSeconds, Time.deltaTime);
        _written = _light.intensity = _base * DistanceLevel() * (_baseBrightness + _audioBoost * loudness);
        SendMessage(BeamReadFromLight, SendMessageOptions.DontRequireReceiver);
    }

    // 1 when Track Distance is off, or when listening to the headset, whose mix already fades with distance.
    float DistanceLevel() =>
        !_trackDistance || _listenTo == ListenTo.Headset
            ? 1f
            : LoudnessMeter.DistanceLevel(_source, _customRolloff, ref _listener);
}
