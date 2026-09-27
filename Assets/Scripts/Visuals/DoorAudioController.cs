using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DoorOpeningController))]
public sealed class DoorAudioController : MonoBehaviour
{
    public AudioClip firstOpening;
    public AudioClip fullOpening;
    public AudioSource source;
    [Range(0f, 1f)] public float firstVolume = 0.7f;
    [Range(0f, 1f)] public float fullVolume = 0.8f;
    public bool playSoundInPreview = true;
    DoorOpeningController door;
#if UNITY_EDITOR
    public static System.Action<AudioClip, float> PreviewPlay;
    public static System.Action PreviewStop;
    bool previewActive;
    public void BeginAudioPreview()
    {
        EndAudioPreview();
        door = GetComponent<DoorOpeningController>();
        door.OpeningStarted -= PlayOpening;
        door.OpeningStarted += PlayOpening;
        previewActive = true;
    }
    public void EndAudioPreview()
    {
        if (!previewActive) return;
        PreviewStop?.Invoke();
        if (door) door.OpeningStarted -= PlayOpening;
        previewActive = false;
    }
#endif
    void OnEnable()
    {
        door = GetComponent<DoorOpeningController>();
        door.OpeningStarted -= PlayOpening;
        door.OpeningStarted += PlayOpening;
    }
    void OnDisable()
    {
        if (door) door.OpeningStarted -= PlayOpening;
        if (source) source.Stop();
#if UNITY_EDITOR
        EndAudioPreview();
#endif
    }
    void PlayOpening(int stage)
    {
        AudioClip clip = stage == 1 ? firstOpening : fullOpening;
        float volume = stage == 1 ? firstVolume : fullVolume;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (previewActive && playSoundInPreview) PreviewPlay?.Invoke(clip, volume);
            return;
        }
#endif
        if (!source) return;
        // Replacing the clip cancels an interrupted opening rather than layering it.
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        if (clip) source.Play();
    }
}
