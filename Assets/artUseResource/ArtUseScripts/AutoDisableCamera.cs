using UnityEngine;

public class AutoDisableCamera : MonoBehaviour
{
    [SerializeField] private bool disableOnAwake = true;
    void Awake()
    {
        if (disableOnAwake)
        {
            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            foreach (Camera cam in cameras)
            {
                cam.enabled = false;
            }
        }
    }
}
