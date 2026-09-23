using System;
using UnityEngine;

// Owns the order of the story: Onboarding → Scene1 → … → Ending. Every part is turned off at start. Begin turns the
// first part on and begins it; each time the current part finishes, it is turned off and the next one begins.
public class StoryDirector : MonoBehaviour
{
    [Tooltip("In order. Set on the scene instance.")]
    [SerializeField] StoryPart[] _parts;

    public event Action Completed;

    int _current = -1;

    void Awake()
    {
        foreach (var part in _parts)
        {
            if (part == null) continue;
            part.Finished += OnPartFinished;
            part.gameObject.SetActive(false);
        }
    }

    // Turns the first part on without beginning it (MR: Onboarding is visible through the portal).
    public void ShowFirst()
    {
        if (_parts.Length > 0) _parts[0].gameObject.SetActive(true);
    }

    public void Begin()
    {
        if (_current >= 0) return;
        Enter(0);
    }

    // Development shortcut: skips what is playing in the current part (see each part's Skip).
    public void SkipCurrent()
    {
        if (_current < 0 || _current >= _parts.Length)
        {
            Debug.Log("[Story] Nothing to skip: the story is not running.");
            return;
        }
        Debug.Log($"[Story] Skip requested in {_parts[_current].name}.");
        _parts[_current].Skip();
    }

    void Enter(int index)
    {
        _current = index;
        var part = _parts[index];
        Debug.Log($"[Story] {part.name} begins.");
        part.gameObject.SetActive(true);
        part.Begin();
    }

    void OnPartFinished(StoryPart part)
    {
        if (_current < 0 || _current >= _parts.Length || part != _parts[_current]) return;
        part.gameObject.SetActive(false);

        if (_current + 1 < _parts.Length)
        {
            Enter(_current + 1);
        }
        else
        {
            _current = _parts.Length;
            Debug.Log("[Story] Completed.");
            Completed?.Invoke();
        }
    }
}
