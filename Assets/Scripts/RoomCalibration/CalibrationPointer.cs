using UnityEngine;

// Shows a sphere on the floor where the controller ray will place the next corner (Calibrating only),
// plus a faint grid at the system floor height so staff can check it matches the real floor
// (Calibrating and Confirming).
public class CalibrationPointer : MonoBehaviour
{
    [SerializeField] CalibrationFlow      _flow;
    [SerializeField] RoomCornerCalibrator _calibrator;
    [SerializeField] float _sphereRadius = 0.04f;
    [SerializeField] Color _color        = Color.cyan;

    [Header("Floor Grid")]
    [SerializeField] bool  _showFloorGrid = true;
    [SerializeField] float _gridSize      = 6f;   // metres, square
    [SerializeField] float _gridSpacing   = 0.5f; // metres between lines
    [SerializeField] Color _gridColor     = new Color(1f, 1f, 1f, 0.25f);

    GameObject _marker;
    GameObject _grid;

    void Awake()
    {
        _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _marker.name = "PointerMarker";
        Destroy(_marker.GetComponent<Collider>());
        _marker.transform.localScale = Vector3.one * (_sphereRadius * 2f);
        _marker.GetComponent<Renderer>().material.color = _color;
        _marker.SetActive(false);

        if (_showFloorGrid) _grid = BuildGrid();
    }

    void Update()
    {
        var state = _flow.CurrentState;
        bool calibrating = state == CalibrationFlow.State.Calibrating;
        UpdateGrid(calibrating || state == CalibrationFlow.State.Confirming);

        if (!calibrating || !_calibrator.TryGetFloorHit(out var point))
        {
            _marker.SetActive(false);
            return;
        }
        _marker.transform.position = point;
        _marker.SetActive(true);
    }

    void OnDestroy()
    {
        Destroy(_marker);
        if (_grid != null) Destroy(_grid);
    }

    // ─── Floor grid ────────────────────────────────────────────────────────────

    // Centred under the headset, snapped to the grid spacing so lines stay fixed in the room.
    void UpdateGrid(bool visible)
    {
        if (_grid == null) return;
        var rig = XRPlatformServices.Instance != null ? XRPlatformServices.Instance.Rig : null;
        if (!visible || rig == null || rig.CenterEyeAnchor == null)
        {
            _grid.SetActive(false);
            return;
        }
        var head = rig.CenterEyeAnchor.position;
        _grid.transform.position = new Vector3(
            Mathf.Round(head.x / _gridSpacing) * _gridSpacing,
            rig.FloorY + 0.005f,
            Mathf.Round(head.z / _gridSpacing) * _gridSpacing);
        _grid.SetActive(true);
    }

    GameObject BuildGrid()
    {
        var root = new GameObject("FloorGrid");
        var material = new Material(Shader.Find("Sprites/Default")) { color = _gridColor };
        int lines = Mathf.RoundToInt(_gridSize / _gridSpacing);
        float half = _gridSize * 0.5f;

        for (int i = 0; i <= lines; i++)
        {
            float offset = -half + i * _gridSpacing;
            AddLine(root.transform, material, new Vector3(offset, 0f, -half), new Vector3(offset, 0f, half));
            AddLine(root.transform, material, new Vector3(-half, 0f, offset), new Vector3(half, 0f, offset));
        }
        root.SetActive(false);
        return root;
    }

    static void AddLine(Transform parent, Material material, Vector3 a, Vector3 b)
    {
        var go = new GameObject("GridLine");
        go.transform.SetParent(parent, worldPositionStays: false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace  = false;
        lr.positionCount  = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth     = 0.005f;
        lr.endWidth       = 0.005f;
        lr.sharedMaterial = material;
    }
}
