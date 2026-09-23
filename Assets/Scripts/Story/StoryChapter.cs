using UnityEngine;

// A chapter made of scenes the visitor can play in any order. All scenes wait (guide sounds loop) until one is
// triggered; all guide sounds fade out and other triggers disarm. The triggered guide fades twice as long,
// while its Timeline begins when the other guides finish fading. Unplayed scenes then resume waiting.
public class StoryChapter : StoryPart
{
    StoryScene[] _scenes;
    StoryScene _activeScene;
    int _pendingOtherGuides;
    bool _activeTimelineStarted;
    bool _activeTimelineFinished;
    bool _activeGuideFaded;

    void Awake()
    {
        _scenes = GetComponentsInChildren<StoryScene>(includeInactive: true);
        foreach (var scene in _scenes)
        {
            scene.Triggered += OnSceneTriggered;
            scene.Finished  += OnSceneFinished;
        }
    }

    public override void Begin()
    {
        _activeScene = null;
        if (_scenes.Length == 0)
        {
            Debug.LogWarning($"[Story] {name} has no scenes.");
            RaiseFinished();
            return;
        }

        foreach (var scene in _scenes)
        {
            bool alreadyActive = scene.gameObject.activeSelf;
            scene.ResetProgress();
            scene.gameObject.SetActive(true);
            if (alreadyActive) scene.ResumeWaiting();
        }
    }

    // A scene playing → skip that scene (the chapter then carries on as usual); otherwise skip the whole chapter.
    public override void Skip()
    {
        if (_activeScene != null && !_activeScene.Done) _activeScene.Skip();
        else RaiseFinished();
    }

    void OnSceneTriggered(StoryScene triggered)
    {
        _activeScene = triggered;
        _activeTimelineStarted = false;
        _activeTimelineFinished = false;
        _activeGuideFaded = false;
        _pendingOtherGuides = 0;

        foreach (var scene in _scenes)
            if (scene != triggered && !scene.Done && scene.gameObject.activeInHierarchy)
                _pendingOtherGuides++;

        triggered.FadeOutGuide(2f, () =>
        {
            if (_activeScene != triggered) return;
            _activeGuideFaded = true;
            if (_activeTimelineFinished) CompleteScene(triggered);
        });

        foreach (var scene in _scenes)
        {
            if (scene == triggered || scene.Done || !scene.gameObject.activeInHierarchy) continue;
            scene.FadeOutGuide(1f, () =>
            {
                if (_activeScene != triggered) return;
                scene.gameObject.SetActive(false);
                _pendingOtherGuides--;
                TryStartTimeline(triggered);
            });
        }
        TryStartTimeline(triggered);
    }

    void TryStartTimeline(StoryScene triggered)
    {
        if (_activeScene != triggered || _activeTimelineStarted || _pendingOtherGuides > 0) return;
        _activeTimelineStarted = true;
        triggered.StartTimeline();
    }

    void OnSceneFinished(StoryScene finished)
    {
        if (_activeScene != finished) return;
        _activeTimelineFinished = true;
        if (_activeGuideFaded) CompleteScene(finished);
    }

    void CompleteScene(StoryScene finished)
    {
        _activeScene = null;
        finished.gameObject.SetActive(false);

        bool allDone = true;
        foreach (var scene in _scenes)
        {
            if (scene.Done) continue;
            allDone = false;
            if (scene.gameObject.activeSelf) scene.ResumeWaiting();
            else scene.gameObject.SetActive(true);
        }

        if (allDone) RaiseFinished();
    }
}
