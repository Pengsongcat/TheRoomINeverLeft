using System;
using UnityEngine;

// Sits on Room. When the Room Calibrator has the room's corners, moves Room to the measured room's centre on
// its floor and turns it to face the front wall (corner 1 → 2). No resizing for now: Room stays at scale 1, so
// everything that follows the room scale stays at its authored size. Without calibration Room stays where it is
// (the tracking origin).
public class RoomFitter : MonoBehaviour
{
    public const string ScaleMethod = "None";

    [SerializeField] RoomCalibrator _calibrator;

    [Tooltip("Size of the designed space in metres, X (width) × Z (length). Must match the Plane.")]
    [SerializeField] Vector2 _designSize = new Vector2(5f, 5f);

    public bool    Calibrated   { get; private set; }
    public Vector2 MeasuredSize { get; private set; } // width × length of the calibrated room, metres
    public float   Scale        { get; private set; } = 1f;
    public Vector2 DesignSize   => _designSize;

    // Raised after Room has been moved, turned and scaled.
    public event Action<RoomFitter> Fitted;

    // The design space as it is now in the world, width × length, metres.
    public Vector2 CurrentSize
    {
        get
        {
            var s = transform.lossyScale;
            return new Vector2(_designSize.x * Mathf.Abs(s.x), _designSize.y * Mathf.Abs(s.z));
        }
    }

    void OnEnable()  => _calibrator.LayoutReady += Fit;
    void OnDisable() => _calibrator.LayoutReady -= Fit;

    void Fit(RoomRect rect)
    {
        Calibrated   = true;
        MeasuredSize = new Vector2(rect.width, rect.length);
        Scale = 1f;
        transform.SetPositionAndRotation(
            new Vector3(rect.centre.x, rect.floorY, rect.centre.z),
            Quaternion.LookRotation(rect.forward, Vector3.up));
        transform.localScale = Vector3.one;
        Debug.Log($"[RoomFitter] Room {rect.width:F2} × {rect.length:F2} m → placed and turned, not scaled");
        Fitted?.Invoke(this);
    }
}
