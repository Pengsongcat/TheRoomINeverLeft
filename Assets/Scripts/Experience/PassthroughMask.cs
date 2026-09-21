using UnityEngine;

// Limits what shows passthrough while the MR portal is open. Draws one quad with a 'TRINL/Passthrough Mask'
// material, kept in front of the camera so it is never culled (the shader maps it to the full screen itself).
//   ShowMask(): before entering the portal, the VR world is visible only through the portal's opening.
//   ShowFill(): after entering, the VR world is everywhere and only the doorway behind shows the real room.
public class PassthroughMask : MonoBehaviour
{
    [Tooltip("'TRINL/Passthrough Mask' material that turns everything outside the portal into passthrough.")]
    [SerializeField] Material _maskMaterial;
    [Tooltip("'TRINL/Passthrough Mask' material that makes everything outside the portal opaque.")]
    [SerializeField] Material _fillMaterial;

    GameObject   _quad;
    MeshRenderer _renderer;

    void Awake()
    {
        _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quad.name = "Passthrough Mask Quad";
        Destroy(_quad.GetComponent<Collider>());
        _renderer = _quad.GetComponent<MeshRenderer>();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _quad.transform.SetParent(transform, worldPositionStays: false);
        _quad.SetActive(false);
    }

    public void ShowMask() => Show(_maskMaterial);
    public void ShowFill() => Show(_fillMaterial);
    public void Hide()     => _quad.SetActive(false);

    void Show(Material material)
    {
        var cam = Camera.main;
        if (cam != null && _quad.transform.parent != cam.transform)
        {
            _quad.transform.SetParent(cam.transform, worldPositionStays: false);
            _quad.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            _quad.transform.localRotation = Quaternion.identity;
        }
        _renderer.sharedMaterial = material;
        _quad.SetActive(true);
    }

    void OnDestroy()
    {
        if (_quad != null) Destroy(_quad);
    }
}
