using UnityEngine;

// Shows the visitor's physics body as a marker on the floor: a filled circle the size of the Player Body capsule,
// with a triangle just outside it pointing where the body faces. Flat on the floor, so it is seen both from the
// Editor and from inside the headset.
// The body is found by tag and its collider is read every frame, so resizing the collider updates the marker.
public class PlayerBodyVisualizer : DebugVisualizer
{
    [SerializeField] Material _material;
    [SerializeField] string   _playerTag = "Player";
    [Tooltip("Marker height above the bottom of the capsule, metres. Keeps it from z-fighting the floor.")]
    [SerializeField, Min(0f)] float _lift = 0.005f;

    [Header("Front triangle")]
    [Tooltip("Distance from the circle edge to the triangle base, metres.")]
    [SerializeField, Min(0f)] float _triangleGap    = 0.02f;
    [SerializeField, Min(0f)] float _triangleLength = 0.08f;
    [SerializeField, Min(0f)] float _triangleWidth  = 0.08f;

    CapsuleCollider _body;
    MeshFilter      _marker;
    float           _builtRadius = -1f;

    protected override void Awake()
    {
        base.Awake();
        _marker = AddRenderer("Marker", _material);
    }

    void LateUpdate()
    {
        if (!IsVisible) return;
        if (_body == null && !TryFindBody()) return;

        var   t      = _body.transform;
        var   scale  = t.lossyScale;
        float radius = _body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(_body.height * Mathf.Abs(scale.y), radius * 2f);

        if (!Mathf.Approximately(radius, _builtRadius))
        {
            Destroy(_marker.sharedMesh);
            _marker.sharedMesh = Marker(radius);
            _builtRadius = radius;
        }

        var centre = t.TransformPoint(_body.center);
        _marker.transform.SetPositionAndRotation(
            new Vector3(centre.x, centre.y - height * 0.5f + _lift, centre.z),
            Quaternion.Euler(0f, t.eulerAngles.y, 0f));
    }

    bool TryFindBody()
    {
        var go = GameObject.FindWithTag(_playerTag);
        if (go != null) _body = go.GetComponent<CapsuleCollider>();
        return _body != null;
    }

    // Filled circle centred on the origin plus a triangle in front of it (+z), all flat and facing +y.
    Mesh Marker(float radius, int segments = 32)
    {
        var vertices  = new Vector3[segments + 1 + 3];
        var triangles = new int[segments * 3 + 3];

        for (int s = 0; s < segments; s++)
        {
            float a = 2f * Mathf.PI * s / segments;
            vertices[s + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            // Clockwise seen from above, so the face points up.
            triangles[s * 3]     = 0;
            triangles[s * 3 + 1] = (s + 1) % segments + 1;
            triangles[s * 3 + 2] = s + 1;
        }

        int   t    = segments + 1;
        float baseZ = radius + _triangleGap;
        vertices[t]     = new Vector3(-_triangleWidth * 0.5f, 0f, baseZ);
        vertices[t + 1] = new Vector3(0f, 0f, baseZ + _triangleLength);
        vertices[t + 2] = new Vector3( _triangleWidth * 0.5f, 0f, baseZ);
        triangles[segments * 3]     = t;
        triangles[segments * 3 + 1] = t + 1;
        triangles[segments * 3 + 2] = t + 2;

        var normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

        var mesh = new Mesh { name = $"Debug Body Marker {radius:0.##}" };
        mesh.vertices  = vertices;
        mesh.normals   = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
