using UnityEngine;

public abstract class XRRig : MonoBehaviour, IXRRig
{
    public abstract Transform CenterEyeAnchor { get; }
    public abstract float FloorY { get; }
}
