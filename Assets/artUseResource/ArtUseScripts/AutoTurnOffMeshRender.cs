using UnityEngine;

public class AutoTurnOffMeshRender : MonoBehaviour
{
    [SerializeField]
    private bool turnOffChildMeshRenderers = false;

    void Start()
    {
        if (!turnOffChildMeshRenderers)
        {
            return;
        }

        MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in childRenderers)
        {
            if (renderer.transform == transform)
            {
                continue;
            }

            renderer.enabled = false;
        }
    }
}
