using UnityEngine;

public static class RoomShapeCheck
{
    const float MinSide = 1f;

    // Returns true if the four clockwise floor-level corners form a usable room shape.
    // Checks: each side >= 1 m, all turns are clockwise (no self-intersection or counter-clockwise winding).
    public static bool Validate(Vector3[] corners, out string reason)
    {
        if (corners == null || corners.Length != 4)
        {
            reason = "Need exactly 4 corners.";
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 4];
            var d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            if (len < MinSide)
            {
                reason = $"Side {i + 1} is {len:F2} m (minimum {MinSide} m).";
                return false;
            }
        }

        // Cross product Y < 0 for each turn means clockwise from above.
        // Any non-negative Y means a counter-clockwise or collinear turn — invalid.
        for (int i = 0; i < 4; i++)
        {
            var ab = corners[(i + 1) % 4] - corners[i];
            var bc = corners[(i + 2) % 4] - corners[(i + 1) % 4];
            ab.y = 0f;
            bc.y = 0f;
            if (Vector3.Cross(ab, bc).y <= 0f)
            {
                reason = "Corners must be placed clockwise (seen from above) without crossing.";
                return false;
            }
        }

        reason = null;
        return true;
    }
}
