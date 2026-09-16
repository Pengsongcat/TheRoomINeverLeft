using UnityEngine;

public enum SoundMarkerKind { Guide, StartPoint, Sound }

// A sound position shown to staff during Confirming, after the room fit is applied.
// triggerRadius > 0 draws a ring on the floor at the (scaled) trigger reach.
// color is the sound's own visualizer color when it has one; otherwise the debug view picks one by kind.
public readonly struct SoundMarker
{
    public readonly Vector3         position;
    public readonly SoundMarkerKind kind;
    public readonly float           triggerRadius;
    public readonly Color?          color;

    public SoundMarker(Vector3 position, SoundMarkerKind kind, float triggerRadius = 0f, Color? color = null)
    {
        this.position      = position;
        this.kind          = kind;
        this.triggerRadius = triggerRadius;
        this.color         = color;
    }
}
