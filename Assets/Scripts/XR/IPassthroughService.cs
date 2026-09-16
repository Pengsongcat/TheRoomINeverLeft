public interface IPassthroughService
{
    bool IsActive { get; }
    float Opacity { get; set; }
    float Brightness { get; set; } // -1 = black, 0 = unchanged, 1 = brightest
    void Enable();
    void Disable();
}
