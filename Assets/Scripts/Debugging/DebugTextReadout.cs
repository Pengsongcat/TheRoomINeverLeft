using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// One head-locked text block that every DebugTextSource child writes its lines into, stacked in child order
// with a blank line between blocks.
// The text material uses the TextMeshPro overlay shader (depth test off), so scene geometry never hides it.
public class DebugTextReadout : DebugVisualizer
{
    [SerializeField] TMP_FontAsset _font;
    [Tooltip("Material for _font using the 'TextMeshPro/Distance Field Overlay' shader.")]
    [SerializeField] Material      _material;
    [Tooltip("Metres from the eye, head-locked.")]
    [SerializeField] Vector3       _offset   = new Vector3(-0.15f, 0.12f, 0.7f);
    [Tooltip("Line height, metres.")]
    [SerializeField, Min(0f)] float _fontSize = 0.02f;
    [Tooltip("Seconds between text updates.")]
    [SerializeField, Min(0.05f)] float _interval = 0.5f;

    readonly List<DebugTextSource> _sources = new();
    readonly StringBuilder _builder = new();
    TextMeshPro _text;
    Transform   _head;
    bool        _wasVisible;
    float       _windowTime;

    protected override void Awake()
    {
        base.Awake();
        GetComponentsInChildren(includeInactive: true, _sources);

        var go = new GameObject("Text") { layer = Geometry.gameObject.layer };
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
            _windowTime = 0f;
            foreach (var s in _sources) s.Begin();
            Refresh(0f);
        }

        float dt = Time.unscaledDeltaTime;
        _windowTime += dt;
        foreach (var s in _sources) s.Sample(dt);

        if (_windowTime < _interval) return;
        Refresh(_windowTime);
        _windowTime = 0f;
    }

    void Refresh(float windowSeconds)
    {
        _builder.Clear();
        foreach (var s in _sources)
        {
            if (!s.isActiveAndEnabled) continue;
            if (_builder.Length > 0) _builder.Append("\n\n"); // blank line between blocks
            s.Write(_builder, windowSeconds);
        }
        _text.text = _builder.ToString();
    }

    // Parents the Geometry to the camera so the text moves with the head in the same frame, without lag.
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
}
