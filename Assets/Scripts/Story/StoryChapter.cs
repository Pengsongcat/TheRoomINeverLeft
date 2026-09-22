using UnityEngine;

// A chapter made of scenes the visitor can play in any order. All scenes wait (guide sounds loop) until one is
// triggered; the others are turned off while it plays. When it finishes it turns off, and the scenes that have not
// played yet turn back on. The chapter is finished once every scene has played.
public class StoryChapter : StoryPart
{
    StoryScene[] _scenes;

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
        if (_scenes.Length == 0)
        {
            Debug.LogWarning($"[Story] {name} has no scenes.");
            RaiseFinished();
            return;
        }

        foreach (var scene in _scenes)
        {
            scene.ResetProgress();
            scene.gameObject.SetActive(true);
        }
    }

    void OnSceneTriggered(StoryScene triggered)
    {
        foreach (var scene in _scenes)
            if (scene != triggered) scene.gameObject.SetActive(false);
    }

    void OnSceneFinished(StoryScene finished)
    {
        finished.gameObject.SetActive(false);

        bool allDone = true;
        foreach (var scene in _scenes)
        {
            if (scene.Done) continue;
            allDone = false;
            scene.gameObject.SetActive(true);
        }

        if (allDone) RaiseFinished();
    }
}
