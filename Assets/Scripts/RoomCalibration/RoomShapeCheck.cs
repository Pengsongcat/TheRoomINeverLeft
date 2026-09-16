using System.Collections.Generic;
using UnityEngine;

public static class RoomShapeCheck
{
    public const float MinSide = 1f;

    // Returns true if the four clockwise floor-level corners form a usable room shape.
    // Checks: each side >= 1 m, all turns are clockwise (no self-intersection or counter-clockwise winding).
    public static bool Validate(Vector3[] corners, out string reason) => Validate(corners, out reason, out _);

    // badSide: index i of the failing side (corner i → i+1), or -1 when the whole shape is at fault.
    public static bool Validate(Vector3[] corners, out string reason, out int badSide)
    {
        badSide = -1;
        if (corners == null || corners.Length != 4)
        {
            reason = "Need exactly 4 corners.";
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            float len = HorizDist(corners[i], corners[(i + 1) % 4]);
            if (len < MinSide)
            {
                reason  = $"Side {i + 1} is {len:F2} m (minimum {MinSide} m).";
                badSide = i;
                return false;
            }
        }

        // Unity is left-handed: a clockwise turn seen from above gives cross.y > 0.
        // Any non-positive Y means a counter-clockwise or collinear turn — invalid.
        for (int i = 0; i < 4; i++)
        {
            if (!IsClockwiseTurn(corners[i], corners[(i + 1) % 4], corners[(i + 2) % 4]))
            {
                reason = "Corners must be placed clockwise (seen from above) without crossing.";
                return false;
            }
        }

        reason = null;
        return true;
    }

    // Checks a candidate corner against the corners already placed, before an anchor is created.
    // Catches the common mistakes early: a double press on the same spot, and turning the wrong way.
    public static bool ValidateNext(IReadOnlyList<Vector3> placed, Vector3 candidate, out string reason)
    {
        int count = placed?.Count ?? 0;
        if (count >= 4)
        {
            reason = "All 4 corners are already placed.";
            return false;
        }

        if (count >= 1)
        {
            float d = HorizDist(placed[count - 1], candidate);
            if (d < MinSide)
            {
                reason = $"Too close to corner {count} ({d:F2} m, need at least {MinSide:F0} m).";
                return false;
            }
        }

        if (count == 3)
        {
            float d = HorizDist(placed[0], candidate);
            if (d < MinSide)
            {
                reason = $"Too close to corner 1 ({d:F2} m, need at least {MinSide:F0} m).";
                return false;
            }
        }

        if (count >= 2 && !IsClockwiseTurn(placed[count - 2], placed[count - 1], candidate))
        {
            reason = "Wrong direction: go clockwise around the room.";
            return false;
        }

        reason = null;
        return true;
    }

    static bool IsClockwiseTurn(Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a;
        var bc = c - b;
        ab.y = 0f;
        bc.y = 0f;
        return Vector3.Cross(ab, bc).y > 0f;
    }

    static float HorizDist(Vector3 a, Vector3 b)
    {
        var d = b - a;
        d.y = 0f;
        return d.magnitude;
    }
}
