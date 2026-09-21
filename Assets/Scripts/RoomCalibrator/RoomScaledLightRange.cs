using UnityEngine;

// Put on a Light under Room. A light's range does not follow transform scale, so this multiplies the authored
// range by the room scale, once, when Room is fitted or when this light is enabled (5 m → 4 m at scale 0.8).
[RequireComponent(typeof(Light))]
public class RoomScaledLightRange : MonoBehaviour
{
    Light      _light;
    RoomFitter _room;
    float      _authoredRange;

    void Awake()
    {
        _light = GetComponent<Light>();
        _authoredRange = _light.range;
        _room = GetComponentInParent<RoomFitter>(includeInactive: true);
        if (_room == null) Debug.LogWarning($"[RoomScaledLightRange] {name} is not under Room; range stays as authored.", this);
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

    void Apply(RoomFitter room) => _light.range = _authoredRange * room.Scale;
}
