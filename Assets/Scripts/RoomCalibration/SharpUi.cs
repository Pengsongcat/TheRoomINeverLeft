using UnityEngine;

// Head-locked text that must stay sharp under the distance blur (plan §8.7). Objects on this layer are left out
// of VR_Renderer's normal opaque / transparent passes and drawn by a Render Objects feature after
// post-processing, on top of everything.
public static class SharpUi
{
    public const string LayerName = "Sharp UI";

    // Puts root and all its children on the Sharp UI layer (no-op if the layer is missing).
    public static void Apply(GameObject root)
    {
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[SharpUi] Layer '{LayerName}' missing; {root.name} will be blurred.");
            return;
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            t.gameObject.layer = layer;
    }
}
