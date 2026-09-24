using UnityEngine;

// Keeps turning this object around its own axis, so everything under it orbits its origin. For sky objects: put
// them under an empty pivot at the room centre and this on the pivot. Speed can be animated by a Timeline (e.g.
// ease in from 0, slow to a stop), which shapes the turning while this keeps it going for as long as it is on.
public class SpinAround : MonoBehaviour
{
    [Tooltip("Degrees per second. Negative turns the other way. Can be animated by a Timeline.")]
    [SerializeField] float _speed = 5f;
    [Tooltip("Axis in this object's own space. Up = around the vertical, like a sky turning overhead.")]
    [SerializeField] Vector3 _axis = Vector3.up;
    [Tooltip("On: follows scaled time, so the story's fast-forward speeds it up too. Off: real time.")]
    [SerializeField] bool _scaledTime = true;

    void Update()
    {
        if (_speed == 0f || _axis == Vector3.zero) return;
        float dt = _scaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
        transform.Rotate(_axis, _speed * dt, Space.Self);
    }
}
