using UnityEngine;

public class MetaXRRig : XRRig
{
    [SerializeField] Transform _centerEyeAnchor;
    [SerializeField] float _floorY;

    public override Transform CenterEyeAnchor => _centerEyeAnchor;
    public override float FloorY => _floorY;
}
