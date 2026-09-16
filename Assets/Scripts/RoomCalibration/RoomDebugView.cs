using UnityEngine;

// Shows debug markers during calibration and confirmation.
// Spawns small spheres at raw anchor positions, and a wireframe at the fitted rectangle corners.
public class RoomDebugView : MonoBehaviour
{
    [SerializeField] float _markerRadius   = 0.05f;
    [SerializeField] Color _rawColor       = Color.yellow;
    [SerializeField] Color _fittedColor    = new Color(0f, 0.8f, 1f);
    [SerializeField] Color _edgeColor      = new Color(0f, 0.8f, 1f);

    GameObject _root;

    public void Show(RoomRect rect, Vector3[] rawPositions)
    {
        Hide();
        _root = new GameObject("RoomDebugView");

        // Raw anchor positions (yellow spheres).
        if (rawPositions != null)
        {
            foreach (var pos in rawPositions)
                SpawnSphere(pos, _rawColor, "RawCorner");
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
            SpawnSphere(pos, _fittedColor, "FittedCorner");

        for (int i = 0; i < 4; i++)
            SpawnEdge(fc[i], fc[(i + 1) % 4], "FittedEdge");
    }

    public void Hide()
    {
        if (_root != null) Destroy(_root);
        _root = null;
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    void SpawnSphere(Vector3 position, Color color, string label)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = label;
        go.transform.SetParent(_root.transform, worldPositionStays: false);
        go.transform.position   = position;
        go.transform.localScale = Vector3.one * (_markerRadius * 2f);

        // Remove the collider; it is not needed for debug markers.
        Destroy(go.GetComponent<Collider>());

        var rend = go.GetComponent<Renderer>();
        rend.material           = new Material(Shader.Find("Sprites/Default"));
        rend.material.color     = color;
    }

    void SpawnEdge(Vector3 a, Vector3 b, string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(_root.transform, worldPositionStays: false);

        var lr             = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = true;
        lr.positionCount   = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth      = 0.01f;
        lr.endWidth        = 0.01f;
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        lr.material.color  = _edgeColor;
    }
}
