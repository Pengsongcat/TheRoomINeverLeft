using UnityEngine;
using UnityEngine.Rendering;

// The endless black void shown after passthrough fades out. Built at startup under Visual Layout (not scaled),
// so its bottom sits on the floor measured from the anchors (Room Root is on the floor).
// Only the floor is needed: the eye cameras clear to black and the scene ambient is black, so any floor area
// outside a sound light's reach renders pure black and the edge is never seen. Walls and ceiling are optional.
// The floor near the room is split into tiles because URP Forward lights at most 4 additional lights per object;
// with one big floor mesh only the 4 brightest sound lights would show. Beyond the tiled area, 4 large strips.
public class VoidBox : MonoBehaviour
{
    [Header("Floor")]
    [Tooltip("Dark, non-metallic URP Lit material. Not pure black, or lights would not show on it.")]
    [SerializeField] Material _floorMaterial;
    [Tooltip("Side of the tiled square around the room centre, metres. Sound lights should stay inside it.")]
    [SerializeField, Min(1f)] float _tiledSize = 24f;
    [SerializeField, Min(0.5f)] float _tileSize = 2f;
    [Tooltip("Side of the whole floor, metres.")]
    [SerializeField, Min(1f)] float _totalSize = 60f;

    [Header("Walls and ceiling (optional)")]
    [SerializeField] bool     _buildWalls;
    [Tooltip("Unlit black. Only used when walls are built.")]
    [SerializeField] Material _wallMaterial;
    [SerializeField, Min(1f)] float _height = 20f;

    [SerializeField] bool _visibleAtStart;

    GameObject _root;

    public bool IsVisible => _root != null && _root.activeSelf;

    void Awake()
    {
        Build();
        SetVisible(_visibleAtStart);
    }

    public void SetVisible(bool visible)
    {
        if (_root != null) _root.SetActive(visible);
    }

    void Build()
    {
        _root = new GameObject("Void Geometry");
        _root.transform.SetParent(transform, worldPositionStays: false);

        if (_floorMaterial == null)
            Debug.LogWarning("[VoidBox] No floor material assigned; the void floor is not built.");
        else
            BuildFloor();

        if (_buildWalls)
        {
            if (_wallMaterial == null) Debug.LogWarning("[VoidBox] No wall material assigned; walls are not built.");
            else BuildWalls();
        }
    }

    void BuildFloor()
    {
        int   tilesPerSide = Mathf.Max(1, Mathf.RoundToInt(_tiledSize / _tileSize));
        float tile         = _tiledSize / tilesPerSide; // exact fit of the tiled square
        float half         = _tiledSize * 0.5f;
        float totalHalf    = Mathf.Max(_totalSize, _tiledSize) * 0.5f;

        var floor    = NewChild(_root.transform, "Floor");
        var tileMesh = Quad(tile, tile);

        for (int ix = 0; ix < tilesPerSide; ix++)
        for (int iz = 0; iz < tilesPerSide; iz++)
        {
            var pos = new Vector3(-half + (ix + 0.5f) * tile, 0f, -half + (iz + 0.5f) * tile);
            AddRenderer(floor, $"Tile {ix},{iz}", tileMesh, _floorMaterial, pos, Quaternion.identity);
        }

        float ring = totalHalf - half; // width of the outer strips
        if (ring <= 0f) return;
        float full = totalHalf * 2f;
        // Front / back strips span the full width; left / right fill between them.
        AddRenderer(floor, "Outer Front", Quad(full, ring), _floorMaterial,
                    new Vector3(0f, 0f,  half + ring * 0.5f), Quaternion.identity);
        AddRenderer(floor, "Outer Back",  Quad(full, ring), _floorMaterial,
                    new Vector3(0f, 0f, -half - ring * 0.5f), Quaternion.identity);
        AddRenderer(floor, "Outer Right", Quad(ring, _tiledSize), _floorMaterial,
                    new Vector3( half + ring * 0.5f, 0f, 0f), Quaternion.identity);
        AddRenderer(floor, "Outer Left",  Quad(ring, _tiledSize), _floorMaterial,
                    new Vector3(-half - ring * 0.5f, 0f, 0f), Quaternion.identity);
    }

    // Four walls and a ceiling facing inwards.
    void BuildWalls()
    {
        float s = Mathf.Max(_totalSize, _tiledSize);
        float h = s * 0.5f;
        var walls   = NewChild(_root.transform, "Walls");
        var wall    = Quad(s, _height); // laid flat, then rotated upright
        var ceiling = Quad(s, s);
        float y = _height * 0.5f;

        // Rotating the up-facing quad by -90° about x makes it face -z (towards the centre from the front wall).
        AddRenderer(walls, "Front",   wall, _wallMaterial, new Vector3(0f, y,  h), Quaternion.Euler(-90f,   0f, 0f));
        AddRenderer(walls, "Back",    wall, _wallMaterial, new Vector3(0f, y, -h), Quaternion.Euler(-90f, 180f, 0f));
        AddRenderer(walls, "Right",   wall, _wallMaterial, new Vector3( h, y, 0f), Quaternion.Euler(-90f,  90f, 0f));
        AddRenderer(walls, "Left",    wall, _wallMaterial, new Vector3(-h, y, 0f), Quaternion.Euler(-90f, -90f, 0f));
        AddRenderer(walls, "Ceiling", ceiling, _wallMaterial, new Vector3(0f, _height, 0f), Quaternion.Euler(180f, 0f, 0f));
    }

    static Transform NewChild(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, worldPositionStays: false);
        return t;
    }

    static void AddRenderer(Transform parent, string name, Mesh mesh, Material material,
                            Vector3 localPosition, Quaternion localRotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.SetLocalPositionAndRotation(localPosition, localRotation);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial         = material;
        r.shadowCastingMode      = ShadowCastingMode.Off;
        r.receiveShadows         = false;
        r.lightProbeUsage        = LightProbeUsage.Off;      // no baked ambient: only sound lights
        r.reflectionProbeUsage   = ReflectionProbeUsage.Off;
    }

    // Flat quad of width (x) × depth (z) centred on the origin, facing +y.
    static Mesh Quad(float width, float depth)
    {
        float w = width * 0.5f, d = depth * 0.5f;
        var mesh = new Mesh { name = $"Void Quad {width:0.##}x{depth:0.##}" };
        mesh.vertices  = new[] { new Vector3(-w, 0f, -d), new Vector3(-w, 0f, d), new Vector3(w, 0f, d), new Vector3(w, 0f, -d) };
        mesh.normals   = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv        = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
