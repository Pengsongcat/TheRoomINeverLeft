using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// Top-level calibration state machine.
// States: Loading → Calibrating ↔ Confirming → Ready → Running (A fires OnCalibrationAccepted once)
// OnLayoutReady fires on entering Confirming (shape check passed or anchors loaded),
// so the sound layout is already fitted to the room while staff confirm.
public class CalibrationFlow : MonoBehaviour
{
    public enum State { Loading, Calibrating, Confirming, Ready, Running }

    [Header("Components")]
    [SerializeField] RoomCornerCalibrator _calibrator;
    [SerializeField] RoomDebugView        _debugView;
    [SerializeField] CalibrationHud       _hud; // optional; messages always go to the console too
    [SerializeField] SoundVisualizer      _soundVisualizer; // optional; sound meshes + legend

    [Header("Input")]
    [SerializeField] InputActionReference _confirmAction;     // A button
    [SerializeField] InputActionReference _recalibrateAction; // B button with Hold interaction
    [SerializeField] InputActionReference _toggleVisualizerAction; // left X (keyboard V in the Editor)

    public event Action<RoomRect> OnLayoutReady;         // apply room fit + show sound markers
    public event Action<RoomRect> OnCalibrationAccepted; // staff pressed Start

    public State CurrentState { get; private set; }

    RoomRect   _pendingRect;
    Vector3[]  _rawCorners; // placed anchor positions, for debug view
    IReadOnlyList<IAnchorHandle> _confirmedAnchors; // anchors behind the current Confirm (placed or loaded)
    bool       _visualizerOn;
    bool       _savingCorners;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start() => _ = StartLoadingAsync();

    void OnEnable()
    {
        _confirmAction.action.Enable();
        _recalibrateAction.action.Enable();
        _confirmAction.action.performed     += OnConfirmInput;
        _recalibrateAction.action.performed += OnRecalibrateInput;
        if (_toggleVisualizerAction != null)
        {
            _toggleVisualizerAction.action.Enable();
            _toggleVisualizerAction.action.performed += OnToggleVisualizerInput;
        }

        _calibrator.OnCornersAccepted  += HandleCornersAccepted;
        _calibrator.OnShapeCheckFailed += HandleShapeCheckFailed;
        _calibrator.OnCornerPlaced     += HandleCornerCountChanged;
        _calibrator.OnCornerRemoved    += HandleCornerRemoved;
        _calibrator.OnCornerRejected   += HandleCornerRejected;
    }

    void OnDisable()
    {
        _confirmAction.action.performed     -= OnConfirmInput;
        _recalibrateAction.action.performed -= OnRecalibrateInput;
        if (_toggleVisualizerAction != null)
            _toggleVisualizerAction.action.performed -= OnToggleVisualizerInput;

        _calibrator.OnCornersAccepted  -= HandleCornersAccepted;
        _calibrator.OnShapeCheckFailed -= HandleShapeCheckFailed;
        _calibrator.OnCornerPlaced     -= HandleCornerCountChanged;
        _calibrator.OnCornerRemoved    -= HandleCornerRemoved;
        _calibrator.OnCornerRejected   -= HandleCornerRejected;
    }

    // ─── Load on startup ───────────────────────────────────────────────────────

    async Task StartLoadingAsync()
    {
        SetState(State.Loading);
        _hud?.SetVisible(true);
        Log("Loading saved room…");

        XRPlatformServices.Instance.PassthroughService.Enable();

        // Anchors left over from a redraw that was never confirmed (app closed first).
        _ = ErasePendingAnchorsAsync();

        var ids = RoomAnchorStore.Load();
        if (ids.Length >= 3)
        {
            var service = XRPlatformServices.Instance.AnchorService;
            var handles = await service.LoadAnchorsAsync(ids);
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
            // Not enough to rebuild the room. The saved IDs stay in the store and are marked for erase
            // when the new corners are saved.
            service.ReleaseAnchors(handles);
        }

        GoToCalibrating();
    }

