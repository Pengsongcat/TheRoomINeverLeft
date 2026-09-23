using System;
using UnityEngine;
using UnityEngine.Playables;

// One scene of a chapter. While it waits, its guide sound loops and its trigger is on. When the visitor walks into
// the trigger, Triggered is raised so the chapter can fade the guides. The chapter starts this scene's
// Timeline after the other guides have faded out. When it ends, Finished is raised.
public class StoryScene : MonoBehaviour
{
    [SerializeField] SceneTrigger _trigger;
    [Tooltip("Looping sound that leads the visitor to the trigger. Play On Awake off.")]
    [SerializeField] AudioSource _guide;
    [Tooltip("Wrap Mode must be None, so the director stops at the end.")]
    [SerializeField] PlayableDirector _timeline;

    public event Action<StoryScene> Triggered;
    public event Action<StoryScene> Finished;

    public bool Done { get; private set; }

    // For the Debugger: which sound leads to this scene, and whether its Timeline is running now.
    public AudioSource Guide   => _guide;
    public bool        Playing => _playing;

    bool _playing;
    bool _triggered;
    GuideSoundFader _guideFader;

    void Awake()
    {
        _trigger.Entered += OnEntered;
        if (_guide == null) return;
        _guideFader = _guide.GetComponent<GuideSoundFader>();
        if (_guideFader == null)
            Debug.LogError($"[Story] {name}: guide sound needs the Guiding Sound prefab variant.", this);
    }

    void OnEnable()
    {
        _timeline.stopped += OnStopped;
        if (!Done && !_triggered) Wait();
    }

    void OnDisable()
    {
        _timeline.stopped -= OnStopped;
        if (_playing) { _playing = false; _timeline.Stop(); }
        _triggered = false;
        if (_guideFader != null) _guideFader.StopImmediate();
    }

    // Called by the chapter when it begins, so the scene can play again.
    public void ResetProgress() { Done = false; _triggered = false; }

    void Wait()
    {
        _trigger.gameObject.SetActive(true);
        if (_guideFader != null) _guideFader.FadeIn();
    }

    public void FadeOutGuide(float durationMultiplier = 1f, Action onComplete = null)
    {
        _trigger.gameObject.SetActive(false);
        if (_guideFader != null) _guideFader.FadeOut(durationMultiplier, onComplete);
        else onComplete?.Invoke();
    }

    public void ResumeWaiting() { if (!Done && !_triggered) Wait(); }

    public void StartTimeline()
    {
        if (!_triggered || _playing || Done || !isActiveAndEnabled) return;
        _playing = true;
        _timeline.time = 0;
        _timeline.Play();
    }

    void OnEntered()
    {
        if (_triggered || Done) return;
        _triggered = true;
        _trigger.gameObject.SetActive(false);
        Debug.Log($"[Story] {name} triggered.");
        Triggered?.Invoke(this);
    }

    void OnStopped(PlayableDirector director)
    {
        // Also raised when the director is disabled with the scene; only a Timeline that ran to its end counts.
        if (!_playing || !isActiveAndEnabled) return;
        _playing = false;
        Done = true;
        Debug.Log($"[Story] {name} finished.");
        Finished?.Invoke(this);
    }
}
