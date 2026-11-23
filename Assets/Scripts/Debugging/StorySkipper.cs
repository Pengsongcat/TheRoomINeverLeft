using UnityEngine;
using UnityEngine.InputSystem;

// Development / staff shortcut: Shift + S, or Y on the left controller, skips the story part playing now (Editor and
// builds).
//   Onboarding / Ending playing        → its Timeline jumps to the end, the next part begins.
//   A chapter with a scene playing     → that scene ends, the chapter carries on as usual.
//   A chapter waiting (nothing playing) → the whole chapter ends, the next part begins.
// Shift + 1 … 9 (keyboard only) triggers that scene of the chapter playing now, as if the visitor had walked into it;
// the chapter logs which number is which scene when it begins. Ignored outside a chapter or while a scene plays.
// Shift + S rather than S, because the Meta XR Simulator walks backwards with S.
// The keys are defined here, not in DebugActions.inputactions, so the Debugger prefab needs no action reference.
public class StorySkipper : MonoBehaviour
{
    [Tooltip("Found in the scene when left empty (the Debugger is a prefab and cannot reference a scene object).")]
    [SerializeField] StoryDirector _director;

    InputAction _skip;
    readonly InputAction[] _triggerScene = new InputAction[9];
    readonly System.Action<InputAction.CallbackContext>[] _onTriggerScene = new System.Action<InputAction.CallbackContext>[9];

    void Awake()
    {
        _skip = new InputAction("SkipStoryPart", InputActionType.Button);
        _skip.AddCompositeBinding("OneModifier")
             .With("Modifier", "<Keyboard>/shift")
             .With("Binding", "<Keyboard>/s");
        _skip.AddBinding("<XRController>{LeftHand}/secondaryButton"); // Y

        for (int i = 0; i < _triggerScene.Length; i++)
        {
            int index = i;
            _triggerScene[i] = new InputAction($"TriggerScene{i + 1}", InputActionType.Button);
            _triggerScene[i].AddCompositeBinding("OneModifier")
                            .With("Modifier", "<Keyboard>/shift")
                            .With("Binding", $"<Keyboard>/{i + 1}");
            _onTriggerScene[i] = _ => OnTriggerScene(index);
        }
    }

    void OnEnable()
    {
        _skip.performed += OnSkip;
        _skip.Enable();
        for (int i = 0; i < _triggerScene.Length; i++)
        {
            _triggerScene[i].performed += _onTriggerScene[i];
            _triggerScene[i].Enable();
        }
    }

    void OnDisable()
    {
        _skip.performed -= OnSkip;
        _skip.Disable();
        for (int i = 0; i < _triggerScene.Length; i++)
        {
            _triggerScene[i].performed -= _onTriggerScene[i];
            _triggerScene[i].Disable();
        }
    }

    void OnDestroy()
    {
        _skip.Dispose();
        foreach (var action in _triggerScene) action.Dispose();
    }

    bool FindDirector()
    {
        if (_director == null) _director = FindFirstObjectByType<StoryDirector>();
        if (_director != null) return true;
        Debug.LogWarning("[StorySkipper] No Story Director in the scene.", this);
        return false;
    }

    void OnTriggerScene(int index)
    {
        if (!FindDirector()) return;
        if (_director.Current is StoryChapter chapter) chapter.TriggerScene(index);
        else Debug.Log($"[StorySkipper] Shift+{index + 1}: no chapter is playing.");
    }

    void OnSkip(InputAction.CallbackContext _)
    {
        if (!FindDirector()) return;
        _director.SkipCurrent();
    }
}
