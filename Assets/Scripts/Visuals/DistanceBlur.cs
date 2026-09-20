using UnityEngine;

// Distance blur settings (plan §8.7, Step 4f), in metres: close things slightly soft, far things fully blurred.
// Drives DistanceBlurFeature on VR_Renderer every frame (a depth-driven full-screen blur, so every object mixes
// with what is behind it). Near / far scale with the room (Sound Layout's range factor), like the sounds'
// audible ranges and floor pools. Off until the passthrough fade completes, so calibration stays untouched.
public class DistanceBlur : MonoBehaviour
{
    [Tooltip("Up to this distance (metres, at room scale 1) visuals get only the minimum blur.")]
    [SerializeField, Min(0.1f)] float _near = 1f;
    [Tooltip("From this distance (metres, at room scale 1) visuals are fully blurred.")]
    [SerializeField, Min(0.1f)] float _far = 6f;
    [Tooltip("Multiply near / far by the room's range factor, so a smaller room blurs sooner.")]
    [SerializeField] bool _scaleWithRoom = true;
    [SerializeField] Transform _soundLayout;
    [Tooltip("Blur when close, as a fraction of full blur (0 = sharp).")]
    [SerializeField, Range(0f, 1f)] float _minBlur = 0.2f;
    [Tooltip("Full blur as an angle (degrees): the blur radius seen by the eye. 10° ≈ a hand's width at arm's length.")]
    [SerializeField, Range(0f, 30f)] float _maxAngle = 6f;
    [Tooltip("Turns the blur on at fade complete. Empty = on from the start.")]
    [SerializeField] VisualsConnector _visuals;

    void Awake() => DistanceBlurFeature.On = false;

    void OnEnable()
    {
        if (_visuals != null) _visuals.OnFadeComplete += TurnOn;
        if (_visuals == null || _visuals.FadeComplete) TurnOn();
    }

    void OnDisable()
    {
        if (_visuals != null) _visuals.OnFadeComplete -= TurnOn;
        DistanceBlurFeature.On = false;
    }

    void TurnOn()
    {
        Push();
        DistanceBlurFeature.On = true;
    }

    void LateUpdate()
    {
        if (DistanceBlurFeature.On) Push();
    }

    void Push()
    {
        float k = _scaleWithRoom && _soundLayout != null ? SoundLightMath.RangeFactor(_soundLayout.lossyScale) : 1f;
        DistanceBlurFeature.Near     = _near * k;
        DistanceBlurFeature.Far      = _far * k;
        DistanceBlurFeature.MinBlur  = _minBlur;
        DistanceBlurFeature.MaxAngle = _maxAngle;
    }
}
