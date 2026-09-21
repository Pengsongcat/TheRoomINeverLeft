using UnityEngine;
using UnityEngine.InputSystem;

// Root of the Debugger: shows and hides every DebugVisualizer under it together.
// Hidden at startup so visitors never see debug content; staff toggle with left X (keyboard V in the Editor).
public class DebugManager : MonoBehaviour
{
    [SerializeField] InputActionReference _toggleAction;
    [SerializeField] bool _visibleAtStart;

    DebugVisualizer[] _visualizers;

    public bool VisualizersVisible { get; private set; }

    void Awake()
    {
        _visualizers = GetComponentsInChildren<DebugVisualizer>(includeInactive: true);
    }

    void OnEnable()
    {
        if (_toggleAction == null) return;
        _toggleAction.action.Enable();
        _toggleAction.action.performed += OnToggleInput;
    }

    void OnDisable()
    {
        if (_toggleAction != null) _toggleAction.action.performed -= OnToggleInput;
    }

    // Start, not Awake: the visualizers build their geometry in their own Awake.
    void Start() => SetVisualizersVisible(_visibleAtStart);

    public void SetVisualizersVisible(bool visible)
    {
        VisualizersVisible = visible;
        foreach (var v in _visualizers)
            if (v != null) v.SetVisible(visible);
    }

    void OnToggleInput(InputAction.CallbackContext _) => SetVisualizersVisible(!VisualizersVisible);
}
