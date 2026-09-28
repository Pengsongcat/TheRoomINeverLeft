using UnityEngine;

/// <summary>Single front-facing door portal. Keeps the plane aligned when the prefab moves.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class DoorPortal : MonoBehaviour
{
    public MeshRenderer aperture;
    public MeshRenderer depthReset;
    public Renderer[] interiorRenderers;
    [Tooltip("Door leaf and frame to restore after clearing the portal depth; keeps inward-opening doors visible.")]
    public Renderer[] retainedRenderers;
    public Vector3 localFrontNormal = Vector3.up;
    [ColorUsage(false,true)] public Color whiteEmission = new Color(2f,2f,2f,1f);
    [Range(0f,0.5f)] public float surfaceDefinition = 0.12f;
    [Header("Interior Lighting")]
    [Tooltip("Dedicated point light. Set its Culling Mask to Nothing and Mode to Realtime to prevent lighting the outside scene.")]
    public Light interiorLight;
    [Tooltip("Room brightness before the dedicated light is added. 1 preserves the original appearance.")]
    [Range(0f,1f)] public float interiorBaseBrightness = 1f;
    [Header("Walk Through")]
    public bool allowEntry = true;
    [Tooltip("Start the transition just before the camera near plane clips the doorway.")]
    [Min(0f)] public float entryPadding = 0.02f;
    [Header("Inside Passthrough")]
    [Tooltip("Virtual wall opacity after entering: 1 hides passthrough, 0 reveals it fully.")]
    [Range(0f,1f)] public float insideWallOpacity = 0.7f;
    [Min(0f)] public float insideFadeDuration = 1f;
    sealed class CameraState { public Vector3 previous; public bool inside; public double enteredAt; }
    public float GetInsideWallOpacity(Camera camera)
    {
        if (!camera || !cameraStates.TryGetValue(camera, out var state) || !state.inside) return 1f;
        float t = insideFadeDuration <= 0f ? 1f : Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - state.enteredAt) / insideFadeDuration);
        return Mathf.Lerp(1f, insideWallOpacity, t*t*(3f-2f*t));
    }
    readonly System.Collections.Generic.Dictionary<Camera, CameraState> cameraStates = new System.Collections.Generic.Dictionary<Camera, CameraState>();
    bool lastPlayState;
    public bool IsCameraInside(Camera camera)
    {
        if (!allowEntry || !camera || !aperture) return false;
        if (lastPlayState != Application.isPlaying) { cameraStates.Clear(); lastPlayState = Application.isPlaying; }
        var filter = aperture.GetComponent<MeshFilter>();
        if (!filter || !filter.sharedMesh) return false;
        Vector3 normal = aperture.transform.TransformDirection(localFrontNormal).normalized;
        Vector3 center = aperture.transform.TransformPoint(filter.sharedMesh.bounds.center);
        Vector3 current = camera.transform.position;
        if (!cameraStates.TryGetValue(camera, out var state))
        {
            state = new CameraState { previous = current };
            cameraStates.Add(camera, state);
        }
        float before = Vector3.Dot(state.previous - center, normal);
        float now = Vector3.Dot(current - center, normal);
        float boundary = camera.nearClipPlane + entryPadding;
        if (!state.inside && before > boundary && now <= boundary)
        {
            float t = (before - boundary) / (before - now);
            Vector3 crossing = Vector3.Lerp(state.previous, current, t);
            // Project onto the actual aperture plane and test its local rectangle.
            crossing -= normal * Vector3.Dot(crossing - center, normal);
            Vector3 local = aperture.transform.InverseTransformPoint(crossing);
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 axis = localFrontNormal.normalized;
            Vector3 delta = local - bounds.center;
            int normalAxis = Mathf.Abs(axis.x) > Mathf.Abs(axis.y) ? 0 : 1;
            if (Mathf.Abs(axis.z) > Mathf.Abs(axis[normalAxis])) normalAxis = 2;
            bool fits = true;
            for (int i = 0; i < 3; i++) if (i != normalAxis && Mathf.Abs(delta[i]) > bounds.extents[i]) fits = false;
            state.inside = fits;
            if (fits) state.enteredAt = Time.realtimeSinceStartupAsDouble;
        }
        if (state.inside && now > boundary + 0.02f) state.inside = false;
        // Walking out through a side/back wall must not reveal the room from outside.
        if (state.inside && now < -0.1f && interiorRenderers != null && interiorRenderers.Length > 0)
        {
            bool haveBounds = false; Bounds roomBounds = default;
            foreach (var renderer in interiorRenderers) if (renderer)
            {
                if (!haveBounds) { roomBounds = renderer.bounds; haveBounds = true; }
                else roomBounds.Encapsulate(renderer.bounds);
            }
            roomBounds.Expand(0.1f);
            if (haveBounds && !roomBounds.Contains(current)) state.inside = false;
        }
        state.previous = current;
        return state.inside;
    }
    [ContextMenu("Reset Camera Entry State")]
    public void ResetCameraEntryState() { cameraStates.Clear(); }
    // Reserved rendering-layer bit for the portal's lit door redraw. Keep ordinary light layers intact.
    public const uint RetainedRenderingLayer = 1u << 31;
    readonly System.Collections.Generic.HashSet<Renderer> markedRenderers = new System.Collections.Generic.HashSet<Renderer>();
    void MarkRetainedRenderers()
    {
        if (retainedRenderers == null) return;
        foreach (var item in retainedRenderers)
            if (item && (item.renderingLayerMask & RetainedRenderingLayer) == 0)
            {
                markedRenderers.Add(item);
                item.renderingLayerMask |= RetainedRenderingLayer;
            }
    }
    MaterialPropertyBlock block;
    public static readonly System.Collections.Generic.List<DoorPortal> Active = new System.Collections.Generic.List<DoorPortal>();
    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); Refresh(); }
    void OnDisable() { Active.Remove(this); cameraStates.Clear(); foreach (var item in markedRenderers) if(item) item.renderingLayerMask &= ~RetainedRenderingLayer; markedRenderers.Clear(); }
    void LateUpdate() { Refresh(); }
    void OnValidate() { Refresh(); }
    public void Refresh()
    {
        MarkRetainedRenderers();
        if (!aperture) return;
        var mesh=aperture.GetComponent<MeshFilter>();
        if (!mesh || !mesh.sharedMesh) return;
        Vector3 normal=aperture.transform.TransformDirection(localFrontNormal).normalized;
        Vector3 center=aperture.transform.TransformPoint(mesh.sharedMesh.bounds.center);
        Vector4 plane=new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(normal,center));
        Apply(aperture,plane); Apply(depthReset,plane);
        if(interiorRenderers!=null)foreach(var renderer in interiorRenderers)Apply(renderer,plane);
    }
    void Apply(Renderer renderer,Vector4 plane)
    {
        if(!renderer)return;
        if(block==null)block=new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetVector("_PortalPlane",plane);
        block.SetColor("_BaseColor",whiteEmission);
        block.SetFloat("_SurfaceDefinition",surfaceDefinition);
        block.SetFloat("_InteriorBaseBrightness", interiorBaseBrightness);
        bool lit = interiorLight && interiorLight.isActiveAndEnabled && interiorLight.type == LightType.Point;
        Vector3 lightPosition = lit ? interiorLight.transform.position : Vector3.zero;
        block.SetVector("_InteriorLightPositionRange", new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, lit ? Mathf.Max(0.001f, interiorLight.range) : 1f));
        Color lightColor = lit ? interiorLight.color : Color.black;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) lightColor = lightColor.linear;
        block.SetColor("_InteriorLightColor", lightColor * (lit ? Mathf.Max(0f, interiorLight.intensity) : 0f));
        renderer.SetPropertyBlock(block);
    }
}



