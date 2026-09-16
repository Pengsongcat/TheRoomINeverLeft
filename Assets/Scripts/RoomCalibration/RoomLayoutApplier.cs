using UnityEngine;

// Applies a computed room fit to the Room Root and Sound Layout transforms.
// Instance Apply(RoomRect) computes the fit from serialized lab settings and calls Apply.
// Static Apply(rect, fit, roomRoot, soundLayout) is the testable core — no MonoBehaviour needed.
public class RoomLayoutApplier : MonoBehaviour
{
    [SerializeField] Transform    _roomRoot;
    [SerializeField] Transform    _soundLayout;
    [SerializeField] LabReference _lab  = new();
    [SerializeField] RoomFitMode  _mode = RoomFitMode.Even;

    public RoomFitResult LastFit { get; private set; }

    public void Apply(RoomRect rect)
    {
        LastFit = RoomFit.Compute(rect, _lab, _mode);
        Apply(rect, LastFit, _roomRoot, _soundLayout);
    }

    public static void Apply(RoomRect rect, RoomFitResult fit, Transform roomRoot, Transform soundLayout)
    {
        roomRoot.SetPositionAndRotation(
            new Vector3(rect.centre.x, rect.floorY, rect.centre.z),
            Quaternion.LookRotation(rect.forward, Vector3.up)
        );
        soundLayout.localScale    = new Vector3(fit.sx, 1f, fit.sz);
        soundLayout.localPosition = fit.soundLayoutLocalPosition;
    }
}
