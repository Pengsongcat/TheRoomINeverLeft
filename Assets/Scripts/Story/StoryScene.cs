using UnityEngine;

// A scene made of chapters the visitor can play in any order. All chapters wait (guide sounds loop) until one is
// triggered; the others are turned off while it plays. When it finishes it turns off, and the chapters that have not
// played yet turn back on. The scene is finished once every chapter has played.
public class StoryScene : StoryPart
{
    Chapter[] _chapters;

    void Awake()
    {
        _chapters = GetComponentsInChildren<Chapter>(includeInactive: true);
        foreach (var chapter in _chapters)
        {
            chapter.Triggered += OnChapterTriggered;
            chapter.Finished  += OnChapterFinished;
        }
    }

    public override void Begin()
    {
        if (_chapters.Length == 0)
        {
            Debug.LogWarning($"[Story] {name} has no chapters.");
            RaiseFinished();
            return;
        }

        foreach (var chapter in _chapters)
        {
            chapter.ResetProgress();
            chapter.gameObject.SetActive(true);
        }
    }

    void OnChapterTriggered(Chapter triggered)
    {
        foreach (var chapter in _chapters)
            if (chapter != triggered) chapter.gameObject.SetActive(false);
    }

    void OnChapterFinished(Chapter finished)
    {
        finished.gameObject.SetActive(false);

        bool allDone = true;
        foreach (var chapter in _chapters)
        {
            if (chapter.Done) continue;
            allDone = false;
            chapter.gameObject.SetActive(true);
        }

        if (allDone) RaiseFinished();
    }
}
