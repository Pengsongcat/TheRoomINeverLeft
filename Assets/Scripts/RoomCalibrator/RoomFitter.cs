using UnityEngine;

// Sits on Room. When the Room Calibrator has the room's corners, moves Room to the measured room's centre on
// its floor, turns it to face the front wall (corner 1 → 2) and scales it evenly on X/Z so the design space
// fits inside. Height scale stays 1. Without calibration Room stays where it is (the tracking origin), unscaled.
public class RoomFitter : MonoBehaviour
{
    [SerializeField] RoomCalibrator _calibrator;

    [Tooltip("Size of the designed space in metres, X (width) × Z (length). Must match the Plane.")]
    [SerializeField] Vector2 _designSize = new Vector2(5f, 5f);

    public float Scale { get; private set; } = 1f;

    void OnEnable()  => _calibrator.LayoutReady += Fit;
    void OnDisable() => _calibrator.LayoutReady -= Fit;

    void Fit(RoomRect rect)
    {
        Scale = Mathf.Min(rect.width / _designSize.x, rect.length / _designSize.y);
        transform.SetPositionAndRotation(
            new Vector3(rect.centre.x, rect.floorY, rect.centre.z),
            Quaternion.LookRotation(rect.forward, Vector3.up));
        transform.localScale = new Vector3(Scale, 1f, Scale);
        Debug.Log($"[RoomFitter] Room {rect.width:F2} × {rect.length:F2} m → scale {Scale:F2}");
    }
}
