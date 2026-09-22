using System;
using UnityEngine;
using UnityEngine.Playables;

// One scene of a chapter. While it waits, its guide sound loops and its trigger is on. When the visitor walks into
// the trigger, the guide stops, Triggered is raised (the chapter turns the other scenes off) and the scene's own
// Timeline plays. When the Timeline ends, Finished is raised and the chapter turns this scene off.
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

    void Awake() => _trigger.Entered += OnEntered;

    void OnEnable()
    {
        _timeline.stopped += OnStopped;
        if (!Done && !_playing) Wait();
    }

    void OnDisable()
    {
        _timeline.stopped -= OnStopped;
        if (_playing) { _playing = false; _timeline.Stop(); }
    }

    // Called by the chapter when it begins, so the scene can play again.
    public void ResetProgress() => Done = false;

    void Wait()
    {
        _trigger.gameObject.SetActive(true);
        if (_guide != null) _guide.Play();
    }

    void OnEntered()
    {
        if (_playing || Done) return;
        _playing = true;
        _trigger.gameObject.SetActive(false);
        if (_guide != null) _guide.Stop();
        Debug.Log($"[Story] {name} triggered.");
        Triggered?.Invoke(this);

        _timeline.time = 0;
        _timeline.Play();
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
