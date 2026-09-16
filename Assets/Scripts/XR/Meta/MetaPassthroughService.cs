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

    // Darkens the camera feed only; virtual content drawn over passthrough is unaffected.
    public override float Brightness
    {
        get => _passthroughLayer != null ? _passthroughLayer.colorMapEditorBrightness : 0f;
        set { if (_passthroughLayer != null) _passthroughLayer.SetBrightnessContrastSaturation(Mathf.Clamp(value, -1f, 1f)); }
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
