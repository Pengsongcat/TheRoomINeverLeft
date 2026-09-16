using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Staff performance check, toggled any time with left Y (keyboard F in the Editor).
// On Quest it turns the OVR Metrics Tool overlay on / off; the tool must be installed on the headset
// and its persistent overlay permission granted once in the tool's own settings.
// In the Editor (no OVR Metrics Tool) or with _inAppReadout on, a small head-locked FPS readout is shown instead / as well.
public class PerformanceOverlayToggle : MonoBehaviour
{
    [SerializeField] InputActionReference _toggleAction;

    [Header("In-app readout")]
    [Tooltip("Also show Unity's own FPS readout on the headset. Always used in the Editor.")]
    [SerializeField] bool    _inAppReadout  = false;
    [SerializeField] Vector3 _readoutOffset = new Vector3(-0.15f, 0.12f, 0.7f); // metres from the eye, head-locked
    [SerializeField] float   _fontSize      = 0.02f; // metres

    const string MetricsPackage  = "com.oculus.ovrmonitormetricsservice";
    const string MetricsReceiver = MetricsPackage + ".SettingsBroadcastReceiver";

    bool        _on;
    TextMeshPro _text;
    float       _windowTime;
    int         _windowFrames;
    float       _worstFrame;
    float       _worstFrameShown;
    float       _worstResetTime;

    bool UseInAppReadout => _inAppReadout || Application.isEditor;

    void OnEnable()
    {
        if (_toggleAction == null) return;
        _toggleAction.action.Enable();
        _toggleAction.action.performed += OnToggle;
    }

    void OnDisable()
    {
        if (_toggleAction != null) _toggleAction.action.performed -= OnToggle;
    }

    void OnToggle(InputAction.CallbackContext _)
    {
        _on = !_on;

        if (Application.platform == RuntimePlatform.Android)
            SendMetricsBroadcast(_on ? "ENABLE_OVERLAY" : "DISABLE_OVERLAY");

        if (UseInAppReadout) SetReadoutVisible(_on);
        Debug.Log($"[PerformanceOverlayToggle] Performance overlay {(_on ? "on" : "off")}");
    }

    // ─── OVR Metrics Tool ──────────────────────────────────────────────────────

    // Same as: adb shell am broadcast -n com.oculus.ovrmonitormetricsservice/.SettingsBroadcastReceiver -a <action>
    static void SendMetricsBroadcast(string action)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var activity = UnityEngine.Android.AndroidApplication.currentActivity;
            using var intent = new AndroidJavaObject("android.content.Intent", $"{MetricsPackage}.{action}");
            intent.Call<AndroidJavaObject>("setClassName", MetricsPackage, MetricsReceiver)?.Dispose();
            activity.Call("sendBroadcast", intent);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PerformanceOverlayToggle] Could not reach OVR Metrics Tool ({action}): {e.Message}");
        }
#endif
    }

    // ─── In-app readout ────────────────────────────────────────────────────────

    void SetReadoutVisible(bool visible)
    {
        if (visible && _text == null) BuildReadout();
        if (_text == null) return;
        _text.gameObject.SetActive(visible);
        _windowTime = 0f; _windowFrames = 0; _worstFrame = 0f; _worstFrameShown = 0f;
    }

    void BuildReadout()
    {
        var rig  = XRPlatformServices.Instance != null ? XRPlatformServices.Instance.Rig : null;
        var head = rig?.CenterEyeAnchor;
        if (head == null) return;

        var go = new GameObject("FpsReadout");
        go.transform.SetParent(head, worldPositionStays: false);
        go.transform.localPosition = _readoutOffset;
        go.transform.localRotation = Quaternion.identity;

        _text = go.AddComponent<TextMeshPro>();
        _text.fontSize         = _fontSize * 10f; // TextMeshPro world text: fontSize 10 ≈ 1 m line height
        _text.alignment        = TextAlignmentOptions.TopLeft;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.rectTransform.sizeDelta = new Vector2(0.3f, 0.1f);
        _text.outlineWidth     = 0.2f;
        _text.outlineColor     = Color.black;
    }

    void Update()
    {
        if (_text == null || !_text.gameObject.activeSelf) return;

        float dt = Time.unscaledDeltaTime;
        _windowTime += dt;
        _windowFrames++;
        _worstFrame = Mathf.Max(_worstFrame, dt);

        if (Time.unscaledTime - _worstResetTime >= 1f)
        {
            _worstFrameShown = _worstFrame;
            _worstFrame      = 0f;
            _worstResetTime  = Time.unscaledTime;
        }

        if (_windowTime < 0.5f) return;

        float fps = _windowFrames / _windowTime;
        float hz  = RefreshRate();
        var color = hz <= 0f || fps >= hz * 0.95f ? "#6f6" : fps >= hz * 0.8f ? "#ff6" : "#f66";
        _text.text = $"<color={color}>{fps:F0} fps</color>" + (hz > 0f ? $" / {hz:F0} Hz" : "") +
                     $"\n{1000f / fps:F1} ms · worst {_worstFrameShown * 1000f:F0} ms";

        _windowTime = 0f;
        _windowFrames = 0;
    }

    static readonly List<XRDisplaySubsystem> Displays = new();

    static float RefreshRate()
    {
        SubsystemManager.GetSubsystems(Displays);
        foreach (var d in Displays)
            if (d.running && d.TryGetDisplayRefreshRate(out float hz)) return hz;
        return 0f;
    }
}
