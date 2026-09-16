using UnityEngine;

public abstract class PassthroughService : MonoBehaviour, IPassthroughService
{
    public abstract bool IsActive { get; }
    public abstract float Opacity { get; set; }
    public abstract float Brightness { get; set; }
    public abstract void Enable();
    public abstract void Disable();
}
