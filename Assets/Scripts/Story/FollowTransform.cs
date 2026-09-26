using UnityEngine;

// Copies another object's world position and rotation every frame, after animation and Timelines have moved it.
// Lets a sound in a scene's Sound prefab ride on an object in its Visual prefab (e.g. the clock sound on the pendulum
// light). The target is usually in the other prefab, so set it on the scene prefab that holds both.
// Runs in Play mode only; in Edit mode the object stays where it was placed.
public class FollowTransform : MonoBehaviour
{
    [Tooltip("The object to follow: its world position and rotation are copied every frame.")]
    [SerializeField] Transform _target;

    void LateUpdate()
    {
        if (_target == null) return;
        transform.SetPositionAndRotation(_target.position, _target.rotation);
    }
}
