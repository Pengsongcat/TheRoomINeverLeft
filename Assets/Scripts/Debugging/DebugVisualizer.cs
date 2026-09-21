using UnityEngine;
using UnityEngine.Rendering;

// Base for every visualizer under the Debugger. The Debug Manager shows and hides them all together.
// A visualizer builds its own meshes under a hidden "Geometry" child on the Debug layer; the renderer skips
// that layer in its normal passes and draws it after post-processing, so no visual effect touches it.
// Materials should be unlit so lights, shadows and ambient occlusion don't either.
public abstract class DebugVisualizer : MonoBehaviour
{
    public const string LayerName = "Debug";

    GameObject _geometry;

    public bool IsVisible => _geometry != null && _geometry.activeSelf;

    protected Transform Geometry => _geometry.transform;

    protected virtual void Awake()
    {
        _geometry = new GameObject("Geometry") { layer = DebugLayer() };
        _geometry.transform.SetParent(transform, worldPositionStays: false);
        _geometry.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (_geometry != null) _geometry.SetActive(visible);
    }

    protected MeshFilter AddRenderer(string name, Material material)
    {
        var go = new GameObject(name) { layer = DebugLayer() };
        go.transform.SetParent(Geometry, worldPositionStays: false);

        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial       = material;
        r.shadowCastingMode    = ShadowCastingMode.Off;
        r.receiveShadows       = false;
        r.lightProbeUsage      = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return go.AddComponent<MeshFilter>();
    }

    static int DebugLayer()
    {
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer >= 0) return layer;
        Debug.LogWarning($"[DebugVisualizer] Layer '{LayerName}' is missing; debug meshes will be affected by visual effects.");
        return 0;
    }
}
