using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Events;

public class TimelineController : MonoBehaviour
{
    public PlayableDirector director;

    [Header("Settings")]
    [Tooltip("If true, the timeline will play automatically when the GameObject starts.")]
    public bool playOnStart = false;

    [Header("Events")]
    public UnityEvent onTimelineStart;
    public UnityEvent onTimelineFinished;

    private bool hasPlayed = false; 

    private void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();

        // Safe check before subscribing
        if (director != null)
        {
            director.stopped += OnTimelineStopped;
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayTimeline();
        }
    }

    private void OnDestroy()
    {
        if (director != null) director.stopped -= OnTimelineStopped;
    }

    public void PlayTimeline()
    {
        if (director == null) return;

        if (hasPlayed) return;

        hasPlayed = true;

        onTimelineStart?.Invoke();
        director.Play();
    }

    private void OnTimelineStopped(PlayableDirector pd)
    {
        if (pd == director)
        {
            Debug.Log($"[{gameObject.name}] Timeline Finished!");
            onTimelineFinished?.Invoke();
        }
    }
}