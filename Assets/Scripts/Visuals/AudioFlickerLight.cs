using UnityEngine;

// Drives brightness from two independently measured sources: Source 1 contributes 70%, Source 2 30%.
// Each source applies its own distance falloff to its base brightness and audio boost before mixing.
// intensity = animated * (Min Brightness + 0.7 * source1Contribution + 0.3 * source2Contribution).
// Missing sources contribute zero; weights are never redistributed. Headset mode uses the whole mix once.
// Runs after animation, retaining the last animated intensity, and refreshes any attached VLB beam.
[RequireComponent(typeof(Light))]
public class AudioFlickerLight : MonoBehaviour
{
    const string BeamReadFromLight = "AssignPropertiesFromAttachedSpotLight"; // VLB, in Assembly-CSharp so not referenced

    [Tooltip("Source: mix Source 1 (70%) and Source 2 (30%). Headset: use the AudioListener's whole mix; " +
             "both source fields and Track Distance are ignored.")]
    [SerializeField] ListenTo _listenTo = ListenTo.Source;
    // Keep the serialized field name to preserve existing scene/prefab references and overrides.
    [InspectorName("Source 1 (70%)")]
    [Tooltip("Contributes 70% of the audio-driven brightness. Missing sources contribute zero.")]
    [SerializeField] AudioSource _source;
    [InspectorName("Source 2 (30%)")]
    [Tooltip("Contributes 30% of the audio-driven brightness. Missing sources contribute zero.")]
    [SerializeField] AudioSource _source2;
    [Tooltip("Fade with the headset's distance from the sound (black out of its range). Off: distance level is " +
             "always 1, so the light is Base Brightness + Audio Boost × loudness wherever the visitor stands.")]
    [SerializeField] bool _trackDistance = true;

    [Header("Brightness (fractions of the animated intensity)")]
    [Tooltip("Always there, whatever the distance or loudness: a faint glow in silence and out of range. 0 = none.")]
    [Min(0f)] [SerializeField] float _minBrightness = 0f;
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
    readonly LoudnessMeter _meter2 = new();
    readonly LoudnessMeter _headsetMeter = new();

    Light          _light;
    AudioListener  _listener;
    AnimationCurve _customRolloff; // cached: GetCustomCurve allocates
    AnimationCurve _customRolloff2;
    AudioSource _cachedSource;
    AudioSource _cachedSource2;
    float _base;     // intensity the Animator / Timeline set
    float _written;  // intensity this script wrote last frame

    void Awake()
    {
        _light = GetComponent<Light>();
        RefreshSources();
    }

    void OnEnable()
    {
        _base = _written = _light.intensity;
        _meter.Reset();
        _meter2.Reset();
        _headsetMeter.Reset();
    }

    // Leave the light at its animated intensity.
    void OnDisable() => _light.intensity = _base;

    void LateUpdate()
    {
        // Changed since we wrote it → the Animator / Timeline set a new value this frame.
        if (_light.intensity != _written) _base = _light.intensity;

        RefreshSources();
        float contribution;
        if (_listenTo == ListenTo.Headset)
        {
            float loudness = Measure(_headsetMeter, ListenTo.Headset, null);
            contribution = _baseBrightness + _audioBoost * loudness;
        }
        else
        {
            contribution = 0.7f * SourceContribution(_source, _customRolloff, _meter)
                         + 0.3f * SourceContribution(_source2, _customRolloff2, _meter2);
        }
        _written = _light.intensity = _base * (_minBrightness + contribution);
        SendMessage(BeamReadFromLight, SendMessageOptions.DontRequireReceiver);
    }

    float Measure(LoudnessMeter meter, ListenTo listenTo, AudioSource source) =>
        meter.Update(listenTo, source, _gain, _fluxWeight, _lowBandWeight, _highBandWeight,
                     _attackSeconds, _releaseSeconds, Time.deltaTime);

    float SourceContribution(AudioSource source, AnimationCurve rolloff, LoudnessMeter meter)
    {
        if (source == null) return 0f;
        float loudness = Measure(meter, ListenTo.Source, source);
        float distance = _trackDistance ? LoudnessMeter.DistanceLevel(source, rolloff, ref _listener) : 1f;
        return distance * (_baseBrightness + _audioBoost * loudness);
    }

    // Refresh cached falloff and analysis history when an Inspector/runtime source assignment changes.
    void RefreshSources()
    {
        if (_cachedSource != _source)
        {
            _cachedSource = _source;
            _customRolloff = LoudnessMeter.CustomRolloff(_source);
            _meter.Reset();
        }
        if (_cachedSource2 != _source2)
        {
            _cachedSource2 = _source2;
            _customRolloff2 = LoudnessMeter.CustomRolloff(_source2);
            _meter2.Reset();
        }
    }
}
