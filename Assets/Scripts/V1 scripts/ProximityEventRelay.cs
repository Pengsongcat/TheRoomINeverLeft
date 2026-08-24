using UnityEngine;
using UnityEngine.Events;

public class ProximityEventRelay : MonoBehaviour
{
    [Header("Proximity Settings")]
    [Tooltip("The VR player's tracked transform, normally CenterEyeAnchor.")]
    public Transform playerTransform;

    [Tooltip("The point the player must approach.")]
    public Transform startPoint;

    [Min(0f)]
    [Tooltip("The trigger radius in metres.")]
    public float triggerDistance = 1f;

    [Tooltip("Ignore vertical distance and measure only on the floor plane.")]
    public bool ignoreHeight = true;

    [Tooltip("If enabled, the event can only fire once until ResetTriggerState is called.")]
    public bool triggerOnlyOnce = true;

    [Header("Event")]
    public UnityEvent onTriggered;

    private bool hasTriggered;

    private void Update()
    {
        if ((triggerOnlyOnce && hasTriggered) || playerTransform == null || startPoint == null)
            return;

        Vector3 playerPosition = playerTransform.position;
        Vector3 startPosition = startPoint.position;

        if (ignoreHeight)
        {
            playerPosition.y = 0f;
            startPosition.y = 0f;
        }

        if ((playerPosition - startPosition).sqrMagnitude >= triggerDistance * triggerDistance)
            return;

        onTriggered?.Invoke();
        hasTriggered = true;
    }

    public void ResetTriggerState()
    {
        hasTriggered = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (startPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPoint.position, triggerDistance);
    }
}
