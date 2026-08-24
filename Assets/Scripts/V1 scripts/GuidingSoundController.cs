using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(BaseSound))]
[RequireComponent(typeof(Collider))] // Ensures a Collider is attached to the same GameObject
public class GuidingSoundController : MonoBehaviour
{
    [Header("Activation Settings")]
    [Tooltip("The time to wait before fading in the sound")]
    public float initialWait = 2f;
    [Tooltip("The time to wait after triggering sound and before activating the trigger.")]
    public float triggerWait = 2f;
    public bool autoActivate = true; // If true, the sound will start automatically on scene load

    [Tooltip("The tag of the object that can trigger this event (usually the Player).")]
    public string targetTag = "Player";

    [Header("Event Relay")]
    public UnityEvent onTargetEntered;

    private BaseSound baseSound;
    private Collider triggerCollider;

    private void Awake()
    {
        // Get the required components attached to this exact GameObject
        baseSound = GetComponent<BaseSound>();
        triggerCollider = GetComponent<Collider>();

        // Force the collider to act as a trigger and disable it initially
        // to prevent the player from triggering it during the initial wait period
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = false;
    }

    private void Start()
    {
        // Automatically activate the guiding sound if autoActivate is enabled
        if (autoActivate)
        {
            ActivateGuidingSound();
        }
    }

    /// <summary>
    /// Public interface: Called by a global Manager or at the end of the previous scene
    /// </summary>
    public void ActivateGuidingSound()
    {
        StartCoroutine(ActivationRoutine());
    }

    /// <summary>
    /// Coroutine to handle the wait time and activation sequence
    /// </summary>
    private IEnumerator ActivationRoutine()
    {
        if (initialWait > 0)
        {
            yield return new WaitForSeconds(initialWait);
        }
        baseSound.FadeIn();

        if (triggerWait > 0)
        {
            yield return new WaitForSeconds(triggerWait);
        }
        triggerCollider.enabled = true;
    }

    /// <summary>
    /// Unity's built-in physics trigger callback
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger has the correct tag
        if (other.CompareTag(targetTag))
        {
            // Immediately disable the collider to prevent multiple triggers
            triggerCollider.enabled = false;

            // baseSound.FadeOut();

            onTargetEntered?.Invoke();
        }
    }
}