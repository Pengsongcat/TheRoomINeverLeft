using UnityEngine;

public class MetaPassthroughService : PassthroughService
{
    [SerializeField] OVRPassthroughLayer _passthroughLayer;

    public override bool IsActive => _passthroughLayer != null && !_passthroughLayer.hidden;

    public override float Opacity
    {
        get => _passthroughLayer != null ? _passthroughLayer.textureOpacity : 0f;
        set { if (_passthroughLayer != null) _passthroughLayer.textureOpacity = value; }
    }

    public override void Enable()
    {
        if (_passthroughLayer == null) return;
        _passthroughLayer.hidden = false;
    }

    public override void Disable()
    {
        if (_passthroughLayer == null) return;
        _passthroughLayer.hidden = true;
    }
}
