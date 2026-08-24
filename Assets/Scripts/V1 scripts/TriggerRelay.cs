using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TriggerRelay : MonoBehaviour
{
    [SerializeField] private TriggerSoundController parentController;

    private void Awake()
    {
        if (parentController == null)
            parentController = GetComponentInParent<TriggerSoundController>();

        if (parentController == null)
        {
            Debug.LogError($"[{gameObject.name}] TriggerRelay not assigned and cannot find TriggerSoundController in parent!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentController != null)
        {
            parentController.HandleTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentController != null)
        {
            parentController.HandleTriggerExit(other);
        }
    }
}