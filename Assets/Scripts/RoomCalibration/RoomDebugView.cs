using System.Collections.Generic;
using UnityEngine;

// Shows debug markers during confirmation.
// Spawns small spheres at raw anchor positions, a wireframe at the fitted rectangle corners,
// and markers for every sound in the fitted layout (set via SetSoundMarkers before Show).
public class RoomDebugView : MonoBehaviour
{
    [SerializeField] float _markerRadius   = 0.05f;
    [SerializeField] Color _rawColor       = Color.yellow;
    [SerializeField] Color _fittedColor    = new Color(0f, 0.8f, 1f);
    [SerializeField] Color _edgeColor      = new Color(0f, 0.8f, 1f);

    [Header("Sound Markers")]
    [SerializeField] float _soundMarkerRadius = 0.06f;
    [SerializeField] Color _guideColor        = new Color(0.2f, 1f, 0.3f);
    [SerializeField] Color _startPointColor   = new Color(1f, 0.3f, 0.9f);
    [SerializeField] Color _soundColor        = new Color(1f, 0.6f, 0.1f);
    [SerializeField] int   _ringSegments      = 32;

    GameObject _root;
    readonly List<SoundMarker> _soundMarkers = new();

    // For the legend.
    public Color RawCornerColor  => _rawColor;
    public Color RoomOutlineColor => _edgeColor;
    public Color StartPointColor => _startPointColor;

    public void SetSoundMarkers(IEnumerable<SoundMarker> markers)
    {
        _soundMarkers.Clear();
        if (markers != null) _soundMarkers.AddRange(markers);
    }

    // includeSounds = false draws only the corners and room outline (during the story the sounds show their own visualizers).
    public void Show(RoomRect rect, Vector3[] rawPositions, bool includeSounds = true)
    {
        Hide();
        _root = new GameObject("RoomDebugView");

        // Raw anchor positions (yellow spheres).
        if (rawPositions != null)
        {
            foreach (var pos in rawPositions)
                SpawnSphere(pos, _rawColor, _markerRadius, "RawCorner");
        }

        // Fitted rectangle corners (cyan spheres) + connecting lines.
        var fc = new[]
        {
            rect.FrontLeft,
            rect.FrontRight,
            rect.BackRight,
            rect.BackLeft,
        };

        foreach (var pos in fc)
            SpawnSphere(pos, _fittedColor, _markerRadius, "FittedCorner");

        for (int i = 0; i < 4; i++)
            SpawnLine(new[] { fc[i], fc[(i + 1) % 4] }, _edgeColor, loop: false, "FittedEdge");

        if (!includeSounds) return;

        // Sounds at their fitted positions; trigger rings on the floor.
        foreach (var m in _soundMarkers)
        {
            var color = m.kind == SoundMarkerKind.StartPoint ? _startPointColor : m.color ?? ColorFor(m.kind);
            SpawnSphere(m.position, color, _soundMarkerRadius, m.kind.ToString());
            if (m.triggerRadius > 0f)
                SpawnRing(new Vector3(m.position.x, rect.floorY + 0.01f, m.position.z), m.triggerRadius, color);
        }
    }

    public void Hide()
    {
        if (_root != null) Destroy(_root);
        _root = null;
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    Color ColorFor(SoundMarkerKind kind) => kind switch
    {
        SoundMarkerKind.Guide      => _guideColor,
        SoundMarkerKind.StartPoint => _startPointColor,
        _                          => _soundColor,
    };

    void SpawnSphere(Vector3 position, Color color, float radius, string label)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = label;
        go.transform.SetParent(_root.transform, worldPositionStays: false);
        go.transform.position   = position;
        go.transform.localScale = Vector3.one * (radius * 2f);

        // Remove the collider; it is not needed for debug markers.
        Destroy(go.GetComponent<Collider>());

        var rend = go.GetComponent<Renderer>();
        rend.material           = new Material(Shader.Find("Sprites/Default"));
        rend.material.color     = color;
    }

    void SpawnRing(Vector3 centre, float radius, Color color)
    {
        var points = new Vector3[_ringSegments];
        for (int i = 0; i < _ringSegments; i++)
        {
            float a = i * Mathf.PI * 2f / _ringSegments;
            points[i] = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }
        SpawnLine(points, color, loop: true, "TriggerRing");
    }

    void SpawnLine(Vector3[] points, Color color, bool loop, string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(_root.transform, worldPositionStays: false);

        var lr             = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = true;
        lr.loop            = loop;
        lr.positionCount   = points.Length;
        lr.SetPositions(points);
        lr.startWidth      = 0.01f;
        lr.endWidth        = 0.01f;
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        lr.material.color  = color;
    }
}
