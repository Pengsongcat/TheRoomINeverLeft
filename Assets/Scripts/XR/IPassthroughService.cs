public interface IPassthroughService
{
    bool IsActive { get; }
    float Opacity { get; set; }
    float Brightness { get; set; } // -1 = black, 0 = unchanged, 1 = brightest
    // true = drawn over the virtual scene, blended with it by Opacity; false = underneath it (the default).
    bool OnTop { get; set; }
    // Each -1..1, 0 = unchanged; saturation -1 = grey. Brightness alone resets contrast and saturation to 0.
    void SetColorAdjustment(float brightness, float contrast, float saturation);
    void Enable();
    void Disable();
}
