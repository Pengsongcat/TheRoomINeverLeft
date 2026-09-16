using NUnit.Framework;
using UnityEngine;

// Tests for FloorPlane.TryRaycast (controller ray onto the system floor).
public class FloorPlaneTests
{
    const float Eps = 0.001f;

    [Test]
    public void RayPointingDown_HitsFloorAtFloorHeight()
    {
        // From 1 m above the floor, 45° down along +z → hits 1 m ahead.
        var dir = new Vector3(0f, -1f, 1f);
        bool hit = FloorPlane.TryRaycast(new Vector3(0f, 1f, 0f), dir, 0f, 10f, out var p);

        Assert.IsTrue(hit);
        Assert.AreEqual(0f, p.x, Eps, "x");
        Assert.AreEqual(0f, p.y, Eps, "y");
        Assert.AreEqual(1f, p.z, Eps, "z");
    }

    [Test]
    public void NonZeroFloorHeight_IsRespected()
    {
        bool hit = FloorPlane.TryRaycast(new Vector3(2f, 1.5f, -1f), Vector3.down, 0.3f, 10f, out var p);

        Assert.IsTrue(hit);
        Assert.AreEqual(new Vector3(2f, 0.3f, -1f).ToString("F3"), p.ToString("F3"));
    }

    [Test]
    public void UnnormalisedDirection_GivesSamePoint()
    {
        FloorPlane.TryRaycast(Vector3.up, new Vector3(0f, -1f, 1f),   0f, 10f, out var a);
        FloorPlane.TryRaycast(Vector3.up, new Vector3(0f, -5f, 5f),   0f, 10f, out var b);

        Assert.AreEqual(a.z, b.z, Eps);
    }

    [Test]
    public void RayParallelToFloor_Misses()
    {
        Assert.IsFalse(FloorPlane.TryRaycast(Vector3.up, Vector3.forward, 0f, 10f, out _));
    }

    [Test]
    public void RayPointingUp_Misses()
    {
        Assert.IsFalse(FloorPlane.TryRaycast(Vector3.up, new Vector3(0f, 1f, 1f), 0f, 10f, out _));
    }

    [Test]
    public void OriginBelowFloor_Misses()
    {
        Assert.IsFalse(FloorPlane.TryRaycast(new Vector3(0f, -0.5f, 0f), Vector3.down, 0f, 10f, out _));
    }

    [Test]
    public void HitBeyondMaxDistance_Misses()
    {
        // Shallow ray: floor is ~10 m away along the ray, max distance 5 m.
        var dir = new Vector3(0f, -0.1f, 1f);
        Assert.IsFalse(FloorPlane.TryRaycast(Vector3.up, dir, 0f, 5f, out _));
        Assert.IsTrue (FloorPlane.TryRaycast(Vector3.up, dir, 0f, 15f, out _));
    }
}