    // ─── State transitions ─────────────────────────────────────────────────────

    void GoToCalibrating()
    {
        SetState(State.Calibrating);
        _calibrator.Activate();
        SetVisualizer(false);
        _hud?.ClearError();
        ShowCornerInstruction();
    }

    // Corners go clockwise seen from above; corners 1 → 2 are the front wall (the wall the visitor faces).
    static readonly string[] CornerNames =
    {
        "front wall, left end",
        "front wall, right end",
        "back wall, right end",
        "back wall, left end",
    };

    void ShowCornerInstruction()
    {
        int n = _calibrator.CornerCount;
        if (n >= 4) return;
        string controls = n == 0 ? "Trigger = place" : "Trigger = place · B = undo · hold B = start over";
        Log($"Corner {n + 1} of 4: {CornerNames[n]}\n<size=75%>Point at the floor. {controls}</size>");
    }

    void GoToConfirming(Vector3[] rawCorners, IReadOnlyList<IAnchorHandle> handles)
    {
        _rawCorners       = rawCorners;
        _confirmedAnchors = handles;
        _pendingRect = new RoomRect(rawCorners[0], rawCorners[1], rawCorners[2], rawCorners[3]);
        SetState(State.Confirming);

        OnLayoutReady?.Invoke(_pendingRect); // listeners hand sound markers to the debug view before Show

        var fit = RoomFit.Compute(_pendingRect, new LabReference());
        _hud?.ClearError();
        Log($"Room {_pendingRect.width:F1} × {_pendingRect.length:F1} m · scale {fit.sx:F2}x\n" +
            "<size=75%>Check the corners and sounds. A = accept · hold B = redo corners · X = sounds on/off</size>");
        SetVisualizer(true);
    }

    void GoToReady()
    {
        SetState(State.Ready);
        SetVisualizer(false);
        Log("Ready\n<size=75%>A = start the experience · X = sounds on/off</size>");
    }

    // Confirming / Ready: planned sound markers from the debug view.
    // Running: the sounds' own visualizer meshes (only sounds currently active) plus the room outline.
    void SetVisualizer(bool on)
    {
        _visualizerOn = on;
        bool hasLayout = CurrentState is State.Confirming or State.Ready or State.Running;
        bool running   = CurrentState == State.Running;

        if (on && hasLayout) _debugView.Show(_pendingRect, _rawCorners, includeSounds: !running);
        else                 _debugView.Hide();

        _soundVisualizer?.SetMeshesVisible(on && running);
        _soundVisualizer?.SetLegendVisible(on && hasLayout);
    }

    // ─── Input ─────────────────────────────────────────────────────────────────

    void OnConfirmInput(InputAction.CallbackContext _)
    {
        if (CurrentState == State.Confirming)
        {
            GoToReady();
            var erase = ErasePendingAnchorsAsync(); // fire and forget: the replaced calibration's anchors
        }
        else if (CurrentState == State.Ready)
        {
            SetState(State.Running); // further A / B presses are ignored
            Log("Starting.");
            _hud?.SetVisible(false);
            SetVisualizer(false);
            OnCalibrationAccepted?.Invoke(_pendingRect);
        }
    }

    void OnToggleVisualizerInput(InputAction.CallbackContext _)
    {
        if (CurrentState is State.Loading or State.Calibrating) return; // no layout yet
        SetVisualizer(!_visualizerOn);
    }

