using UnityEngine;

// Put on TRINL_Door. Starts the game from the door's own walk-through and dissolve: the Experience Manager begins
// the story (passthrough stays on), either on the Door Exit Dissolve's start trigger (its Start Point after walking
// through the door, or without one the door itself; the passthrough fade and dissolve start then too) or once the
// door has fully dissolved.
public class DoorGameStart : MonoBehaviour
{
    public enum StartOn { Trigger, Dissolved }

    [Tooltip("Trigger: on the Door Exit Dissolve's start trigger (Start Point, or the door when it has none), " +
             "while the passthrough fade and dissolve play. Dissolved: once the door has fully dissolved. " +
             "Passthrough stays on either way.")]
    [SerializeField] StartOn _startOn = StartOn.Trigger;
    [Tooltip("Found under this object if left empty.")]
    [SerializeField] DoorExitDissolve _dissolve;
    [Tooltip("Found in the scene if left empty.")]
    [SerializeField] ExperienceManager _experience;
    [Tooltip("Turned off once the door has dissolved, so its portal room stops drawing. Leave empty to keep it.")]
    [SerializeField] GameObject _hideWhenDone;

    void Awake()
    {
        if (_dissolve == null) _dissolve = GetComponentInChildren<DoorExitDissolve>(includeInactive: true);
        if (_experience == null) _experience = FindFirstObjectByType<ExperienceManager>(FindObjectsInactive.Include);
        if (_dissolve == null) Debug.LogWarning($"[DoorGameStart] No Door Exit Dissolve under {name}.", this);
        if (_experience == null) Debug.LogWarning("[DoorGameStart] No Experience Manager in the scene.", this);
    }

    void OnEnable()
    {
        if (_dissolve == null) return;
        _dissolve.Started   += OnStarted;
        _dissolve.Completed += OnDissolved;
    }

    void OnDisable()
    {
        if (_dissolve == null) return;
        _dissolve.Started   -= OnStarted;
        _dissolve.Completed -= OnDissolved;
    }

    void OnStarted()
    {
        if (_startOn == StartOn.Trigger) StartGame("Start trigger reached");
    }

    void OnDissolved()
    {
        if (_startOn == StartOn.Dissolved) StartGame("Door dissolved");
        // Only now: turning the door off earlier would stop its dissolve.
        if (_hideWhenDone != null) _hideWhenDone.SetActive(false);
    }

    void StartGame(string reason)
    {
        Debug.Log($"[DoorGameStart] {reason}; starting the game.", this);
        if (_experience != null) _experience.CompleteMRFromDoor();
    }
}
