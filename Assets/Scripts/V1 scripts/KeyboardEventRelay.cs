using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class KeyboardEventRelay : MonoBehaviour
{
    [Header("Key Settings")]
    public Key triggerKey = Key.Space;

    [Tooltip("Default is true. If enabled, the event will only fire once.")]
    public bool triggerOnlyOnce = true;

    [Header("Event")]
    public UnityEvent onKeyPressed;

    private bool hasTriggered = false;

    void Update()
    {
        if (triggerOnlyOnce && hasTriggered) return;

        if (Keyboard.current == null) return;

        if (Keyboard.current[triggerKey].wasPressedThisFrame)
        {
            onKeyPressed?.Invoke();

            if (triggerOnlyOnce)
            {
                hasTriggered = true;
            }
        }
    }

    public void ResetTriggerState()
    {
        hasTriggered = false;
    }
}
