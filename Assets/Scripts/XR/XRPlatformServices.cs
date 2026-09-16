using UnityEngine;

public class XRPlatformServices : MonoBehaviour
{
    public static XRPlatformServices Instance { get; private set; }

    [SerializeField] XRRig _rig;
    [SerializeField] SpatialAnchorService _anchorService;
    [SerializeField] PassthroughService _passthroughService;
    [SerializeField] BoundaryService _boundaryService;

    public IXRRig Rig => _rig;
    public ISpatialAnchorService AnchorService => _anchorService;
    public IPassthroughService PassthroughService => _passthroughService;
    public IBoundaryService BoundaryService => _boundaryService;

    void Awake() => Instance = this;
}
