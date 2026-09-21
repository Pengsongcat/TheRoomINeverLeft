using UnityEngine;

public class XRPlatformServices : MonoBehaviour
{
    public static XRPlatformServices Instance { get; private set; }

    [SerializeField] XRRig _rig;
    [SerializeField] SpatialAnchorService _anchorService;
    [SerializeField] PassthroughService _passthroughService;

    public IXRRig Rig => _rig;
    public ISpatialAnchorService AnchorService => _anchorService;
    public IPassthroughService PassthroughService => _passthroughService;

    void Awake() => Instance = this;
}
