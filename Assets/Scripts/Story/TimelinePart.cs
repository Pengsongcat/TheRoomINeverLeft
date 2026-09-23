using UnityEngine;
using UnityEngine.Playables;

// A story part that is one Timeline (Onboarding, Ending). Begin plays it; it is finished when the Timeline ends.
// The Timeline drives the visuals and sound sources under the part.
public class TimelinePart : StoryPart
{
    [Tooltip("Wrap Mode must be None, so the director stops at the end.")]
    [SerializeField] PlayableDirector _timeline;

    bool _playing;

    void OnEnable() => _timeline.stopped += OnStopped;

    void OnDisable()
    {
        _timeline.stopped -= OnStopped;
        if (_playing) { _playing = false; _timeline.Stop(); }
    }

    public override void Begin()
    {
        _playing = true;
        _timeline.time = 0;
        _timeline.Play();
    }

    public override void Skip()
    {
        if (_playing) JumpToEnd(_timeline);
    }

    void OnStopped(PlayableDirector director)
    {
        // Also raised when the director is disabled with the part; only a Timeline that ran to its end counts.
        if (!_playing || !isActiveAndEnabled) return;
        _playing = false;
        RaiseFinished();
    }
}
