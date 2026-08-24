using UnityEngine;

public class FollowPosition : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position;
    }
}