using NUnit.Framework;
using UnityEngine;

public class RoomMathTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────────

    static LabReference DefaultLab() => new LabReference();

    // Axis-aligned clockwise corners for a room centred at the origin.
    // c1=front-left, c2=front-right, c3=back-right, c4=back-left.
    static Vector3[] RectCorners(float width, float length) => new[]
    {
        new Vector3(-width * 0.5f, 0f,  length * 0.5f),
        new Vector3( width * 0.5f, 0f,  length * 0.5f),
        new Vector3( width * 0.5f, 0f, -length * 0.5f),
        new Vector3(-width * 0.5f, 0f, -length * 0.5f),
    };

    // ─── RoomRect ───────────────────────────────────────────────────────────────

    [Test]
    public void RoomRect_AxisAligned_ComputesCentreWidthLengthForward()
    {
        var c = RectCorners(4f, 8f);
        var rect = new RoomRect(c[0], c[1], c[2], c[3]);

        Assert.AreEqual(0f, rect.centre.x,   0.001f, "centre.x");
        Assert.AreEqual(0f, rect.centre.z,   0.001f, "centre.z");
        Assert.AreEqual(4f, rect.width,       0.001f, "width");
        Assert.AreEqual(8f, rect.length,      0.001f, "length");
        Assert.AreEqual(0f, rect.forward.x,   0.001f, "forward.x");
        Assert.AreEqual(1f, rect.forward.z,   0.001f, "forward.z");
    }

    [Test]
    public void RoomRect_FloorY_IsAverageOfCornerY()
    {
        var c1 = new Vector3(-1f, 0.10f,  2f);
        var c2 = new Vector3( 1f, 0.20f,  2f);
        var c3 = new Vector3( 1f, 0.30f, -2f);
        var c4 = new Vector3(-1f, 0.40f, -2f);

        var rect = new RoomRect(c1, c2, c3, c4);

        Assert.AreEqual(0.25f, rect.floorY, 0.001f);
    }

    [Test]
    public void RoomRect_Rotated45_ForwardAndDimensionsCorrect()
    {
        float cos = Mathf.Cos(45f * Mathf.Deg2Rad);
        float sin = Mathf.Sin(45f * Mathf.Deg2Rad);
        Vector3 Rot45(Vector3 v) => new Vector3(v.x * cos + v.z * sin, v.y, -v.x * sin + v.z * cos);

        var c = RectCorners(4f, 8f);
        var rect = new RoomRect(Rot45(c[0]), Rot45(c[1]), Rot45(c[2]), Rot45(c[3]));

        Assert.AreEqual(sin, rect.forward.x, 0.001f, "forward.x after 45° rotation");
        Assert.AreEqual(cos, rect.forward.z, 0.001f, "forward.z after 45° rotation");
        Assert.AreEqual(4f,  rect.width,     0.01f,  "width unchanged by rotation");
        Assert.AreEqual(8f,  rect.length,    0.01f,  "length unchanged by rotation");
    }

    // ─── RoomRect.RebuildCorner ──────────────────────────────────────────────────

    [Test]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RebuildCorner_Rectangle_RebuildsAllFourCorners(int missing)
    {
        var original = new[]
        {
            new Vector3(0f, 0f, 8f),
            new Vector3(4f, 0f, 8f),
            new Vector3(4f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
        };
        var expected = original[missing];
        var corners  = (Vector3[])original.Clone();
        corners[missing] = Vector3.zero;

        var rebuilt = RoomRect.RebuildCorner(missing, corners);

        Assert.AreEqual(expected.x, rebuilt.x, 0.001f, $"corner {missing} x");
        Assert.AreEqual(expected.z, rebuilt.z, 0.001f, $"corner {missing} z");
    }

    // ─── RoomFit ────────────────────────────────────────────────────────────────

    [Test]
    public void RoomFit_Even_PicksSmallerAxis()
    {
        var c    = RectCorners(3f, 5f);
        var rect = new RoomRect(c[0], c[1], c[2], c[3]);
        var lab  = DefaultLab();

        var result = RoomFit.Compute(rect, lab, RoomFitMode.Even);

        float expected = Mathf.Min(3f / 5.2f, 5f / 8.7f);
        Assert.AreEqual(expected, result.sx, 0.001f, "sx");
        Assert.AreEqual(expected, result.sz, 0.001f, "sz");
    }

    [Test]
    public void RoomFit_Even_ScalesUp()
    {
        var c    = RectCorners(7f, 12f);
        var rect = new RoomRect(c[0], c[1], c[2], c[3]);
        var lab  = DefaultLab();

        var result = RoomFit.Compute(rect, lab, RoomFitMode.Even);

        float expected = Mathf.Min(7f / 5.2f, 12f / 8.7f);
        Assert.AreEqual(expected, result.sx, 0.001f, "sx");
        Assert.Greater(result.sx, 1f, "scale should be > 1 for a larger room");
    }

    [Test]
    public void RoomFit_Stretch_UsesSeparateAxes()
    {
        var c    = RectCorners(3f, 5f);
        var rect = new RoomRect(c[0], c[1], c[2], c[3]);
        var lab  = DefaultLab();

        var result = RoomFit.Compute(rect, lab, RoomFitMode.Stretch);

        Assert.AreEqual(3f / 5.2f, result.sx, 0.001f, "sx");
        Assert.AreEqual(5f / 8.7f, result.sz, 0.001f, "sz");
    }

    [Test]
    public void RoomFit_RangeFactor_IsMinAxisInBothModes()
    {
        var c    = RectCorners(3f, 6f);
        var rect = new RoomRect(c[0], c[1], c[2], c[3]);
        var lab  = DefaultLab();

        var even    = RoomFit.Compute(rect, lab, RoomFitMode.Even);
        var stretch = RoomFit.Compute(rect, lab, RoomFitMode.Stretch);

        float expected = Mathf.Min(3f / 5.2f, 6f / 8.7f);
        Assert.AreEqual(expected, even.rangeFactor,    0.001f, "rangeFactor Even");
        Assert.AreEqual(expected, stretch.rangeFactor, 0.001f, "rangeFactor Stretch");
    }

    [Test]
    public void RoomFit_SoundLayoutOffset_CentresLabAtOrigin()
    {
        // At any scale, soundLayoutLocalPos + scale * labCentreLocal must equal zero.
        var c    = RectCorners(5.2f, 8.7f);
        var rect = new RoomRect(c[0], c[1], c[2], c[3]);
        var lab  = DefaultLab();

        var result = RoomFit.Compute(rect, lab, RoomFitMode.Even);

        float worldX = result.soundLayoutLocalPosition.x + result.sx * lab.centreX;
        float worldZ = result.soundLayoutLocalPosition.z + result.sz * lab.centreZ;
        Assert.AreEqual(0f, worldX, 0.001f, "lab centre x maps to Room Root");
        Assert.AreEqual(0f, worldZ, 0.001f, "lab centre z maps to Room Root");
    }

    [Test]
    public void RoomFit_SoundLayoutOffset_CentresLabAtOrigin_WhenScaled()
    {
        // Same invariant holds for a non-1:1 room.
        var c    = RectCorners(3f, 5f);
        var rect = new RoomRect(c[0], c[1], c[2], c[3]);
        var lab  = DefaultLab();

        var result = RoomFit.Compute(rect, lab, RoomFitMode.Stretch);

        float worldX = result.soundLayoutLocalPosition.x + result.sx * lab.centreX;
        float worldZ = result.soundLayoutLocalPosition.z + result.sz * lab.centreZ;
        Assert.AreEqual(0f, worldX, 0.001f, "lab centre x maps to Room Root at non-1 scale");
        Assert.AreEqual(0f, worldZ, 0.001f, "lab centre z maps to Room Root at non-1 scale");
    }
}
