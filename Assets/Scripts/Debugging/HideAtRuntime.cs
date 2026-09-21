using UnityEngine;

// Development-only visuals (e.g. the 5 × 5 m design Plane): visible while editing, hidden in Play mode and builds.
// Only the renderers are turned off; the object stays, so tools that read its shape (the Plane Visualizer) work.
public class HideAtRuntime : MonoBehaviour
{
    void Awake()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(includeInactive: true))
            r.enabled = false;
    }
}
