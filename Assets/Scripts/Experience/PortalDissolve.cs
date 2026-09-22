using UnityEngine;

// Put on the root of anything under the MR Portal that should dissolve with it; covers every renderer below it.
// The MR Portal finds these itself and cuts them all from the top down together. The materials must be able to
// be cut: 'TRINL/Portal' (openings, frame) or 'TRINL/Dissolve Lit' (models).
// Mark For MR: during MR the Passthrough Mask turns every pixel into passthrough unless it is marked as part of
// the portal (stencil bit 1). A model with a normal material doesn't mark its pixels, so with this on each renderer
// gets the portal's mark material added at runtime and shows in MR. Leave it off for 'TRINL/Portal' parts, which
// mark their pixels themselves.
public class PortalDissolve : MonoBehaviour
{
    static readonly int CutYId = Shader.PropertyToID("_CutY");

    [Tooltip("Add the portal's stencil mark so the model shows in MR. Off for parts that use 'TRINL/Portal'.")]
    [SerializeField] bool _markForMR = true;

    Renderer[] _renderers;
    MaterialPropertyBlock _block;

    // Called by the MR Portal before the first cut.
    public void Prepare(Material mark)
    {
        if (_renderers != null) return;
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _block = new MaterialPropertyBlock();
        if (!_markForMR || mark == null) return;

        foreach (var r in _renderers)
        {
            var materials = r.sharedMaterials;
            if (System.Array.IndexOf(materials, mark) >= 0) continue;
            System.Array.Resize(ref materials, materials.Length + 1);
            materials[^1] = mark; // an extra material redraws the last submesh
            r.sharedMaterials = materials;
        }
    }

    public void SetCut(float worldY)
    {
        _block.SetFloat(CutYId, worldY);
        foreach (var r in _renderers)
            if (r != null) r.SetPropertyBlock(_block);
    }
}
