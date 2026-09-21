using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Owns the top-level flow and passthrough. Starts in VR (passthrough off) and asks staff which experience to run:
//   VR (B) → VR experience.
//   MR (A) → passthrough on, VR content hidden → Room Calibrator runs → Completed
//          → MR experience → VR experience (passthrough off, VR content shown).
// The MR and VR experience steps have no logic yet; the MR step stays in MR until FinishMRExperience() is called.
public class ExperienceManager : MonoBehaviour
{
    public enum Mode  { None, VR, MR }
    public enum State { Choosing, Calibrating, MRExperience, VRExperience }

    [Header("Components")]
    [SerializeField] CalibrationHud _hud;
    [Tooltip("Set on the scene instance.")]
    [SerializeField] RoomCalibrator _calibrator;

    [Header("Input")]
    [SerializeField] InputActionReference _chooseMRAction; // A
    [SerializeField] InputActionReference _chooseVRAction; // B

    [Tooltip("VR content hidden while passthrough shows the real room (e.g. the Plane). Set on the scene instance.")]
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
    }

    void OnDisable()
    {
        _chooseMRAction.action.performed -= OnChooseMR;
        _chooseVRAction.action.performed -= OnChooseVR;
        _calibrator.Completed -= OnCalibrationCompleted;
    }

    void Start()
    {
        SetPassthrough(false);
        SetState(State.Choosing);
        _hud.SetVisible(true);
        _hud.SetMessage("VR or MR experience?\n<size=75%>A = MR · B = VR</size>");
    }

    // ─── Choice ────────────────────────────────────────────────────────────────

    void OnChooseMR(InputAction.CallbackContext _)
    {
        if (CurrentState != State.Choosing) return;
        CurrentMode = Mode.MR;
        _hud.SetVisible(false);
        Debug.Log("[ExperienceManager] MR chosen.");

        SetPassthrough(true);
        SetState(State.Calibrating);
        _calibrator.Begin();
    }

    void OnChooseVR(InputAction.CallbackContext _)
    {
        if (CurrentState != State.Choosing) return;
        CurrentMode = Mode.VR;
        _hud.SetVisible(false);
        Debug.Log("[ExperienceManager] VR chosen.");

        StartVRExperience();
    }

    void OnCalibrationCompleted(RoomRect _)
    {
        if (CurrentState != State.Calibrating) return;
        StartMRExperience();
    }

    // ─── Experience steps ──────────────────────────────────────────────────────

    // MR experience logic goes here. It calls FinishMRExperience() when it is done; until that logic exists
    // the flow stays here, in passthrough.
    void StartMRExperience()
    {
        SetState(State.MRExperience);
        Debug.Log("[ExperienceManager] MR experience (no logic yet; staying in MR).");
    }

    public void FinishMRExperience()
    {
        if (CurrentState != State.MRExperience) return;
        StartVRExperience();
    }

    // VR experience logic goes here.
    void StartVRExperience()
    {
        SetPassthrough(false);
        SetState(State.VRExperience);
        Debug.Log("[ExperienceManager] VR experience (no logic yet).");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    void SetPassthrough(bool on)
    {
        var passthrough = XRPlatformServices.Instance.PassthroughService;
        if (on) passthrough.Enable();
        else    passthrough.Disable();

        foreach (var go in _hideInPassthrough)
            if (go != null) go.SetActive(!on);
    }

    void SetState(State state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }
}
