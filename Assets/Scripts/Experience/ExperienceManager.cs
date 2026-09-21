using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Owns the top-level flow and passthrough. Starts in VR (passthrough off), with every story part off, and asks
// staff which experience to run:
//   VR (B) → VR experience: Onboarding on.
//   MR (A) → passthrough on, VR content hidden → Room Calibrator runs → Completed
//          → MR experience: Onboarding on, the VR world masked to the portal's opening
//          → the visitor walks through the portal → VR experience: the VR world is all around, the real room
//            shows only through the doorway behind → the portal dissolves → passthrough off.
public class ExperienceManager : MonoBehaviour
{
    public enum Mode  { None, VR, MR }
    public enum State { Choosing, Calibrating, MRExperience, VRExperience }

    [Header("Components")]
    [SerializeField] CalibrationHud  _hud;
    [SerializeField] PassthroughMask _mask;
    [Tooltip("Set on the scene instance.")]
    [SerializeField] RoomCalibrator _calibrator;
    [Tooltip("Set on the scene instance.")]
    [SerializeField] MRPortal _portal;

    [Header("Story")]
    [Tooltip("Every story part (Onboarding, scenes, Ending). All are turned off at start. Set on the scene instance.")]
    [SerializeField] GameObject[] _storyParts;
    [Tooltip("Turned on when the experience starts. Set on the scene instance.")]
    [SerializeField] GameObject _onboarding;

    [Header("Input")]
    [SerializeField] InputActionReference _chooseMRAction; // A
    [SerializeField] InputActionReference _chooseVRAction; // B

    [Header("Passthrough")]
    [Tooltip("VR content hidden while calibrating in passthrough (e.g. the Plane). Set on the scene instance.")]
    [SerializeField] GameObject[] _hideInPassthrough;

    public event Action<State> StateChanged;

    public Mode  CurrentMode  { get; private set; }
    public State CurrentState { get; private set; }

    void OnEnable()
    {
        _chooseMRAction.action.Enable();
        _chooseVRAction.action.Enable();
        _chooseMRAction.action.performed += OnChooseMR;
        _chooseVRAction.action.performed += OnChooseVR;
        _calibrator.Completed += OnCalibrationCompleted;
        _portal.Entered += FinishMRExperience;
        _portal.Closed  += OnPortalClosed;
    }

    void OnDisable()
    {
        _chooseMRAction.action.performed -= OnChooseMR;
        _chooseVRAction.action.performed -= OnChooseVR;
        _calibrator.Completed -= OnCalibrationCompleted;
        _portal.Entered -= FinishMRExperience;
        _portal.Closed  -= OnPortalClosed;
    }

    void Start()
    {
        foreach (var part in _storyParts)
            if (part != null) part.SetActive(false);
        _portal.gameObject.SetActive(false);
        _mask.Hide();

        SetPassthrough(false);
        SetState(State.Choosing);
        _hud.SetVisible(true);
        _hud.SetMessage("VR or MR experience?\n<size=75%>A = MR · B = VR</size>");
    }

    // ─── Choice ────────────────────────────────────────────────────────────────

    void OnChooseMR(InputAction.CallbackContext context)
    {
        if (CurrentState != State.Choosing) return;
        CurrentMode = Mode.MR;
        _hud.SetVisible(false);
        Debug.Log("[ExperienceManager] MR chosen.");

        SetPassthrough(true);
        SetVRContentHidden(true);
        SetState(State.Calibrating);
        _calibrator.Begin();
    }

    void OnChooseVR(InputAction.CallbackContext context)
    {
        if (CurrentState != State.Choosing) return;
        CurrentMode = Mode.VR;
        _hud.SetVisible(false);
        Debug.Log("[ExperienceManager] VR chosen.");

        StartVRExperience();
    }

    void OnCalibrationCompleted(RoomRect rect)
    {
        if (CurrentState != State.Calibrating) return;
        StartMRExperience();
    }

    // ─── Experience steps ──────────────────────────────────────────────────────

    // Passthrough stays on; the VR world is shown again but masked, so it is only visible through the portal.
    void StartMRExperience()
    {
        SetState(State.MRExperience);
        SetVRContentHidden(false);
        _mask.ShowMask();
        SetOnboarding(true);
        _portal.gameObject.SetActive(true);
        Debug.Log("[ExperienceManager] MR experience: portal open.");
    }

    // Called when the visitor walks through the portal (the portal then dissolves itself and raises Closed).
    public void FinishMRExperience()
    {
        if (CurrentState != State.MRExperience) return;
        StartVRExperience();
    }

    // VR experience logic goes here.
    void StartVRExperience()
    {
        SetState(State.VRExperience);
        SetOnboarding(true);

        // MR path: passthrough stays on only through the doorway behind the visitor, until the portal closes.
        if (CurrentMode == Mode.MR) _mask.ShowFill();
        else SetPassthrough(false);

        Debug.Log("[ExperienceManager] VR experience.");
    }

    void OnPortalClosed()
    {
        _mask.Hide();
        SetPassthrough(false);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    void SetPassthrough(bool on)
    {
        var passthrough = XRPlatformServices.Instance.PassthroughService;
        if (on) passthrough.Enable();
        else    passthrough.Disable();
    }

    void SetVRContentHidden(bool hidden)
    {
        foreach (var go in _hideInPassthrough)
            if (go != null) go.SetActive(!hidden);
    }

    void SetOnboarding(bool on)
    {
        if (_onboarding != null) _onboarding.SetActive(on);
    }

    void SetState(State state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }
}
