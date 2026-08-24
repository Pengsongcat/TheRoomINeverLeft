using UnityEngine;
using UnityEngine.Events; // Required for UnityEvent

[RequireComponent(typeof(BaseSound))] // Ensures a BaseSound component is attached to the same GameObject
public class OnSoundFinishEventRelay : MonoBehaviour
{
    [Header("Event Settings")]
    [Tooltip("Triggered when the attached BaseSound finishes its FadeOut process or finishes playing.")]
    public UnityEvent onPlaybackFinished;

    private BaseSound baseSound;

    private void Awake()
    {
        // Get the BaseSound component attached to the same GameObject
        baseSound = GetComponent<BaseSound>();

        if (baseSound == null)
        {
            Debug.LogError($"[{gameObject.name}] BaseSound component not found! Event Relay requires it to function.");
        }
    }

    private void OnEnable()
    {
        // Subscribe to the C# event from BaseSound
        if (baseSound != null)
        {
            baseSound.OnFadeOutFinished += HandlePlaybackFinished;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from the C# event to prevent memory leaks and null reference errors
        if (baseSound != null)
        {
            baseSound.OnFadeOutFinished -= HandlePlaybackFinished;
        }
    }

    /// <summary>
    /// This method is called automatically when BaseSound finishes fading out.
    /// </summary>
    private void HandlePlaybackFinished()
    {
        // Invoke the UnityEvent configured in the inspector
        onPlaybackFinished?.Invoke();
    }
}