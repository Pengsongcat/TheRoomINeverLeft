using System;
using System.Collections;
using UnityEngine;

// Start → full VR. When staff press Start, passthrough fades from its current opacity to 0, then stops,
// and the void appears. The void is opaque, so it stays hidden during the fade; showing it early would cut
// the room off at once instead of fading it. Runs on unscaled time, so DevFastForward does not shorten it.
public class VisualsConnector : MonoBehaviour
{
    [SerializeField] CalibrationFlow _flow;
    [SerializeField] VoidBox         _void;
    [SerializeField, Min(0f)] float  _fadeSeconds = 5f;
    [Tooltip("Shape of the fade. 1 = linear opacity, which looks slow at the end because the eye still sees a " +
             "faint room at low opacity. Higher = the dim tail passes quicker, so it reads as an even fade.")]
    [SerializeField, Range(1f, 4f)] float _fadeCurve = 2f;
    [Tooltip("Stop passthrough once it is fully faded. The boundary may then show near its edges, " +
             "because it is only suppressed while passthrough is active.")]
    [SerializeField] bool            _stopPassthroughAfterFade = true;

    public event Action OnFadeComplete; // the room is gone; the void is shown

    public bool FadeComplete { get; private set; }

    void OnEnable()  => _flow.OnCalibrationAccepted += OnStart;
    void OnDisable() => _flow.OnCalibrationAccepted -= OnStart;

    void OnStart(RoomRect _) => StartCoroutine(FadeToVoid());

    IEnumerator FadeToVoid()
    {
        var passthrough = XRPlatformServices.Instance.PassthroughService;
        float startOpacity = passthrough.Opacity;
        for (float t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
        {
            passthrough.Opacity = startOpacity * Mathf.Pow(1f - t / _fadeSeconds, _fadeCurve);
            yield return null;
        }
        passthrough.Opacity = 0f;
        if (_stopPassthroughAfterFade) passthrough.Disable();

        if (_void != null) _void.SetVisible(true);
        FadeComplete = true;
        Debug.Log("[VisualsConnector] Passthrough faded out; void shown.");
        OnFadeComplete?.Invoke();
    }
}
