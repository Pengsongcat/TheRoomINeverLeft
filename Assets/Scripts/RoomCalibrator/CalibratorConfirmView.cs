using UnityEngine;

// While staff confirm: small spheres at the anchor positions (yellow) and the fitted
// rectangle's corners and outline (cyan). Ported from V1's RoomDebugView without the sound markers.
public class CalibratorConfirmView : MonoBehaviour
{
    [SerializeField] RoomCalibrator _flow;
    [SerializeField] float _markerRadius = 0.05f;
    [SerializeField] float _lineWidth    = 0.01f;
    [SerializeField] Color _rawColor     = Color.yellow;
    [SerializeField] Color _fittedColor  = new Color(0f, 0.8f, 1f);

    GameObject _root;
    Material   _material;

    void Awake() => _material = new Material(Shader.Find("Sprites/Default"));

    void OnEnable()  => _flow.LayoutReady += Build;
    void OnDisable() => _flow.LayoutReady -= Build;

    void OnDestroy()
    {
        Clear();
        if (_material != null) Destroy(_material);
    }

    void Update()
    {
        if (_root == null) return;
        _root.SetActive(_flow.CurrentState == RoomCalibrator.State.Confirming);
    }

    void Build(RoomRect rect)
    {
        Clear();
        _root = new GameObject("ConfirmView");
        _root.transform.SetParent(transform, worldPositionStays: false);

        if (_flow.RawCorners != null)
            foreach (var p in _flow.RawCorners) Sphere(p, _rawColor, "RawCorner");

        var fitted = new[] { rect.FrontLeft, rect.FrontRight, rect.BackRight, rect.BackLeft };
        foreach (var p in fitted) Sphere(p, _fittedColor, "FittedCorner");
        for (int i = 0; i < 4; i++) Line(fitted[i], fitted[(i + 1) % 4], _fittedColor);
    }

    void Clear()
    {
        if (_root != null) Destroy(_root);
        _root = null;
    }

    void Sphere(Vector3 position, Color color, string label)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = label;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_root.transform, worldPositionStays: false);
        go.transform.position   = position;
        go.transform.localScale = Vector3.one * (_markerRadius * 2f);
        go.GetComponent<Renderer>().material = new Material(_material) { color = color };
    }

    void Line(Vector3 a, Vector3 b, Color color)
    {
        var go = new GameObject("FittedEdge");
        go.transform.SetParent(_root.transform, worldPositionStays: false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace  = true;
        lr.positionCount  = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth     = lr.endWidth = _lineWidth;
        lr.sharedMaterial = _material;
        lr.startColor     = lr.endColor = color;
    }
}
