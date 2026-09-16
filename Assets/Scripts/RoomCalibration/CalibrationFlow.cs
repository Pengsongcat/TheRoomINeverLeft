using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// Top-level calibration state machine.
// States: Loading → Calibrating ↔ Confirming → Ready
public class CalibrationFlow : MonoBehaviour
{
    public enum State { Loading, Calibrating, Confirming, Ready }

    [Header("Components")]
    [SerializeField] RoomCornerCalibrator _calibrator;
    [SerializeField] RoomDebugView        _debugView;

    [Header("Input")]
    [SerializeField] InputActionReference _confirmAction;     // A button
    [SerializeField] InputActionReference _recalibrateAction; // B button with Hold interaction

    public event Action<RoomRect> OnCalibrationAccepted;

    public State CurrentState { get; private set; }

    RoomRect   _pendingRect;
    Vector3[]  _rawCorners; // placed anchor positions, for debug view

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start() => _ = StartLoadingAsync();

    void OnEnable()
    {
        _confirmAction.action.Enable();
        _recalibrateAction.action.Enable();
        _confirmAction.action.performed     += OnConfirmInput;
        _recalibrateAction.action.performed += OnRecalibrateInput;

        _calibrator.OnCornersAccepted  += HandleCornersAccepted;
        _calibrator.OnShapeCheckFailed += HandleShapeCheckFailed;
    }

    void OnDisable()
    {
        _confirmAction.action.performed     -= OnConfirmInput;
        _recalibrateAction.action.performed -= OnRecalibrateInput;

        _calibrator.OnCornersAccepted  -= HandleCornersAccepted;
        _calibrator.OnShapeCheckFailed -= HandleShapeCheckFailed;
    }

    // ─── Load on startup ───────────────────────────────────────────────────────

    async Task StartLoadingAsync()
    {
        SetState(State.Loading);
        Log("Loading anchors…");

        XRPlatformServices.Instance.PassthroughService.Enable();

        var ids = RoomAnchorStore.Load();
        if (ids.Length >= 3)
        {
            var handles = await XRPlatformServices.Instance.AnchorService.LoadAnchorsAsync(ids);
            if (handles.Count == 4)
            {
                GoToConfirming(HandlesToPositions(handles), handles);
                return;
            }
            if (handles.Count == 3)
            {
                var four = TryRebuildFourthCorner(handles, ids);
                if (four != null)
                {
                    GoToConfirming(HandlesToPositions(four), four);
                    return;
                }
            }
        }

        GoToCalibrating();
    }

    // ─── State transitions ─────────────────────────────────────────────────────

    void GoToCalibrating()
    {
        SetState(State.Calibrating);
        Log($"Place corner {_calibrator.CornerCount + 1}/4 (trigger). B = undo.");
        _calibrator.Activate();
        _debugView.Hide();
    }

    void GoToConfirming(Vector3[] rawCorners, IReadOnlyList<IAnchorHandle> handles)
    {
        _rawCorners  = rawCorners;
        _pendingRect = new RoomRect(rawCorners[0], rawCorners[1], rawCorners[2], rawCorners[3]);
        SetState(State.Confirming);

        var fit = RoomFit.Compute(_pendingRect, new LabReference());
        Log($"Room {_pendingRect.width:F1} × {_pendingRect.length:F1} m  scale {fit.sx:F2}x. A = accept, hold B = redo.");
        _debugView.Show(_pendingRect, _rawCorners);
    }

    void GoToReady()
    {
        SetState(State.Ready);
        _debugView.Hide();
        Log("Ready. Staff presses Start.");
    }

    // ─── Input ─────────────────────────────────────────────────────────────────

    void OnConfirmInput(InputAction.CallbackContext _)
    {
        if (CurrentState != State.Confirming) return;
        GoToReady();
        OnCalibrationAccepted?.Invoke(_pendingRect);
    }

    void OnRecalibrateInput(InputAction.CallbackContext _)
    {
        if (CurrentState != State.Confirming) return;
        _calibrator.ClearAnchors();
        RoomAnchorStore.Clear();
        GoToCalibrating();
    }

    // ─── Calibrator callbacks ──────────────────────────────────────────────────

    async void HandleCornersAccepted(RoomRect rect)
    {
        _calibrator.Deactivate();
        Log("Saving anchors…");

        var anchors = _calibrator.GetAnchors();
        bool saved  = await XRPlatformServices.Instance.AnchorService.SaveAnchorsAsync(anchors);
        if (!saved)
        {
            Log("Anchor save failed — restarting calibration.");
            GoToCalibrating();
            return;
        }

        var ids = new Guid[anchors.Length];
        for (int i = 0; i < anchors.Length; i++) ids[i] = anchors[i].Uuid;
        RoomAnchorStore.Save(ids);

        GoToConfirming(_calibrator.GetPositions(), anchors);
    }

    void HandleShapeCheckFailed(string reason)
    {
        Log($"Shape invalid: {reason}  B = undo.");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    static Vector3[] HandlesToPositions(IReadOnlyList<IAnchorHandle> handles)
    {
        var pos = new Vector3[handles.Count];
        for (int i = 0; i < handles.Count; i++) pos[i] = handles[i].Position;
        return pos;
    }

    // Rebuild the missing 4th anchor using the parallelogram property.
    // Returns a 4-element array in saved ID order, or null if reconstruction fails.
    static IReadOnlyList<IAnchorHandle> TryRebuildFourthCorner(
        IReadOnlyList<IAnchorHandle> three, Guid[] savedIds)
    {
        if (savedIds.Length < 4) return null;

        var dict = new Dictionary<Guid, IAnchorHandle>();
        foreach (var h in three) dict[h.Uuid] = h;

        var ordered    = new IAnchorHandle[4];
        int missingIdx = -1;
        for (int i = 0; i < 4; i++)
        {
            if (dict.TryGetValue(savedIds[i], out ordered[i])) continue;
            if (missingIdx >= 0) return null; // more than one missing
            missingIdx = i;
        }
        if (missingIdx < 0) return null;

        var knownPos = new Vector3[4];
        for (int i = 0; i < 4; i++) if (ordered[i] != null) knownPos[i] = ordered[i].Position;
        var rebuilt = RoomRect.RebuildCorner(missingIdx, knownPos);

        ordered[missingIdx] = new RebuiltAnchorHandle(savedIds[missingIdx], rebuilt);
        return ordered;
    }

    void SetState(State s) => CurrentState = s;
    void Log(string msg)   => Debug.Log($"[CalibrationFlow] {msg}");

    // Stand-in for an anchor position that was geometrically reconstructed, not loaded from OVR.
    sealed class RebuiltAnchorHandle : IAnchorHandle
    {
        public Guid Uuid           { get; }
        public Vector3 Position    { get; }
        public Quaternion Rotation => Quaternion.identity;
        public bool IsLocalized    => true;

        public RebuiltAnchorHandle(Guid uuid, Vector3 position) { Uuid = uuid; Position = position; }
    }
}
