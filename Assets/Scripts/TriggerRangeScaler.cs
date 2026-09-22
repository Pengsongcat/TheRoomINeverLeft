using UnityEngine;

// Keeps a trigger collider's real-world size in step with the room, alongside SoundRangeScaler.
//
// Unity already stretches a collider with its parent's scale, but by its own rules (a sphere follows the largest
// axis), so this script sets the collider size that lands on the size we want after Unity's stretch:
//   scaling  - designed size × the room factor, the smaller of the parent's X and Z, matching SoundRangeScaler
//   static   - the designed size in metres, whatever the room does; the parent's stretch is cancelled out
//
// Always computed from the designed values cached at Awake, so recalibrating never compounds.
[RequireComponent(typeof(Collider))]
public class TriggerRangeScaler : MonoBehaviour
{
    [Tooltip("On: the trigger keeps its designed size in metres in any room. Off: it shrinks and grows with the room.")]
    [SerializeField] bool _staticSize;

    Collider _collider;
    float    _designedRadius, _designedHeight;
    Vector3  _designedSize;
    Vector3  _appliedScale;
    float    _appliedFactor;

    void Awake()
    {
        _collider = GetComponent<Collider>();
        switch (_collider)
        {
            case SphereCollider sphere:   _designedRadius = sphere.radius; break;
            case BoxCollider box:         _designedSize   = box.size; break;
            case CapsuleCollider capsule: _designedRadius = capsule.radius; _designedHeight = capsule.height; break;
            default:
                Debug.LogWarning($"[TriggerRangeScaler] {name} has a {_collider.GetType().Name}, which is not scaled.");
                break;
        }
        _appliedScale  = Vector3.one;
        _appliedFactor = 1f;
    }

    // Before the trigger is used: its scene enables it as it starts waiting.
    void OnEnable() => Rescale();

    // Call this when the room is recalibrated while the trigger is already enabled.
    public void Rescale()
    {
        var  scale  = transform.lossyScale;
        float factor = _staticSize ? 1f : RoomFactor(scale);
        if (scale == _appliedScale && Mathf.Approximately(factor, _appliedFactor)) return;

        float x = Axis(scale.x), y = Axis(scale.y), z = Axis(scale.z);
        switch (_collider)
        {
            case SphereCollider sphere:
                // Unity scales a sphere by the largest axis.
                sphere.radius = _designedRadius * factor / Mathf.Max(x, y, z);
                break;
            case BoxCollider box:
                box.size = new Vector3(_designedSize.x * factor / x,
                                       _designedSize.y * factor / y,
                                       _designedSize.z * factor / z);
                break;
            case CapsuleCollider capsule:
                // Height follows the capsule's own axis; the radius follows the larger of the other two.
                float height = capsule.direction == 0 ? x : capsule.direction == 1 ? y : z;
                float radius = capsule.direction == 0 ? Mathf.Max(y, z)
                             : capsule.direction == 1 ? Mathf.Max(x, z)
                                                      : Mathf.Max(x, y);
                capsule.radius = _designedRadius * factor / radius;
                capsule.height = _designedHeight * factor / height;
                break;
        }

        _appliedScale  = scale;
        _appliedFactor = factor;
    }

    // The room's horizontal factor, the smaller of X and Z, so a stretched room never over-reaches.
    static float RoomFactor(Vector3 scale) => Mathf.Min(Axis(scale.x), Axis(scale.z));

    static float Axis(float value) => Mathf.Max(Mathf.Abs(value), 0.0001f);
}
