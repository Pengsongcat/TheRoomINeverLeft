using UnityEngine;
using VLB;

// Fades a Volumetric Light Beam HD out as the visitor's head moves away from the beam's source: full strength up to
// Fade Start, gone at Fade End, linear in between. Distances are at room scale 1 and shrink or grow with the
// calibrated room, like the sound ranges and trigger spheres.
// When the beam takes its intensity from its spot light (as the Flower beam does, with the timeline animating the
// light), the fade scales the beam's Intensity Multiplier so the light animation still comes through; otherwise it
// scales the beam's own Intensity.
[RequireComponent(typeof(VolumetricLightBeamHD))]
public class BeamDistanceFade : MonoBehaviour
{
    [Tooltip("Up to this distance from the head the beam is at full strength, metres at room scale 1.")]
    [SerializeField, Min(0f)] float _fadeStart = 2f;
    [Tooltip("At this distance from the head the beam is fully faded out, metres at room scale 1.")]
    [SerializeField, Min(0f)] float _fadeEnd = 4f;

    VolumetricLightBeamHD _beam;
    RoomFitter _room;
    Transform  _head;
    bool       _fromLight;
    float      _authored; // multiplier or intensity, whichever the fade scales

    void Awake()
    {
        _beam = GetComponent<VolumetricLightBeamHD>();
        _room = GetComponentInParent<RoomFitter>(includeInactive: true);
        _fromLight = _beam.useIntensityFromAttachedLightSpot;
        _authored  = _fromLight ? _beam.intensityMultiplier : _beam.intensity;
    }

    void OnDisable() => Apply(1f);

    void Update()
    {
        if (_head == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _head = cam.transform;
        }

        float s = _room != null ? _room.Scale : 1f;
        float d = Vector3.Distance(_head.position, transform.position);
        Apply(1f - Mathf.InverseLerp(_fadeStart * s, Mathf.Max(_fadeEnd, _fadeStart) * s, d));
    }

    void Apply(float fade)
    {
        if (_beam == null) return;
        if (_fromLight) _beam.intensityMultiplier = _authored * fade;
        else            _beam.intensity           = _authored * fade;
    }
}
