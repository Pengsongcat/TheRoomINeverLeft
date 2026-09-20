using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

// Development tool: hold the left trigger (keyboard T in the Editor) to fast-forward the story.
// The story waits on scaled time (WaitForSeconds, Time.deltaTime fades, Timelines on game time) but audio playback
// ignores Time.timeScale, so both are sped up: time scale = speed, and every sound's pitch = designed pitch × speed,
// so clips end when their fade-out timers expect. Unity caps pitch at 3; above that, audio lags its timers.
// Timelines on unscaled time (e.g. Timeline 4) ignore the time scale, so their graphs are sped up directly.
// Only while the story runs, and only in the Editor and development builds.
public class DevFastForward : MonoBehaviour
{
    const float MaxPitch = 3f;

    [SerializeField] InputActionReference _holdAction;
    [SerializeField] CalibrationFlow      _flow;
    [Tooltip("Every AudioSource under it (inactive included) is pitched up while fast-forwarding.")]
    [SerializeField] Transform            _soundLayout;
    [SerializeField, Range(1f, 10f)] float _speed = 3f;
    [Tooltip("Off = never fast-forward, even in development builds.")]
    [SerializeField] bool                 _allow = true;

    [Header("Indicator")]
    [SerializeField] Vector3 _indicatorOffset = new Vector3(0f, 0.1f, 0.7f); // metres from the eye, head-locked
    [SerializeField] float   _fontSize        = 0.03f; // metres

    readonly Dictionary<AudioSource, float> _designedPitch     = new();
    readonly List<PlayableDirector>         _unscaledDirectors = new();
    readonly HashSet<PlayableDirector>      _spedUpDirectors   = new();
    bool        _active;
    TextMeshPro _indicator;

    bool Allowed => _allow && Debug.isDebugBuild;

    void Awake()
    {
        if (_soundLayout == null) return;
        foreach (var source in _soundLayout.GetComponentsInChildren<AudioSource>(includeInactive: true))
            _designedPitch[source] = source.pitch;
        foreach (var director in _soundLayout.GetComponentsInChildren<PlayableDirector>(includeInactive: true))
            if (director.timeUpdateMode == DirectorUpdateMode.UnscaledGameTime) _unscaledDirectors.Add(director);
    }

    void OnEnable()
    {
        if (_holdAction != null && Allowed) _holdAction.action.Enable();
    }

    void OnDisable() => SetActive(false);

    void Update()
    {
        bool want = Allowed
                    && _holdAction != null && _holdAction.action.IsPressed()
                    && _flow != null && _flow.CurrentState == CalibrationFlow.State.Running;
        if (want != _active) SetActive(want);
        if (_active) SpeedUpUnscaledDirectors(); // a Timeline may start playing while the trigger is held
    }

    void SpeedUpUnscaledDirectors()
    {
        foreach (var director in _unscaledDirectors)
        {
            if (director == null || _spedUpDirectors.Contains(director) || !director.playableGraph.IsValid()) continue;
            director.playableGraph.GetRootPlayable(0).SetSpeed(_speed);
            _spedUpDirectors.Add(director);
        }
    }

    void RestoreUnscaledDirectors()
    {
        foreach (var director in _spedUpDirectors)
            if (director != null && director.playableGraph.IsValid())
                director.playableGraph.GetRootPlayable(0).SetSpeed(1);
        _spedUpDirectors.Clear();
    }

    void SetActive(bool active)
    {
        if (active == _active) return;
        _active = active;

        Time.timeScale = active ? _speed : 1f;

        float pitchFactor = active ? _speed : 1f;
        foreach (var pair in _designedPitch)
            if (pair.Key != null) pair.Key.pitch = Mathf.Min(pair.Value * pitchFactor, MaxPitch);

        if (active) SpeedUpUnscaledDirectors();
        else        RestoreUnscaledDirectors();

        SetIndicatorVisible(active);
        Debug.Log($"[DevFastForward] {(active ? $"Fast-forward {_speed:0.#}×" : "Normal speed")}");
    }

    void SetIndicatorVisible(bool visible)
    {
        if (visible && _indicator == null) BuildIndicator();
        if (_indicator != null) _indicator.gameObject.SetActive(visible);
    }

    void BuildIndicator()
    {
        var rig  = XRPlatformServices.Instance != null ? XRPlatformServices.Instance.Rig : null;
        var head = rig?.CenterEyeAnchor;
        if (head == null) return;

        var go = new GameObject("FastForwardIndicator");
        go.transform.SetParent(head, worldPositionStays: false);
        go.transform.localPosition = _indicatorOffset;
        go.transform.localRotation = Quaternion.identity;

        _indicator = go.AddComponent<TextMeshPro>();
        _indicator.text             = $">> {_speed:0.#}x";
        _indicator.fontSize         = _fontSize * 10f; // TextMeshPro world text: fontSize 10 ≈ 1 m line height
        _indicator.alignment        = TextAlignmentOptions.Center;
        _indicator.textWrappingMode = TextWrappingModes.NoWrap;
        _indicator.rectTransform.sizeDelta = new Vector2(0.3f, 0.1f);
        _indicator.color            = new Color(1f, 0.85f, 0.1f);
        _indicator.outlineWidth     = 0.2f;
        _indicator.outlineColor     = Color.black;
        SharpUi.Apply(go);
    }
}