    // Hold B: from Confirming, redo the corners; while placing corners, start over.
    // Saved anchors are not erased here but marked, and erased once the new corners are confirmed.
    void OnRecalibrateInput(InputAction.CallbackContext _)
    {
        if (_savingCorners) return;
        if (CurrentState != State.Confirming && CurrentState != State.Calibrating) return;
        bool anchorsWereLoaded = _calibrator.CornerCount == 0; // Confirm came from saved anchors, not placement
        _calibrator.Deactivate();
        _calibrator.ClearAnchors(); // anchors placed this session (not yet confirmed, so erased right away)
        if (CurrentState == State.Confirming)
        {
            // Anchors loaded from a previous session are not owned by the calibrator; stop tracking them here.
            if (anchorsWereLoaded && _confirmedAnchors != null)
                XRPlatformServices.Instance.AnchorService.ReleaseAnchors(_confirmedAnchors);
            _confirmedAnchors = null;
            RoomAnchorStore.MarkForErase(RoomAnchorStore.Load()); // includes IDs that failed to load or were rebuilt
            RoomAnchorStore.Clear();
        }
        GoToCalibrating();
    }

    // ─── Calibrator callbacks ──────────────────────────────────────────────────

    async void HandleCornersAccepted(RoomRect rect)
    {
        if (_savingCorners) return;
        _savingCorners = true;
        _calibrator.Deactivate();
        Log("Saving corners…");

        var anchors = _calibrator.GetAnchors();
        var service = XRPlatformServices.Instance.AnchorService;
        bool saved;
        string error;
        try
        {
            saved = await service.SaveAnchorsAsync(anchors);
            error = service.LastSaveError;
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            saved = false;
            error = e.Message;
        }
        finally { _savingCorners = false; }
        if (!saved)
        {
            _calibrator.ClearAnchors();
            GoToCalibrating();
            ShowError($"Could not save the corners.\n{error}\nPlace them again after resolving the issue.");
            return;
        }

        var ids = new Guid[anchors.Length];
        for (int i = 0; i < anchors.Length; i++) ids[i] = anchors[i].Uuid;

        // A previous calibration still in the store (e.g. its anchors failed to load) is replaced now.
        var replaced = new List<Guid>(RoomAnchorStore.Load());
        replaced.RemoveAll(id => Array.IndexOf(ids, id) >= 0);
        RoomAnchorStore.MarkForErase(replaced);
        RoomAnchorStore.Save(ids);

        GoToConfirming(_calibrator.GetPositions(), anchors);
    }

    void HandleShapeCheckFailed(string reason, int badSide)
    {
        Log("Room shape is not valid\n<size=75%>B = undo last corner · hold B = start over</size>");
        ShowError(reason);
    }

    void HandleCornerCountChanged(int index, Vector3 position)
    {
        if (CurrentState != State.Calibrating) return;
        _hud?.ClearError();
        ShowCornerInstruction();
    }

    void HandleCornerRemoved(int index) => HandleCornerCountChanged(index, default);

    void HandleCornerRejected(string reason)
    {
        if (CurrentState == State.Calibrating) ShowError(reason);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    // Erases anchors from replaced calibrations by UUID, so ones that never loaded are erased too.
    // The current corners are never erased. On failure the list is kept and retried next time.
    static async Task ErasePendingAnchorsAsync()
    {
        var pending = RoomAnchorStore.LoadPendingErase();
        if (pending.Length == 0) return;

        var current = RoomAnchorStore.Load();
        var toErase = new List<Guid>();
        foreach (var id in pending)
            if (Array.IndexOf(current, id) < 0) toErase.Add(id);

        try
        {
            if (toErase.Count > 0 && !await XRPlatformServices.Instance.AnchorService.EraseAnchorsByIdAsync(toErase))
            {
                Debug.LogWarning($"[CalibrationFlow] Could not erase {toErase.Count} previous anchor(s); will retry.");
                return;
            }
            RoomAnchorStore.ClearPendingErase(pending);
            if (toErase.Count > 0) Debug.Log($"[CalibrationFlow] Erased {toErase.Count} previous anchor(s).");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CalibrationFlow] Erasing previous anchors failed; will retry. {e.Message}");
        }
    }

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
    void Log(string msg)
    {
        Debug.Log($"[CalibrationFlow] {msg}");
        _hud?.SetMessage(msg);
    }

    void ShowError(string error)
    {
        Debug.Log($"[CalibrationFlow] {error}");
        _hud?.ShowError(error);
    }

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
