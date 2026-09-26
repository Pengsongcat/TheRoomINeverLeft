using UnityEngine;

// Plays one step per stride while this object moves, alternating the left and right foot's sources.
public class StrideStepSound : MonoBehaviour
{
    [Tooltip("Left foot, playing its own clip: Loop off, Play On Awake off. Place it where the left foot should sound.")]
    [SerializeField] AudioSource _left;
    [Tooltip("Right foot, playing its own clip: Loop off, Play On Awake off. Place it where the right foot should sound.")]
    [SerializeField] AudioSource _right;

    [Tooltip("Horizontal distance between two steps, in meters.")]
    [SerializeField] float _stride = 0.65f;
    [SerializeField] float _pitchJitter = 0.05f;
    [SerializeField] float _volumeJitter = 0.1f;

    Vector3 _last;
    float _travelled;
    bool _leftNext = true;

    void OnEnable()
    {
        _last = transform.position;
        _travelled = 0f;
    }

    // After Update, so this frame's move by the Timeline has already happened.
    void LateUpdate()
    {
        Vector3 delta = transform.position - _last;
        _last = transform.position;
        delta.y = 0f;

        // Standing still, or jumped (a Move Clip starting from the headset): start the next walk with a fresh stride.
        if (delta.sqrMagnitude < 1e-8f || delta.magnitude > _stride)
        {
            _travelled = 0f;
            return;
        }

        _travelled += delta.magnitude;
        if (_travelled < _stride) return;

        _travelled -= _stride;
        Step(_leftNext ? _left : _right);
        _leftNext = !_leftNext;
    }

    void Step(AudioSource source)
    {
        if (source == null || source.clip == null) return;

        source.pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
        source.PlayOneShot(source.clip, 1f - Random.Range(0f, _volumeJitter));
    }
}
