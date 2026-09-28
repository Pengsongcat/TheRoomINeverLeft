using System.Collections;
using UnityEngine;

// Put on the Ending's End room. When it turns on, its lights fade up from black; if passthrough is on (MR route) it is
// drawn over the room and fades out at the same time, so the real room crossfades into the VR room. Passthrough is
// turned off once the fade is done.
public class EndRoomFadeIn : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float _duration = 5f;

    Light[] _lights;
    float[] _intensities;

    void Awake()
    {
        _lights = GetComponentsInChildren<Light>(includeInactive: true);
        _intensities = new float[_lights.Length];
        for (int i = 0; i < _lights.Length; i++) _intensities[i] = _lights[i].intensity;
    }

    void OnEnable() => StartCoroutine(FadeIn());

    IEnumerator FadeIn()
    {
        var passthrough = XRPlatformServices.Instance.PassthroughService;
        bool crossfade = passthrough.IsActive;
        float startOpacity = passthrough.Opacity;
        if (crossfade) passthrough.OnTop = true;

        for (float t = 0f; ; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / _duration);
            for (int i = 0; i < _lights.Length; i++) _lights[i].intensity = _intensities[i] * k;
            if (crossfade) passthrough.Opacity = startOpacity * (1f - k);
            if (k >= 1f) break;
            yield return null;
        }

        if (crossfade) passthrough.Disable();
    }
}
