using UnityEngine;

public class FollowXZ : MonoBehaviour
{
    public Transform target;
    public float fixedY = 0f;

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = new Vector3(
            target.position.x,
            fixedY,
            target.position.z
        );
    }
}