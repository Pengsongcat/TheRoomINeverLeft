using UnityEngine;

public enum RoomFitMode { Even, Stretch }

public readonly struct RoomFitResult
{
    public readonly float sx;
    public readonly float sz;
    public readonly float rangeFactor;
    public readonly Vector3 soundLayoutLocalPosition;

    public RoomFitResult(float sx, float sz, float rangeFactor, Vector3 soundLayoutLocalPosition)
    {
        this.sx = sx;
        this.sz = sz;
        this.rangeFactor = rangeFactor;
        this.soundLayoutLocalPosition = soundLayoutLocalPosition;
    }
}

public static class RoomFit
{
    public static RoomFitResult Compute(RoomRect room, LabReference lab, RoomFitMode mode = RoomFitMode.Even)
    {
        float sx = room.width  / lab.width;
        float sz = room.length / lab.length;

        if (mode == RoomFitMode.Even)
            sx = sz = Mathf.Min(sx, sz);

        float rangeFactor = Mathf.Min(sx, sz);

        // Negate the lab centre so it lands on Room Root after the Sound Layout transform.
        // Sound Layout localPos + scale * labCentreLocal = 0  =>  localPos = -scale * labCentreLocal
        var offset = new Vector3(-lab.centreX * sx, 0f, -lab.centreZ * sz);

        return new RoomFitResult(sx, sz, rangeFactor, offset);
    }
}
