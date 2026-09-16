using UnityEngine;

// Measured rectangle derived from four floor-level anchor positions.
// Corners are clockwise seen from above; c1→c2 is the front wall (virtual +z).
public readonly struct RoomRect
{
    public readonly Vector3 centre;
    public readonly Vector3 forward;  // horizontal, points toward front wall
    public readonly Vector3 right;    // horizontal, cross(up, forward)
    public readonly float width;      // average of front and back edge lengths
    public readonly float length;     // average of left and right edge lengths
    public readonly float floorY;     // average corner Y

    public RoomRect(Vector3 c1, Vector3 c2, Vector3 c3, Vector3 c4)
    {
        centre = (c1 + c2 + c3 + c4) * 0.25f;

        var frontMid = (c1 + c2) * 0.5f;
        var backMid  = (c3 + c4) * 0.5f;
        var fwd = frontMid - backMid;
        fwd.y = 0f;
        forward = fwd.normalized;
        right = Vector3.Cross(Vector3.up, forward);

        width  = (HorizDist(c1, c2) + HorizDist(c3, c4)) * 0.5f;
        length = (HorizDist(c2, c3) + HorizDist(c4, c1)) * 0.5f;
        floorY = (c1.y + c2.y + c3.y + c4.y) * 0.25f;
    }

    // Rebuild the corner at missingIndex using the parallelogram diagonal property.
    // Pass the other three corners in their correct index positions; missingIndex slot is ignored.
    public static Vector3 RebuildCorner(int missingIndex, Vector3[] corners) =>
        missingIndex switch
        {
            0 => corners[1] + corners[3] - corners[2],
            1 => corners[0] + corners[2] - corners[3],
            2 => corners[1] + corners[3] - corners[0],
            3 => corners[0] + corners[2] - corners[1],
            _ => throw new System.ArgumentOutOfRangeException(nameof(missingIndex))
        };

    static float HorizDist(Vector3 a, Vector3 b)
    {
        var d = b - a;
        d.y = 0f;
        return d.magnitude;
    }
}
