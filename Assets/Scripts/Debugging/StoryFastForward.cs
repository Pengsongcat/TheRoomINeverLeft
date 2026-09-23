using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Development / staff tool, ported from V1's DevFastForward: hold the left trigger (keyboard T) to fast-forward the
// story, in the Editor and in builds. Only while the Story Director is running.
// The story waits on scaled time (Timelines on game time, guide fades on Time.deltaTime) but audio playback ignores
// Time.timeScale, so both are sped up: time scale = speed, and every sound's pitch = designed pitch × speed, so clips
// end when their Timelines expect. Unity caps pitch at 3; above that, audio lags its Timeline.
// Every AudioSource in the scene is covered (inactive included, read once at start). A head-locked ">> 3x" shows
// while it is active, on the Debug layer so no post-processing touches it.
public class StoryFastForward : MonoBehaviour
{
    const float MaxPitch = 3f;

    [Tooltip("Found in the scene when left empty (the Debugger is a prefab and cannot reference a scene object).")]
    [SerializeField] StoryDirector _director;
    [SerializeField, Range(1f, 10f)] float _speed = 3f;

    [Header("Indicator")]
    [Tooltip("Always-on-top text material (Debug Text Overlay).")]
    [SerializeField] Material _indicatorMaterial;
    [SerializeField] Vector3 _indicatorOffset = new Vector3(0f, 0.1f, 0.7f); // metres from the eye, head-locked
    [SerializeField] float   _fontSize        = 0.03f; // metres

    readonly Dictionary<AudioSource, float> _designedPitch = new();
    InputAction _hold;
    bool        _active;
    TextMeshPro _indicator;

    void Awake()
    {
        _hold = new InputAction("FastForward", InputActionType.Button);
        _hold.AddBinding("<XRController>{LeftHand}/triggerButton");
        _hold.AddBinding("<Keyboard>/t");
    }

    // Start, so every story part has loaded; inactive parts are included.
    void Start()
    {
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            _designedPitch[source] = source.pitch;
    }

    void OnEnable()  => _hold.Enable();

    void OnDisable()
    {
        _hold.Disable();
        SetActive(false);
    }

    void OnDestroy() => _hold.Dispose();

    void Update()
    {
        if (_director == null) _director = FindFirstObjectByType<StoryDirector>();
        bool want = _hold.IsPressed() && _director != null && _director.Running;
        if (want != _active) SetActive(want);
    }

    void SetActive(bool active)
    {
        if (active == _active) return;
        _active = active;

        Time.timeScale = active ? _speed : 1f;

        float pitchFactor = active ? _speed : 1f;
        foreach (var pair in _designedPitch)
            if (pair.Key != null) pair.Key.pitch = Mathf.Min(pair.Value * pitchFactor, MaxPitch);

        SetIndicatorVisible(active);
        Debug.Log($"[StoryFastForward] {(active ? $"Fast-forward {_speed:0.#}×" : "Normal speed")}");
    }

    void SetIndicatorVisible(bool visible)
    {
        if (visible && _indicator == null) BuildIndicator();
        if (_indicator != null) _indicator.gameObject.SetActive(visible);
    }

    void BuildIndicator()
    {
        var head = Camera.main != null ? Camera.main.transform : null;
        if (head == null) return;

        var go = new GameObject("Fast Forward Indicator") { layer = LayerMask.NameToLayer("Debug") };
        go.transform.SetParent(head, worldPositionStays: false);
        go.transform.localPosition = _indicatorOffset;
        go.transform.localRotation = Quaternion.identity;

        _indicator = go.AddComponent<TextMeshPro>();
        if (_indicatorMaterial != null) _indicator.fontSharedMaterial = _indicatorMaterial;
        _indicator.text             = $">> {_speed:0.#}x";
        _indicator.fontSize         = _fontSize * 10f; // TextMeshPro world text: fontSize 10 ≈ 1 m line height
        _indicator.alignment        = TextAlignmentOptions.Center;
        _indicator.textWrappingMode = TextWrappingModes.NoWrap;
        _indicator.rectTransform.sizeDelta = new Vector2(0.3f, 0.1f);
        _indicator.color            = new Color(1f, 0.85f, 0.1f);
    }
}
