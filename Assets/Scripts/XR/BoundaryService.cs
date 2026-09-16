using UnityEngine;

public abstract class BoundaryService : MonoBehaviour, IBoundaryService
{
    public abstract void SetSuppressed(bool suppressed);
}
