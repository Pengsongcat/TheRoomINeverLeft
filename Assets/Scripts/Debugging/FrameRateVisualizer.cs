using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

// Head-locked frame rate readout: fps against the headset refresh rate, average frame time and the worst frame
// of the last second. Green at 95 % of the refresh rate or better, yellow down to 80 %, red below.
// Measures only while visible. On Air Link this is the PC's render rate; frames lost in streaming don't show here.
// The text material uses the TextMeshPro overlay shader (depth test off), so scene geometry never hides it.
public class FrameRateVisualizer : DebugVisualizer
{
    [SerializeField] TMP_FontAsset _font;
    [Tooltip("Material for _font using the 'TextMeshPro/Distance Field Overlay' shader.")]
    [SerializeField] Material      _material;
    [Tooltip("Metres from the eye, head-locked.")]
    [SerializeField] Vector3       _offset   = new Vector3(-0.15f, 0.12f, 0.7f);
    [Tooltip("Line height, metres.")]
    [SerializeField, Min(0f)] float _fontSize = 0.02f;
    [Tooltip("Seconds between readout updates.")]
    [SerializeField, Min(0.05f)] float _interval = 0.5f;

    TextMeshPro _text;
    Transform   _head;
    bool        _wasVisible;
    float       _windowTime;
    int         _windowFrames;
    float       _worstFrame;
    float       _worstFrameShown;
    float       _worstResetTime;

    protected override void Awake()
    {
        base.Awake();

        var go = new GameObject("Readout") { layer = Geometry.gameObject.layer };
        go.transform.SetParent(Geometry, worldPositionStays: false);

        _text = go.AddComponent<TextMeshPro>();
        if (_font != null) _text.font = _font;
        if (_material != null) _text.fontSharedMaterial = _material;
        _text.fontSize         = _fontSize * 10f; // TextMeshPro world text: fontSize 10 ≈ 1 m line height
        _text.alignment        = TextAlignmentOptions.TopLeft;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.rectTransform.sizeDelta = new Vector2(0.3f, 0.1f);
        _text.text = "";
    }

    void LateUpdate()
    {
        if (!IsVisible) { _wasVisible = false; return; }
        if (_head == null && !TryAttachToHead()) return;

        if (!_wasVisible)
        {
            _wasVisible = true;
            _windowTime = 0f; _windowFrames = 0; _worstFrame = 0f; _worstFrameShown = 0f;
            _worstResetTime = Time.unscaledTime;
            _text.text = "";
        }

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

        if (_windowTime < _interval) return;

        float fps   = _windowFrames / _windowTime;
        float hz    = RefreshRate();
        var   color = hz <= 0f || fps >= hz * 0.95f ? "#6f6" : fps >= hz * 0.8f ? "#ff6" : "#f66";
        _text.text = $"<color={color}>{fps:F0} fps</color>" + (hz > 0f ? $" / {hz:F0} Hz" : "") +
                     $"\n{1000f / fps:F1} ms · worst {_worstFrameShown * 1000f:F0} ms";

        _windowTime   = 0f;
        _windowFrames = 0;
    }

    // Parents the Geometry to the camera so the readout moves with the head in the same frame, without lag.
    bool TryAttachToHead()
    {
        var cam = Camera.main;
        if (cam == null) return false;

        _head = cam.transform;
        Geometry.SetParent(_head, worldPositionStays: false);
        Geometry.localPosition = _offset;
        Geometry.localRotation = Quaternion.identity;
        return true;
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
