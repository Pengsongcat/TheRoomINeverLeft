using UnityEngine;

public interface IXRRig
{
    Transform CenterEyeAnchor { get; }
    float FloorY { get; }
}
