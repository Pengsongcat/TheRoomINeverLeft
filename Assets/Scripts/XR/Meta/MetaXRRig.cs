using System.Collections;
using UnityEngine;

public class MetaXRRig : XRRig
{
    [SerializeField] Transform _centerEyeAnchor;
    [Tooltip("OVRCameraRig/TrackingSpace. With a Floor Level tracking origin, its height is the system floor.")]
    [SerializeField] Transform _trackingSpace;

    public override Transform CenterEyeAnchor => _centerEyeAnchor;
    public override float FloorY => _trackingSpace != null ? _trackingSpace.position.y : 0f;

    IEnumerator Start()
    {
        if (_trackingSpace == null)
            Debug.LogWarning("[MetaXRRig] Tracking space is not assigned; floor height falls back to 0.");

        // The runtime applies the configured origin a few frames after startup; checking in Start reports EyeLevel.
        const float timeout = 10f;
        for (float t = 0f; t < timeout; t += Time.unscaledDeltaTime)
        {
            if (OVRManager.instance == null) yield break;
            if (OVRManager.instance.trackingOriginType == OVRManager.TrackingOrigin.FloorLevel) yield break;
            yield return null;
        }
        Debug.LogWarning($"[MetaXRRig] Tracking origin is {OVRManager.instance.trackingOriginType}, not FloorLevel; " +
                         "the system floor height will be wrong.");
    }
}
