using UnityEngine;

// Plays the step sound while this object is moving and pauses it while it stands still.
public class StepSound : MonoBehaviour
{
    [Tooltip("The step sound: Loop on, Play On Awake off.")]
    [SerializeField] AudioSource _steps;

    Vector3 _last;

    void OnEnable() => _last = transform.position;

    // After Update, so this frame's move by the Timeline has already happened.
    void LateUpdate()
    {
        bool moving = transform.position != _last;
        _last = transform.position;

        if (moving && !_steps.isPlaying) _steps.Play();
        else if (!moving && _steps.isPlaying) _steps.Pause();
    }
}
