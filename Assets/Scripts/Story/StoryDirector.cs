using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Owns the order of the story: Onboarding → Scene1 → … → Ending. Every part is turned off at start. Begin turns the
// first part on and begins it; each time the current part finishes, it is turned off and the next one begins.
public class StoryDirector : MonoBehaviour
{
    // Kept only to migrate existing scene instances to the combined list below.
    [SerializeField, HideInInspector] StoryPart[] _parts;

    [Serializable]
    struct PartTiming
    {
        [HideInInspector] public string name;
        public StoryPart part;
        [Tooltip("Seconds to wait after this part finishes before the next part begins.")]
        [Min(0f)] public float endDelay;
    }

    [InspectorName("Parts")]
    [Tooltip("In story order. End Delay is the pause after that Chapter or Timeline finishes.")]
    [SerializeField] List<PartTiming> _partTimings = new();

    public event Action Completed;

    int _current = -1;
    Coroutine _advance;

    // True from Begin until the last part has finished.
    public bool Running => _current >= 0 && _current < _partTimings.Count;

    void Awake()
    {
        EnsurePartsMigrated();
        foreach (var timing in _partTimings)
        {
            var part = timing.part;
            if (part == null) continue;
            part.Finished += OnPartFinished;
            part.gameObject.SetActive(false);
        }
    }

    void OnValidate()
    {
        EnsurePartsMigrated();
        for (int i = 0; i < _partTimings.Count; i++)
        {
            var timing = _partTimings[i];
            string partName = timing.part != null ? timing.part.name : "Missing Part";
            if (timing.name == partName) continue;
            timing.name = partName;
            _partTimings[i] = timing;
        }
    }

    void EnsurePartsMigrated()
    {
        _partTimings ??= new List<PartTiming>();
        if (_partTimings.Count == 0 && _parts != null)
        {
            foreach (var part in _parts)
            {
                _partTimings.Add(new PartTiming
                {
                    name = part != null ? part.name : "Missing Part",
                    part = part,
                    endDelay = 0f,
                });
            }
        }
        _parts = Array.Empty<StoryPart>();
    }

    // Turns the first part on without beginning it (MR: Onboarding is visible through the portal).
    public void ShowFirst()
    {
        if (_partTimings.Count > 0 && _partTimings[0].part != null)
            _partTimings[0].part.gameObject.SetActive(true);
    }

    public void Begin()
    {
        if (_current >= 0) return;
        Enter(0);
    }

    // Development shortcut: skips what is playing in the current part (see each part's Skip).
    public void SkipCurrent()
    {
        if (!Running)
        {
            Debug.Log("[Story] Nothing to skip: the story is not running.");
            return;
        }
        if (_advance != null)
        {
            Debug.Log("[Story] The current part has finished and is waiting to advance.");
            return;
        }
        var part = _partTimings[_current].part;
        if (part == null) return;
        Debug.Log($"[Story] Skip requested in {part.name}.");
        part.Skip();
    }

    void Enter(int index)
    {
        _current = index;
        var part = _partTimings[index].part;
        if (part == null)
        {
            Debug.LogError($"[Story] Part {index} is missing.", this);
            return;
        }
        Debug.Log($"[Story] {part.name} begins.");
        part.gameObject.SetActive(true);
        part.Begin();
    }

    void OnPartFinished(StoryPart part)
    {
        if (_current < 0 || _current >= _partTimings.Count || part != _partTimings[_current].part || _advance != null)
            return;
        float delay = Mathf.Max(0f, _partTimings[_current].endDelay);
        if (delay > 0f)
        {
            _advance = StartCoroutine(AdvanceAfterDelay(part, delay));
            return;
        }
        Advance(part);
    }

    IEnumerator AdvanceAfterDelay(StoryPart part, float delay)
    {
        yield return new WaitForSeconds(delay);
        _advance = null;
        Advance(part);
    }

    void Advance(StoryPart part)
    {
        if (_current < 0 || _current >= _partTimings.Count || part != _partTimings[_current].part) return;
        part.gameObject.SetActive(false);

        if (_current + 1 < _partTimings.Count)
        {
            Enter(_current + 1);
        }
        else
        {
            _current = _partTimings.Count;
            Debug.Log("[Story] Completed.");
            Completed?.Invoke();
        }
    }
}
