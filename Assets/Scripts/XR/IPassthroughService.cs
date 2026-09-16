public interface IPassthroughService
{
    bool IsActive { get; }
    float Opacity { get; set; }
    void Enable();
    void Disable();
}
