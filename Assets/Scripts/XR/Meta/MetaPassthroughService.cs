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

#pragma warning disable CS0618 // overlayType is deprecated but still what the layer composites by
    public override bool OnTop
    {
        get => _passthroughLayer != null && _passthroughLayer.overlayType == OVROverlay.OverlayType.Overlay;
        set
        {
            if (_passthroughLayer == null) return;
            _passthroughLayer.overlayType = value ? OVROverlay.OverlayType.Overlay : OVROverlay.OverlayType.Underlay;
        }
    }
#pragma warning restore CS0618

    public override void SetColorAdjustment(float brightness, float contrast, float saturation)
    {
        if (_passthroughLayer == null) return;
        _passthroughLayer.SetBrightnessContrastSaturation(
            Mathf.Clamp(brightness, -1f, 1f), Mathf.Clamp(contrast, -1f, 1f), Mathf.Clamp(saturation, -1f, 1f));
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
