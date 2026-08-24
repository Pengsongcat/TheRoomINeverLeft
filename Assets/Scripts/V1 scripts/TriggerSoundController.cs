using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct SoundClipData
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume;
}

[RequireComponent(typeof(BaseSound))]
public class TriggerSoundController : MonoBehaviour
{
    public enum TriggerMode
    {
        PlayOnce,
        LoopUntilExit
    }

    [Header("Trigger Settings")]
    public string targetTag = "Player";
    public TriggerMode playMode = TriggerMode.PlayOnce;
    public float triggerCooldown = 0.5f;
    public bool inactiveOnDeactivate = true;

    [Header("Sound Pool Settings")]
    public List<SoundClipData> soundClips = new List<SoundClipData>();

    [Header("Debug Settings")]
    public bool autoActivate = false;
    public bool showDebugLogs = false;

    private BaseSound baseSound;
    private Collider triggerCollider;

    private bool isActive = false;
    private float lastTriggerTime = -999f;

    private void Awake()
    {
        baseSound = GetComponent<BaseSound>();
        triggerCollider = GetComponentInChildren<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogWarning($"[{gameObject.name}] There is no trigger Collider in Children!");
            return;
        }

        // Ensure proper component setup
        triggerCollider.isTrigger = true;
        baseSound.inactivateOnFadeOut = false;
    }

    private void Start()
    {
        if (autoActivate)
        {
            ActivateTrigger();
        }
        else
        {
            isActive = false;
            if (triggerCollider != null) triggerCollider.enabled = false;
        }
    }

    public void ActivateTrigger()
    {
        if (isActive) return;

        isActive = true;
        if (triggerCollider != null)
            triggerCollider.enabled = true;

        if (showDebugLogs) Debug.Log($"[{gameObject.name}] Trigger ACTIVE.");
    }

    public void DeactivateTrigger()
    {
        if (!isActive) return;

        isActive = false;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        baseSound.inactivateOnFadeOut = inactiveOnDeactivate;
        baseSound.FadeOut();

        if (showDebugLogs) Debug.Log($"[{gameObject.name}] Trigger DEACTIVATED.");
    }

    public void HandleTriggerEnter(Collider other)
    {
        if (!isActive || !other.CompareTag(targetTag)) return;

        // Prevent multiple rapid triggers (debounce)
        if (Time.time - lastTriggerTime < triggerCooldown) return;

        // Prevent interrupting currently playing sound
        if (baseSound.IsPlaying) return;

        if (soundClips.Count == 0) return;

        lastTriggerTime = Time.time;
        SoundClipData randomData = soundClips[Random.Range(0, soundClips.Count)];

        if (randomData.clip == null) return;

        bool shouldLoop = (playMode == TriggerMode.LoopUntilExit);

        if (showDebugLogs) Debug.Log($"[{gameObject.name}] Playing: {randomData.clip.name} | Vol: {randomData.volume}");

        baseSound.PlayNewClip(randomData.clip, shouldLoop, randomData.volume);
    }

    public void HandleTriggerExit(Collider other)
    {
        if (!isActive || !other.CompareTag(targetTag)) return;

        if (playMode == TriggerMode.LoopUntilExit)
        {
            baseSound.FadeOut();
        }
    }
}