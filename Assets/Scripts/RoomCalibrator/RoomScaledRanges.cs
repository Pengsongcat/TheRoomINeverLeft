using System.Collections.Generic;
using UnityEngine;

// On Room, next to RoomFitter. Scales what transform scale does not (or not evenly) when Room is fitted, so the
// soundscape and the trigger areas shrink and grow with the room like the positions do:
//   - every AudioSource under Room: min / max distance × room scale (5 m → 4 m at scale 0.8);
//   - every trigger sphere under Room: world radius × room scale. A sphere collider takes its radius from its
//     largest scale axis, and Room's height scale stays 1, so without this it would not shrink in a smaller room.
// Authored values are read in Awake, while Room is still unfitted (scale 1). Inactive story parts are included.
// Anything carrying KeepRealRange, or under something that does, is left at its authored range.
[RequireComponent(typeof(RoomFitter))]
public class RoomScaledRanges : MonoBehaviour
{
    struct Sound   { public AudioSource Source; public float Min, Max; }
    struct Trigger { public SphereCollider Collider; public float WorldRadius; }

    readonly List<Sound>   _sounds   = new();
    readonly List<Trigger> _triggers = new();
    RoomFitter _room;

    // For the Debugger: the min / max this sound was authored with, before any room scaling.
    public bool TryGetAuthoredRange(AudioSource source, out float min, out float max)
    {
        foreach (var s in _sounds)
            if (s.Source == source) { min = s.Min; max = s.Max; return true; }
        min = max = 0f;
        return false;
    }

    void Awake()
    {
        _room = GetComponent<RoomFitter>();

        foreach (var s in GetComponentsInChildren<AudioSource>(includeInactive: true))
            if (!KeepsRealRange(s))
                _sounds.Add(new Sound { Source = s, Min = s.minDistance, Max = s.maxDistance });

        foreach (var c in GetComponentsInChildren<SphereCollider>(includeInactive: true))
            if (c.isTrigger && !KeepsRealRange(c))
                _triggers.Add(new Trigger { Collider = c, WorldRadius = c.radius * MaxAxis(c.transform) });
    }

    static bool KeepsRealRange(Component c) =>
        c.GetComponentInParent<KeepRealRange>(includeInactive: true) != null;

    void OnEnable()
    {
        _room.Fitted += Apply;
        Apply(_room);
    }

    void OnDisable() => _room.Fitted -= Apply;

    void Apply(RoomFitter room)
    {
        float s = room.Scale;

        foreach (var sound in _sounds)
        {
            if (sound.Source == null) continue;
            sound.Source.minDistance = sound.Min * s;
            sound.Source.maxDistance = sound.Max * s;
        }

        foreach (var trigger in _triggers)
        {
            if (trigger.Collider == null) continue;
            float axis = MaxAxis(trigger.Collider.transform);
            if (axis > 0f) trigger.Collider.radius = trigger.WorldRadius * s / axis;
        }

        Debug.Log($"[RoomScaledRanges] Scale {s:0.00}: {_sounds.Count} sound ranges, {_triggers.Count} trigger spheres.");
    }

    static float MaxAxis(Transform t)
    {
        var l = t.lossyScale;
        return Mathf.Max(Mathf.Abs(l.x), Mathf.Abs(l.y), Mathf.Abs(l.z));
    }
}
