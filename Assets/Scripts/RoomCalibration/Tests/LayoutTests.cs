using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Tests for RoomLayoutApplier (static Apply overload).
// AudioRangeScaler is in Assembly-CSharp and is not reachable from this test assembly;
// its scaling logic is verified by the Quest end-to-end test (Step 3f).
public class LayoutTests
{
    readonly List<GameObject> _cleanup = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _cleanup) Object.DestroyImmediate(go);
        _cleanup.Clear();
    }

    (Transform root, Transform layout) MakeHierarchy()
    {
        var root   = Tracked(new GameObject("Root")).transform;
        var layout = Tracked(new GameObject("Layout")).transform;
        layout.SetParent(root);
        return (root, layout);
    }

    GameObject Tracked(GameObject go) { _cleanup.Add(go); return go; }

    static RoomRect AxisAlignedRect(float w, float l) =>
        new(new(-w*.5f, 0f, l*.5f), new(w*.5f, 0f, l*.5f),
            new( w*.5f, 0f,-l*.5f), new(-w*.5f, 0f,-l*.5f));

    // ─── RoomLayoutApplier ───────────────────────────────────────────────────────

    [Test]
    public void Apply_SetsRoomRootXZFromCentre()
    {
        var rect = AxisAlignedRect(4f, 8f);
        var fit  = RoomFit.Compute(rect, new LabReference());
        var (root, layout) = MakeHierarchy();

        RoomLayoutApplier.Apply(rect, fit, root, layout);

        Assert.AreEqual(rect.centre.x, root.position.x, 0.001f, "x");
        Assert.AreEqual(rect.centre.z, root.position.z, 0.001f, "z");
    }

    [Test]
    public void Apply_SetsRoomRootYFromFloorY()
    {
        // Corners at y = 0.1, 0.2, 0.3, 0.4 → average 0.25
        var rect = new RoomRect(
            new(-2f, 0.1f,  4f), new(2f, 0.2f,  4f),
            new( 2f, 0.4f, -4f), new(-2f, 0.3f, -4f));
        var fit  = RoomFit.Compute(rect, new LabReference());
        var (root, layout) = MakeHierarchy();

        RoomLayoutApplier.Apply(rect, fit, root, layout);

        Assert.AreEqual(0.25f, root.position.y, 0.001f);
    }

    [Test]
    public void Apply_SetsRoomRootForwardFromRect()
    {
        float cos = Mathf.Cos(45f * Mathf.Deg2Rad);
        float sin = Mathf.Sin(45f * Mathf.Deg2Rad);
        Vector3 R(Vector3 v) => new(v.x * cos + v.z * sin, 0f, -v.x * sin + v.z * cos);
        var rect = new RoomRect(R(new(-2f,0f,4f)), R(new(2f,0f,4f)),
                                R(new( 2f,0f,-4f)), R(new(-2f,0f,-4f)));
        var fit  = RoomFit.Compute(rect, new LabReference());
        var (root, layout) = MakeHierarchy();

        RoomLayoutApplier.Apply(rect, fit, root, layout);

        Assert.AreEqual(rect.forward.x, root.forward.x, 0.01f, "forward.x");
        Assert.AreEqual(rect.forward.z, root.forward.z, 0.01f, "forward.z");
    }

    [Test]
    public void Apply_SetsSoundLayoutScaleFromFit()
    {
        var rect = AxisAlignedRect(3f, 5f);
        var fit  = RoomFit.Compute(rect, new LabReference(), RoomFitMode.Stretch);
        var (root, layout) = MakeHierarchy();

        RoomLayoutApplier.Apply(rect, fit, root, layout);

        Assert.AreEqual(fit.sx, layout.localScale.x, 0.001f, "scale x");
        Assert.AreEqual(1f,     layout.localScale.y, 0.001f, "scale y stays 1");
        Assert.AreEqual(fit.sz, layout.localScale.z, 0.001f, "scale z");
    }

    [Test]
    public void Apply_SoundLayoutOffsetCentresLabOnRoomRoot()
    {
        // After applying, (labCentre * scale) + soundLayoutLocalPos must equal zero,
        // so the lab centre maps exactly to Room Root's world position.
        var rect = AxisAlignedRect(3f, 5f);
        var lab  = new LabReference();
        var fit  = RoomFit.Compute(rect, lab);
        var (root, layout) = MakeHierarchy();

        RoomLayoutApplier.Apply(rect, fit, root, layout);

        float worldX = layout.localPosition.x + fit.sx * lab.centreX;
        float worldZ = layout.localPosition.z + fit.sz * lab.centreZ;
        Assert.AreEqual(0f, worldX, 0.001f, "lab centre x → Room Root");
        Assert.AreEqual(0f, worldZ, 0.001f, "lab centre z → Room Root");
    }
}
