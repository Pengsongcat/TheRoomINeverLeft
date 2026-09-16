using UnityEngine;

// Ray intersection with a horizontal floor plane at a given height.
// Used to place corner anchors on the system floor without any colliders.
public static class FloorPlane
{
    public static bool TryRaycast(Vector3 origin, Vector3 direction, float floorY, float maxDistance, out Vector3 point)
    {
        point = default;
        if (direction.y > -1e-4f) return false; // parallel to the floor or pointing up

        float distance = (floorY - origin.y) / direction.y * direction.magnitude;
        if (distance < 0f || distance > maxDistance) return false; // floor is behind the origin or too far

        point = origin + direction.normalized * distance;
        point.y = floorY;
        return true;
    }
}
