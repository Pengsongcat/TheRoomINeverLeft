using UnityEngine;

// Put on the root of anything under Room that should follow the room's position and rotation but keep its real
// size (visual models, the MR portal). Room is scaled (s, 1, s) by calibration; this cancels that scale on this
// object once, when Room is fitted or when this object is enabled. Its position still scales with the room.
//
// Rotate this object around Y only; tilt its children instead. A tilted object under Room's (s, 1, s) scale is
// skewed, and its own scale cannot undo that.
public class KeepRealSize : MonoBehaviour
{
    RoomFitter _room;
    Vector3    _authoredScale;

    void Awake()
    {
        _authoredScale = transform.localScale;
        _room = GetComponentInParent<RoomFitter>(includeInactive: true);
        if (_room == null) Debug.LogWarning($"[KeepRealSize] {name} is not under Room; nothing to cancel.", this);
    }

    void OnEnable()
    {
        if (_room == null) return;
        _room.Fitted += Apply;
        Apply(_room);
    }

    void OnDisable()
    {
        if (_room != null) _room.Fitted -= Apply;
    }

    void Apply(RoomFitter room)
    {
        float s = room.Scale;
        transform.localScale = new Vector3(_authoredScale.x / s, _authoredScale.y, _authoredScale.z / s);

        var up = Quaternion.Inverse(room.transform.rotation) * transform.rotation * Vector3.up;
        if (up.y < 0.999f)
            Debug.LogWarning($"[KeepRealSize] {name} is tilted relative to Room, so the room scale skews it. " +
                             "Rotate it around Y only and tilt a child instead.", this);
    }
}
