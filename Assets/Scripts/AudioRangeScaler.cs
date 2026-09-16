using System.Collections.Generic;
using UnityEngine;

// Caches the designed AudioSource ranges and ProximityEventRelay trigger distances
// under the Sound Layout, then scales them by the room's range factor.
// Always scales from the original cached values, so recalibration never compounds.
//
// Lives in Assembly-CSharp (not an asmdef) so it can reference ProximityEventRelay.
public class AudioRangeScaler : MonoBehaviour
{
    [SerializeField] Transform _soundLayout;

    struct AudioEntry  { public AudioSource src; public float origMin, origMax; }
    struct ProximEntry { public ProximityEventRelay relay; public float origDist; }

    readonly List<AudioEntry>  _audio  = new();
    readonly List<ProximEntry> _proxim = new();
    bool _cached;

    void Awake()
    {
        if (_soundLayout != null) CacheOriginals();
    }

    // Call this when the sound layout is assigned after Awake (e.g. in tests or programmatic setup).
    public void Initialise(Transform soundLayout)
    {
        _soundLayout = soundLayout;
        CacheOriginals();
    }

    public void Scale(float rangeFactor)
    {
        if (!_cached)
        {
            if (_soundLayout == null) { Debug.LogWarning("[AudioRangeScaler] _soundLayout is not assigned."); return; }
            CacheOriginals();
        }
        foreach (var e in _audio)
        {
            e.src.minDistance = e.origMin * rangeFactor;
            e.src.maxDistance = e.origMax * rangeFactor;
        }
        foreach (var e in _proxim)
            e.relay.triggerDistance = e.origDist * rangeFactor;
    }

    void CacheOriginals()
    {
        _audio.Clear();
        _proxim.Clear();
        foreach (var s in _soundLayout.GetComponentsInChildren<AudioSource>(includeInactive: true))
            _audio.Add(new AudioEntry { src = s, origMin = s.minDistance, origMax = s.maxDistance });
        foreach (var p in _soundLayout.GetComponentsInChildren<ProximityEventRelay>(includeInactive: true))
            _proxim.Add(new ProximEntry { relay = p, origDist = p.triggerDistance });
        _cached = true;
    }
}
