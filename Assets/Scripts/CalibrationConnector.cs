using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Wires CalibrationFlow.OnLayoutReady → RoomLayoutApplier + AudioRangeScaler + player floor height,
// and hands the fitted sound positions to RoomDebugView for the Confirming phase.
// Wires CalibrationFlow.OnCalibrationAccepted (staff pressed Start) → dim passthrough + activate the story.
// Must live in Assembly-CSharp (not an asmdef) to reference AudioRangeScaler and the V1 sound scripts.
public class CalibrationConnector : MonoBehaviour
{
    [SerializeField] CalibrationFlow   _flow;
    [SerializeField] RoomLayoutApplier _applier;
    [SerializeField] AudioRangeScaler  _scaler;

    [Header("Start")]
    [Tooltip("Switched on when staff press Start (e.g. Onboarding). The story chain continues from there.")]
    [SerializeField] GameObject[] _activateOnStart;
    [Tooltip("Passthrough opacity during the story. 1 = full room view, 0 = black.")]
    [SerializeField, Range(0f, 1f)] float _storyPassthroughOpacity = 0.15f;
    [Tooltip("Passthrough brightness during the story. 0 = unchanged, -1 = black. Darkens the camera feed only.")]
    [SerializeField, Range(-1f, 0f)] float _storyPassthroughBrightness = -0.5f;
    [SerializeField, Min(0f)] float _passthroughFadeSeconds = 2f;

    [Header("Player")]
    [SerializeField] FollowXZ _feetCollider;
    [SerializeField] float    _feetHeightAboveFloor = 0.1f;

    [Header("Debug View")]
    [SerializeField] RoomDebugView   _debugView;
    [SerializeField] Transform       _soundLayout;
    [SerializeField] SoundVisualizer _soundVisualizer; // marker colors match the sounds' own visualizers

    void OnEnable()
    {
        _flow.OnLayoutReady         += OnLayoutReady;
        _flow.OnCalibrationAccepted += OnStart;
    }

    void OnDisable()
    {
        _flow.OnLayoutReady         -= OnLayoutReady;
        _flow.OnCalibrationAccepted -= OnStart;
    }

    void OnStart(RoomRect rect)
    {
        StartCoroutine(FadePassthrough(_storyPassthroughOpacity, _storyPassthroughBrightness, _passthroughFadeSeconds));

        if (_activateOnStart == null || _activateOnStart.Length == 0)
            Debug.LogWarning("[CalibrationConnector] Nothing to activate on Start; assign Onboarding.");
        else
            foreach (var go in _activateOnStart)
                if (go != null) go.SetActive(true);
    }

    static IEnumerator FadePassthrough(float targetOpacity, float targetBrightness, float seconds)
    {
        var passthrough = XRPlatformServices.Instance.PassthroughService;
        float startOpacity    = passthrough.Opacity;
        float startBrightness = passthrough.Brightness;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            passthrough.Opacity    = Mathf.Lerp(startOpacity, targetOpacity, t / seconds);
            passthrough.Brightness = Mathf.Lerp(startBrightness, targetBrightness, t / seconds);
            yield return null;
        }
        passthrough.Opacity    = targetOpacity;
        passthrough.Brightness = targetBrightness;
    }

    void OnLayoutReady(RoomRect rect)
    {
        _applier.Apply(rect);
        _scaler.Scale(_applier.LastFit.rangeFactor);

        if (_feetCollider != null)
            _feetCollider.fixedY = rect.floorY + _feetHeightAboveFloor;

        if (_debugView != null && _soundLayout != null)
            _debugView.SetSoundMarkers(CollectSoundMarkers(_soundLayout));
    }

    Color? SoundColor(Component sound) =>
        _soundVisualizer != null && _soundVisualizer.TryGetSoundColor(sound.transform, out var c) ? c : null;

    // Guides get a ring at their scaled trigger reach, the Start Point at its scaled trigger distance.
    // Every other AudioSource gets a plain marker. Inactive objects are included (e.g. Onboarding).
    List<SoundMarker> CollectSoundMarkers(Transform soundLayout)
    {
        var markers = new List<SoundMarker>();
        var marked  = new HashSet<GameObject>();

        foreach (var guide in soundLayout.GetComponentsInChildren<GuidingSoundController>(includeInactive: true))
        {
            markers.Add(new SoundMarker(guide.transform.position, SoundMarkerKind.Guide,
                                        HorizontalTriggerRadius(guide.GetComponent<Collider>()), SoundColor(guide)));
            marked.Add(guide.gameObject);
        }

        foreach (var relay in soundLayout.GetComponentsInChildren<ProximityEventRelay>(includeInactive: true))
        {
            var point = relay.startPoint != null ? relay.startPoint : relay.transform;
            markers.Add(new SoundMarker(point.position, SoundMarkerKind.StartPoint, relay.triggerDistance));
            marked.Add(point.gameObject);
        }

        foreach (var source in soundLayout.GetComponentsInChildren<AudioSource>(includeInactive: true))
        {
            if (marked.Contains(source.gameObject)) continue;
            markers.Add(new SoundMarker(source.transform.position, SoundMarkerKind.Sound, color: SoundColor(source)));
        }

        return markers;
    }

    // World-space horizontal reach of a trigger collider. Computed from the collider's size and
    // lossy scale, because Collider.bounds is empty while the collider is disabled (guides start disabled).
    static float HorizontalTriggerRadius(Collider collider)
    {
        if (collider == null) return 0f;
        var s = collider.transform.lossyScale;
        float horizontalScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        return collider switch
        {
            CapsuleCollider capsule => capsule.radius * horizontalScale,
            SphereCollider  sphere  => sphere.radius * Mathf.Max(horizontalScale, Mathf.Abs(s.y)),
            BoxCollider     box     => Mathf.Max(box.size.x * Mathf.Abs(s.x), box.size.z * Mathf.Abs(s.z)) * 0.5f,
            _                       => 0f,
        };
    }
}
