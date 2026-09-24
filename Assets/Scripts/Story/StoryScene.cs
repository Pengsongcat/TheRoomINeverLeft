using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// One scene of a chapter. While it waits, its guide sound loops and its trigger is on. When the visitor walks into
// the trigger, Triggered is raised so the chapter can fade the guides. The chapter starts this scene's
// Timeline after the other guides have faded out. When it ends, Finished is raised.
// With Guide On and Guide Off set, a waiting scene pulses: its guide sounds with the trigger armed for Guide On
// seconds, then fades out with the trigger disarmed and stays silent for Guide Off seconds, and again.
public class StoryScene : MonoBehaviour
{
    [SerializeField] SceneTrigger _trigger;
    [Tooltip("Looping sound that leads the visitor to the trigger. Play On Awake off.")]
    [SerializeField] AudioSource _guide;
    [Tooltip("Wrap Mode must be None, so the director stops at the end.")]
    [SerializeField] PlayableDirector _timeline;
    [Tooltip("Seconds the guide sounds (trigger armed) from the start of its fade-in. 0 = no pulse: sounds until triggered.")]
    [SerializeField, Min(0f)] float _guideOnDuration = 0f;
    [Tooltip("Seconds of silence (trigger disarmed) once the guide has faded out. 0 = no pulse.")]
    [SerializeField, Min(0f)] float _guideOffDuration = 0f;

    public event Action<StoryScene> Triggered;
    public event Action<StoryScene> Finished;

    // Set by the chapter: asked before this scene triggers; false = another scene is under way, so the entry is ignored.
    public Func<StoryScene, bool> CanTrigger;

    public bool Done { get; private set; }

    // For the Debugger: which sound leads to this scene, and whether its Timeline is running now.
    // Trigger and Guide are also moved by the chapter to keep them clear of the visitor.
    public SceneTrigger Trigger => _trigger;
    public AudioSource Guide   => _guide;
    public bool        Playing => _playing;

    bool _playing;
    bool _triggered;
    GuideSoundFader _guideFader;
    Coroutine _pulse;

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
        StopPulse();
        if (_playing) { _playing = false; _timeline.Stop(); }
        _triggered = false;
        if (_guideFader != null) _guideFader.StopImmediate();
    }

    // Called by the chapter when it begins, so the scene can play again.
    public void ResetProgress() { Done = false; _triggered = false; }

    void Wait()
    {
        StopPulse();
        if (_guideFader != null && _guideOnDuration > 0f && _guideOffDuration > 0f)
        {
            _pulse = StartCoroutine(Pulse());
            return;
        }
        _trigger.gameObject.SetActive(true);
        if (_guideFader != null) _guideFader.FadeIn();
    }

    // Scaled time, so the story's fast-forward shortens it too.
    IEnumerator Pulse()
    {
        while (true)
        {
            _trigger.gameObject.SetActive(true);
            _guideFader.FadeIn();
            yield return new WaitForSeconds(_guideOnDuration);

            _trigger.gameObject.SetActive(false);
            bool silent = false;
            _guideFader.FadeOut(false, () => silent = true);
            yield return new WaitUntil(() => silent);
            yield return new WaitForSeconds(_guideOffDuration);
        }
    }

    void StopPulse()
    {
        if (_pulse != null) StopCoroutine(_pulse);
        _pulse = null;
    }

    public void FadeOutGuide(bool wasTriggered, Action onComplete = null)
    {
        StopPulse();
        _trigger.gameObject.SetActive(false);
        if (_guideFader != null) _guideFader.FadeOut(wasTriggered, onComplete);
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

    // Development shortcut: ends a triggered scene as if its Timeline had run to its end, starting it first if the
    // other guides are still fading.
    public void Skip()
    {
        if (!_triggered || Done || !isActiveAndEnabled) return;
        _playing = true;
        StoryPart.JumpToEnd(_timeline);
    }

    void OnEntered()
    {
        if (_triggered || Done) return;
        if (CanTrigger != null && !CanTrigger(this))
        {
            Debug.Log($"[Story] {name} entered, but another scene is under way; ignored.");
            return;
        }
        _triggered = true;
        StopPulse();
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
