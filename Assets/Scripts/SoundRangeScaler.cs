using UnityEngine;

// Keeps an AudioSource's Min/Max distance in step with the room. Unity does not scale audio ranges with the
// transform, so a layout shrunk to fit a small room would keep its designed ranges and reach too far.
//
// The factor comes from this object's lossy scale (the calibration scales a parent, e.g. Room Root), taking the
// smaller of X and Z so a stretched room never over-reaches. Rescaling always starts from the designed values
// cached at Awake, so recalibrating never compounds.
[RequireComponent(typeof(AudioSource))]
public class SoundRangeScaler : MonoBehaviour
{
    AudioSource _source;
    float       _designedMin, _designedMax;
    float       _applied;

    void Awake()
    {
        _source      = GetComponent<AudioSource>();
        _designedMin = _source.minDistance;
        _designedMax = _source.maxDistance;
        _applied     = 1f;
    }

    // Before the sound plays: the guides are enabled by their scene right before Play.
    void OnEnable() => Rescale();

    // Call this when the room is recalibrated while the sound is already enabled.
    public void Rescale()
    {
        float factor = RoomFactor();
        if (Mathf.Approximately(factor, _applied)) return;

        _source.minDistance = _designedMin * factor;
        _source.maxDistance = _designedMax * factor;
        _applied            = factor;
    }

    float RoomFactor()
    {
        var scale = transform.lossyScale;
        return Mathf.Max(Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.z)), 0.0001f);
    }
}
