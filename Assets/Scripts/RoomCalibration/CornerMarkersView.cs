using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Shows corners while staff place them: a numbered post per corner, lines between placed corners,
// a preview line from the last corner to the pointer, and the next corner's number at the pointer.
// Posts are grey while the anchor is being created and yellow once anchored.
// After a failed shape check the four corners stay, with the bad side (or whole outline) in red.
public class CornerMarkersView : MonoBehaviour
{
    [SerializeField] CalibrationFlow      _flow;
    [SerializeField] RoomCornerCalibrator _calibrator;

    [Header("Look")]
    [SerializeField] float _postHeight   = 0.3f;
    [SerializeField] float _postRadius   = 0.03f;
    [SerializeField] float _labelHeight  = 0.45f; // above the floor
    [SerializeField] float _labelSize    = 0.12f; // metres
    [SerializeField] float _lineWidth    = 0.015f;
    [SerializeField] Color _pendingColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] Color _placedColor  = new Color(1f, 0.85f, 0.1f);
    [SerializeField] Color _previewColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] Color _errorColor   = new Color(1f, 0.25f, 0.2f);

    sealed class Corner
    {
        public GameObject root;
        public Renderer[] renderers;
        public Vector3    position;
        public bool       anchored;
    }

    readonly List<Corner> _corners = new();
    readonly List<LineRenderer> _sides = new(); // side i: corner i → i+1 (side 3 closes 4 → 1)
    LineRenderer _preview, _closingPreview;
    TextMeshPro  _nextLabel;
    readonly List<TextMeshPro> _labels = new();
    Material     _material;
    int          _badSide = -2; // -2 none, -1 whole outline, 0..3 one side

    void Awake()
    {
        _material       = new Material(Shader.Find("Sprites/Default"));
        _preview        = CreateLine("PreviewLine", _previewColor);
        _closingPreview = CreateLine("ClosingPreviewLine", _previewColor);
        _nextLabel      = CreateLabel("NextCornerLabel", _previewColor);
        for (int i = 0; i < 4; i++)
        {
            var side = CreateLine($"Side{i + 1}", _placedColor);
            side.positionCount = 2;
            side.gameObject.SetActive(false);
            _sides.Add(side);
        }
    }

    void OnEnable()
    {
        _calibrator.OnCornerPending    += HandlePending;
        _calibrator.OnCornerPlaced     += HandlePlaced;
        _calibrator.OnCornerRemoved    += HandleRemoved;
        _calibrator.OnCornersCleared   += HandleCleared;
        _calibrator.OnShapeCheckFailed += HandleShapeFailed;
    }

    void OnDisable()
    {
        _calibrator.OnCornerPending    -= HandlePending;
        _calibrator.OnCornerPlaced     -= HandlePlaced;
        _calibrator.OnCornerRemoved    -= HandleRemoved;
        _calibrator.OnCornersCleared   -= HandleCleared;
        _calibrator.OnShapeCheckFailed -= HandleShapeFailed;
    }

    void OnDestroy()
    {
        HandleCleared();
        if (_material != null) Destroy(_material);
    }

    // ─── Calibrator events ─────────────────────────────────────────────────────

    void HandlePending(int index, Vector3 position)
    {
        RemoveFrom(index);
        _corners.Add(CreateCorner(index, position));
        _badSide = -2;
        RefreshSides();
    }

    void HandlePlaced(int index, Vector3 position)
    {
        if (index >= _corners.Count) return;
        var c = _corners[index];
        c.position = position;
        c.anchored = true;
        c.root.transform.position = position;
        RefreshSides();
    }

    void HandleRemoved(int index)
    {
        RemoveFrom(index);
        _badSide = -2;
        RefreshSides();
    }

    void HandleCleared()
    {
        RemoveFrom(0);
        _badSide = -2;
        RefreshSides();
    }

    void HandleShapeFailed(string reason, int badSide)
    {
        _badSide = badSide;
        RefreshSides();
    }

    // ─── Per frame: visibility, preview line, labels facing the head ───────────

    void LateUpdate()
    {
        bool calibrating = _flow.CurrentState == CalibrationFlow.State.Calibrating;
        foreach (var c in _corners) c.root.SetActive(calibrating);
        for (int i = 0; i < _sides.Count; i++) _sides[i].enabled = calibrating;

        int count = _corners.Count;
        Vector3 hit = default;
        bool canPreview = calibrating && count < 4 && !_calibrator.IsPlacing &&
                          _calibrator.TryGetFloorHit(out hit);

        // Line from the last corner to the pointer.
        _preview.gameObject.SetActive(canPreview && count >= 1);
        if (canPreview && count >= 1) SetLine(_preview, Lift(_corners[count - 1].position), Lift(hit));

        // With three corners down, also preview the closing side back to corner 1.
        _closingPreview.gameObject.SetActive(canPreview && count == 3);
        if (canPreview && count == 3) SetLine(_closingPreview, Lift(hit), Lift(_corners[0].position));

        // Next corner number at the pointer.
        _nextLabel.gameObject.SetActive(canPreview);
        if (canPreview)
        {
            _nextLabel.text = (count + 1).ToString();
            _nextLabel.transform.position = hit + Vector3.up * _labelHeight;
        }

        FaceHead();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    void RefreshSides()
    {
        int count = _corners.Count;
        for (int i = 0; i < 4; i++)
        {
            var side = _sides[i];
            int a = i, b = (i + 1) % 4;
            bool visible = i < 3 ? b < count : count == 4; // side 4 closes only when all four exist
            side.gameObject.SetActive(visible);
            if (!visible) continue;

            SetLine(side, Lift(_corners[a].position), Lift(_corners[b].position));
            bool bad = _badSide == -1 || _badSide == i;
            var color = bad ? _errorColor : _placedColor;
            side.startColor = side.endColor = color;
        }

        // Posts: grey while anchoring, yellow once anchored, red when they belong to a bad side / outline.
        for (int i = 0; i < count; i++)
        {
            var c = _corners[i];
            bool bad = count == 4 && (_badSide == -1 || _badSide == i || _badSide == (i + 3) % 4);
            SetCornerColor(c, bad ? _errorColor : c.anchored ? _placedColor : _pendingColor);
        }
    }

    void RemoveFrom(int index)
    {
        for (int i = _corners.Count - 1; i >= index && i >= 0; i--)
        {
            Destroy(_corners[i].root);
            _corners.RemoveAt(i);
            _labels.RemoveAt(i);
        }
    }

    Corner CreateCorner(int index, Vector3 position)
    {
        var root = new GameObject($"Corner{index + 1}");
        root.transform.SetParent(transform, worldPositionStays: false);
        root.transform.position = position;

        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Post";
        Destroy(post.GetComponent<Collider>());
        post.transform.SetParent(root.transform, worldPositionStays: false);
        post.transform.localPosition = new Vector3(0f, _postHeight * 0.5f, 0f);
        post.transform.localScale    = new Vector3(_postRadius * 2f, _postHeight * 0.5f, _postRadius * 2f);

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Base";
        Destroy(ball.GetComponent<Collider>());
        ball.transform.SetParent(root.transform, worldPositionStays: false);
        ball.transform.localScale = Vector3.one * (_postRadius * 3f);

        var label = CreateLabel("Label", _placedColor);
        label.transform.SetParent(root.transform, worldPositionStays: false);
        label.transform.localPosition = new Vector3(0f, _labelHeight, 0f);
        label.text = (index + 1).ToString();
        _labels.Add(label);

        var renderers = new[] { post.GetComponent<Renderer>(), ball.GetComponent<Renderer>() };
        foreach (var r in renderers) r.material = new Material(_material); // unlit, own colour per corner

        var corner = new Corner { root = root, position = position, renderers = renderers };
        SetCornerColor(corner, _pendingColor);
        return corner;
    }

    void SetCornerColor(Corner c, Color color)
    {
        foreach (var r in c.renderers) r.material.color = color;
    }

    LineRenderer CreateLine(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, worldPositionStays: false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace  = true;
        lr.positionCount  = 2;
        lr.startWidth     = lr.endWidth = _lineWidth;
        lr.sharedMaterial = _material;
        lr.startColor     = lr.endColor = color;
        go.SetActive(false);
        return lr;
    }

    TextMeshPro CreateLabel(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, worldPositionStays: false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize  = _labelSize * 10f; // TextMeshPro world text: fontSize 10 ≈ 1 m line height
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.fontStyle = FontStyles.Bold;
        tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.3f);
        return tmp;
    }

    void FaceHead()
    {
        var rig = XRPlatformServices.Instance != null ? XRPlatformServices.Instance.Rig : null;
        var head = rig?.CenterEyeAnchor;
        if (head == null) return;
        foreach (var l in _labels) Face(l.transform, head.position);
        if (_nextLabel.gameObject.activeSelf) Face(_nextLabel.transform, head.position);
    }

    static void Face(Transform t, Vector3 headPosition)
    {
        var look = t.position - headPosition;
        look.y = 0f;
        if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look, Vector3.up);
    }

    static void SetLine(LineRenderer lr, Vector3 a, Vector3 b)
    {
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    static Vector3 Lift(Vector3 p) => p + Vector3.up * 0.01f;
}
