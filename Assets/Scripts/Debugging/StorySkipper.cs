using UnityEngine;
using UnityEngine.InputSystem;

// Development / staff shortcut: Shift + S skips the story part playing now (Editor and builds).
//   Onboarding / Ending playing        → its Timeline jumps to the end, the next part begins.
//   A chapter with a scene playing     → that scene ends, the chapter carries on as usual.
//   A chapter waiting (nothing playing) → the whole chapter ends, the next part begins.
// Shift + S rather than S, because the Meta XR Simulator walks backwards with S.
// The key is defined here, not in DebugActions.inputactions, so the Debugger prefab needs no action reference.
public class StorySkipper : MonoBehaviour
{
    [Tooltip("Found in the scene when left empty (the Debugger is a prefab and cannot reference a scene object).")]
    [SerializeField] StoryDirector _director;

    InputAction _skip;

    void Awake()
    {
        _skip = new InputAction("SkipStoryPart", InputActionType.Button);
        _skip.AddCompositeBinding("OneModifier")
             .With("Modifier", "<Keyboard>/shift")
             .With("Binding", "<Keyboard>/s");
    }

    void OnEnable()
    {
        _skip.performed += OnSkip;
        _skip.Enable();
    }

    void OnDisable()
    {
        _skip.performed -= OnSkip;
        _skip.Disable();
    }

    void OnDestroy() => _skip.Dispose();

    void OnSkip(InputAction.CallbackContext _)
    {
        if (_director == null) _director = FindFirstObjectByType<StoryDirector>();
        if (_director == null)
        {
            Debug.LogWarning("[StorySkipper] No Story Director in the scene.", this);
            return;
        }
        _director.SkipCurrent();
    }
}
