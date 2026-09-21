using UnityEngine;

// White outline of the Room's Plane (the design space), flat on the floor. Follows the plane's corners every
// frame, so it shows the calibrated position, rotation and size. The line keeps its width in metres however
// the plane is scaled.
public class PlaneVisualizer : DebugVisualizer
{
    [SerializeField] Material   _material;
    [Tooltip("The plane to outline (a Quad or Plane mesh). Set on the scene instance of the Debugger.")]
    [SerializeField] MeshFilter _plane;
    [Tooltip("Line width, metres.")]
    [SerializeField, Min(0.001f)] float _width = 0.01f;
    [Tooltip("Height above the plane, metres. Keeps the line from z-fighting it.")]
    [SerializeField, Min(0f)] float _lift = 0.005f;

    MeshFilter _outline;
    Mesh       _mesh;
    readonly Vector3[] _corners  = new Vector3[4];
    readonly Vector3[] _vertices = new Vector3[8];

    protected override void Awake()
    {
        base.Awake();
        _outline = AddRenderer("Outline", _material);
        _mesh = new Mesh { name = "Debug Plane Outline" };
        _mesh.MarkDynamic();
        _outline.sharedMesh = _mesh;
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }

    void LateUpdate()
    {
        if (!IsVisible || _plane == null || _plane.sharedMesh == null) return;

        // Mesh bounds corners in the plane's local space. A Quad lies in local XY, a Plane mesh in local XZ.
        var b = _plane.sharedMesh.bounds;
        bool xy = b.size.z < b.size.y;
        var t = _plane.transform;
        for (int i = 0; i < 4; i++)
        {
            float u = i == 1 || i == 2 ? b.max.x : b.min.x;
            float v = i >= 2 ? (xy ? b.max.y : b.max.z) : (xy ? b.min.y : b.min.z);
            var local = xy ? new Vector3(u, v, b.center.z) : new Vector3(u, b.center.y, v);
            _corners[i] = t.TransformPoint(local) + Vector3.up * _lift;
        }

        // Outer and inner ring, half the width either side of each edge.
        var centre = (_corners[0] + _corners[1] + _corners[2] + _corners[3]) * 0.25f;
        var axisA  = (_corners[1] - _corners[0]).normalized;
        var axisB  = (_corners[3] - _corners[0]).normalized;
        float half = _width * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            var toCorner = _corners[i] - centre;
            var offset = (Mathf.Sign(Vector3.Dot(toCorner, axisA)) * axisA +
                          Mathf.Sign(Vector3.Dot(toCorner, axisB)) * axisB) * half;
            _vertices[i]     = _corners[i] + offset; // outer
            _vertices[i + 4] = _corners[i] - offset; // inner
        }

        // Geometry sits at the world origin, so world positions are used directly.
        Geometry.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Geometry.localScale = Vector3.one;
        _outline.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        _mesh.vertices = _vertices;
        if (_mesh.triangles.Length == 0 || _mesh.normals.Length == 0) BuildTopology();
        _mesh.RecalculateBounds();
    }

    // One quad per side, each wound to face up (Unity's front face: Cross(b - a, c - a) points at the viewer).
    void BuildTopology()
    {
        var triangles = new int[24];
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            int oi = i, oj = j, ii = i + 4, ij = j + 4;
            var n = Vector3.Cross(_vertices[oj] - _vertices[oi], _vertices[ij] - _vertices[oi]);
            int[] quad = n.y > 0f ? new[] { oi, oj, ij, oi, ij, ii }
                                  : new[] { oi, ij, oj, oi, ii, ij };
            quad.CopyTo(triangles, i * 6);
        }
        _mesh.triangles = triangles;

        var normals = new Vector3[8];
        for (int i = 0; i < 8; i++) normals[i] = Vector3.up;
        _mesh.normals = normals;
    }
}
