using UnityEngine;

/// <summary>Exposes a scalar cutoff for Timeline animation, including edit-mode preview.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioLowPassFilter))]
public sealed class TimelineLowPassFilter : MonoBehaviour
{
    [Tooltip("Animate this value in Timeline. Lower frequencies make the sound more muffled.")]
    [Range(10f, 22000f)]
    public float CutoffFrequency = 22000f;

    AudioLowPassFilter _filter;

    void OnEnable() => Apply();
    void OnValidate() => Apply();
    void OnDidApplyAnimationProperties() => Apply();

    // Also follows the restored animated value when Timeline preview is closed.
    void Update() => Apply();

    void Apply()
    {
        if (_filter == null) _filter = GetComponent<AudioLowPassFilter>();
        if (_filter != null)
            _filter.cutoffFrequency = Mathf.Clamp(CutoffFrequency, 10f, 22000f);
    }
}
