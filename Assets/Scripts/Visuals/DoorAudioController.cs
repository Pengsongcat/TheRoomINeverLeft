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
    [Tooltip("Seconds the previous stage fades out while the next one fades in. 0 = cut straight to the next stage.")]
    [Min(0f)] public float crossfade = 0f;
    public bool playSoundInPreview = true;
    DoorOpeningController door;
    AudioSource other;   // a copy of source, so one stage can fade out while the next fades in
    AudioSource current;
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
        if (other) other.Stop();
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
        if (crossfade > 0f)
        {
            // The next stage plays on the idle source and fades in while the playing one fades out.
            if (!other) other = Instantiate(source, source.transform.parent);
            var previous = current ? current : source;
            current = previous == source ? other : source;
            StopAllCoroutines();
            current.Stop();
            current.clip = clip;
            current.volume = 0f;
            if (clip) current.Play();
            StartCoroutine(Crossfade(previous, current, volume));
            return;
        }
        // Replacing the clip cancels an interrupted opening rather than layering it.
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        if (clip) source.Play();
    }
    System.Collections.IEnumerator Crossfade(AudioSource from, AudioSource to, float volume)
    {
        float fromVolume = from.volume;
        for (float t = 0f; t < crossfade; t += Time.deltaTime)
        {
            float k = t / crossfade;
            from.volume = fromVolume * (1f - k);
            to.volume = volume * k;
            yield return null;
        }
        from.Stop();
        to.volume = volume;
    }
}
