using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// Measures the real room. Idle until Begin() is called (by the Experience Manager, with passthrough already on):
//   load the saved corners (Confirming) or place four new ones (Placing)
//   → Confirming: LayoutReady(rect) fires, so the room is already fitted while staff check it
//   → A = accept → Completed(rect).
// Only knows about corners; what gets fitted or started listens to the events.
// Corner placement, anchor storage and the text panel are reused from TRINL.RoomCalibration (V1).
public class RoomCalibrator : MonoBehaviour
{
    public enum State { Idle, Loading, Placing, Confirming, Done }

    [Header("Components")]
    [SerializeField] RoomCornerCalibrator _corners;
    [SerializeField] CalibrationHud       _hud;

    [Header("Input")]
    [SerializeField] InputActionReference _confirmAction; // A: accept the corners
    [SerializeField] InputActionReference _redoAction;    // hold B: redo the corners / start over

    public event Action<RoomRect> LayoutReady; // corners accepted or loaded; fit the room now
    public event Action<RoomRect> Completed;   // staff accepted the corners; calibration is over

    public State    CurrentState { get; private set; }
    public RoomRect Rect         { get; private set; }
    public Vector3[] RawCorners  { get; private set; } // anchor positions behind Rect, for the confirm view

    IReadOnlyList<IAnchorHandle> _confirmedAnchors; // anchors behind the current Confirming (placed or loaded)
    bool _savingCorners;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _confirmAction.action.Enable();
        _redoAction.action.Enable();
        _confirmAction.action.performed += OnConfirmInput;
        _redoAction.action.performed    += OnRedoInput;

        _corners.OnCornersAccepted  += HandleCornersAccepted;
        _corners.OnShapeCheckFailed += HandleShapeCheckFailed;
        _corners.OnCornerPlaced     += HandleCornerCountChanged;
        _corners.OnCornerRemoved    += HandleCornerRemoved;
        _corners.OnCornerRejected   += HandleCornerRejected;
    }

    void OnDisable()
    {
        _confirmAction.action.performed -= OnConfirmInput;
        _redoAction.action.performed    -= OnRedoInput;

        _corners.OnCornersAccepted  -= HandleCornersAccepted;
        _corners.OnShapeCheckFailed -= HandleShapeCheckFailed;
        _corners.OnCornerPlaced     -= HandleCornerCountChanged;
        _corners.OnCornerRemoved    -= HandleCornerRemoved;
        _corners.OnCornerRejected   -= HandleCornerRejected;
    }

    // Starts calibrating. Passthrough is the caller's job.
    public void Begin()
    {
        if (CurrentState is not (State.Idle or State.Done)) return;
        _hud.SetVisible(true);
        _ = LoadOrPlaceAsync();
    }

    // ─── Input ─────────────────────────────────────────────────────────────────

    void OnConfirmInput(InputAction.CallbackContext context)
    {
        if (CurrentState != State.Confirming) return;
        CurrentState = State.Done;
        _hud.SetVisible(false);
        _ = ErasePendingAnchorsAsync(); // the replaced calibration's anchors
        Debug.Log("[RoomCalibrator] Corners accepted; calibration complete.");
        Completed?.Invoke(Rect);
    }

    // Hold B: from Confirming, redo the corners; while placing corners, start over.
    // Saved anchors are not erased here but marked, and erased once the new corners are confirmed.
    void OnRedoInput(InputAction.CallbackContext _)
    {
        if (_savingCorners) return;
        if (CurrentState != State.Confirming && CurrentState != State.Placing) return;
        bool anchorsWereLoaded = _corners.CornerCount == 0; // Confirming came from saved anchors, not placement
        _corners.Deactivate();
        _corners.ClearAnchors(); // anchors placed this session (not confirmed yet, so erased right away)
        if (CurrentState == State.Confirming)
        {
            // Anchors loaded from a previous session are not owned by the corner calibrator; stop tracking them.
            if (anchorsWereLoaded && _confirmedAnchors != null)
                XRPlatformServices.Instance.AnchorService.ReleaseAnchors(_confirmedAnchors);
            _confirmedAnchors = null;
            RoomAnchorStore.MarkForErase(RoomAnchorStore.Load()); // includes IDs that failed to load or were rebuilt
            RoomAnchorStore.Clear();
        }
        GoToPlacing();
    }

    // ─── States ────────────────────────────────────────────────────────────────

    async Task LoadOrPlaceAsync()
    {
        CurrentState = State.Loading;
        Log("Loading saved room…");

        // Anchors left over from a redraw that was never confirmed (app closed first).
        _ = ErasePendingAnchorsAsync();

        var ids = RoomAnchorStore.Load();
        if (ids.Length >= 3)
        {
            var service = XRPlatformServices.Instance.AnchorService;
            var handles = await service.LoadAnchorsAsync(ids);
            if (handles.Count == 4)
            {
                // Anchors load in any order; corner order is what says which wall is the front.
                var ordered = InSavedOrder(handles, ids);
                if (ordered != null)
                {
                    GoToConfirming(Positions(ordered), ordered);
                    return;
                }
            }
            if (handles.Count == 3)
            {
                var four = TryRebuildFourthCorner(handles, ids);
                if (four != null)
                {
                    GoToConfirming(Positions(four), four);
                    return;
                }
            }
            // Not enough to rebuild the room. The saved IDs stay in the store and are marked for erase
            // when the new corners are saved.
            service.ReleaseAnchors(handles);
        }

        GoToPlacing();
    }

    void GoToPlacing()
    {
        CurrentState = State.Placing;
        _corners.Activate();
        _hud.ClearError();
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
        int n = _corners.CornerCount;
        if (n >= 4) return;
        string controls = n == 0 ? "Trigger = place" : "Trigger = place · B = undo · hold B = start over";
        Log($"Corner {n + 1} of 4: {CornerNames[n]}\n<size=75%>Point at the floor. {controls}</size>");
    }

    void GoToConfirming(Vector3[] rawCorners, IReadOnlyList<IAnchorHandle> handles)
    {
        RawCorners        = rawCorners;
        _confirmedAnchors = handles;
        Rect = new RoomRect(rawCorners[0], rawCorners[1], rawCorners[2], rawCorners[3]);
        CurrentState = State.Confirming;

        LayoutReady?.Invoke(Rect);

        _hud.ClearError();
        Log($"Room {Rect.width:F1} × {Rect.length:F1} m\n" +
            "<size=75%>Check the corners. A = accept · hold B = redo corners</size>");
    }

    // ─── Corner calibrator callbacks ───────────────────────────────────────────

    async void HandleCornersAccepted(RoomRect rect)
    {
        if (_savingCorners) return;
        _savingCorners = true;
        _corners.Deactivate();
        Log("Saving corners…");

        var anchors = _corners.GetAnchors();
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
            _corners.ClearAnchors();
            GoToPlacing();
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

        GoToConfirming(_corners.GetPositions(), anchors);
    }

    void HandleShapeCheckFailed(string reason, int badSide)
    {
        Log("Room shape is not valid\n<size=75%>B = undo last corner · hold B = start over</size>");
        ShowError(reason);
    }

    void HandleCornerCountChanged(int index, Vector3 position)
    {
        if (CurrentState != State.Placing) return;
        _hud.ClearError();
        ShowCornerInstruction();
    }

    void HandleCornerRemoved(int index) => HandleCornerCountChanged(index, default);

    void HandleCornerRejected(string reason)
    {
        if (CurrentState == State.Placing) ShowError(reason);
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
                Debug.LogWarning($"[RoomCalibrator] Could not erase {toErase.Count} previous anchor(s); will retry.");
                return;
            }
            RoomAnchorStore.ClearPendingErase(pending);
            if (toErase.Count > 0) Debug.Log($"[RoomCalibrator] Erased {toErase.Count} previous anchor(s).");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RoomCalibrator] Erasing previous anchors failed; will retry. {e.Message}");
        }
    }

    static Vector3[] Positions(IReadOnlyList<IAnchorHandle> handles)
    {
        var pos = new Vector3[handles.Count];
        for (int i = 0; i < handles.Count; i++) pos[i] = handles[i].Position;
        return pos;
    }

    // The loaded anchors in saved ID order, or null if they don't match the saved IDs.
    static IReadOnlyList<IAnchorHandle> InSavedOrder(IReadOnlyList<IAnchorHandle> handles, Guid[] savedIds)
    {
        if (handles.Count != savedIds.Length) return null;
        var ordered = new IAnchorHandle[handles.Count];
        foreach (var h in handles)
        {
            int i = Array.IndexOf(savedIds, h.Uuid);
            if (i < 0 || ordered[i] != null) return null;
            ordered[i] = h;
        }
        return ordered;
    }

    // Rebuilds a missing 4th anchor using the parallelogram property.
    // Returns the four in saved ID order, or null if that is not possible.
    static IReadOnlyList<IAnchorHandle> TryRebuildFourthCorner(IReadOnlyList<IAnchorHandle> three, Guid[] savedIds)
    {
        if (savedIds.Length < 4) return null;

        var byId = new Dictionary<Guid, IAnchorHandle>();
        foreach (var h in three) byId[h.Uuid] = h;

        var ordered    = new IAnchorHandle[4];
        int missingIdx = -1;
        for (int i = 0; i < 4; i++)
        {
            if (byId.TryGetValue(savedIds[i], out ordered[i])) continue;
            if (missingIdx >= 0) return null; // more than one missing
            missingIdx = i;
        }
        if (missingIdx < 0) return null;

        var known = new Vector3[4];
        for (int i = 0; i < 4; i++) if (ordered[i] != null) known[i] = ordered[i].Position;
        ordered[missingIdx] = new RebuiltAnchorHandle(savedIds[missingIdx], RoomRect.RebuildCorner(missingIdx, known));
        return ordered;
    }

    void Log(string message)
    {
        Debug.Log($"[RoomCalibrator] {message}");
        _hud.SetMessage(message);
    }

    void ShowError(string error)
    {
        Debug.Log($"[RoomCalibrator] {error}");
        _hud.ShowError(error);
    }

    // Stand-in for an anchor position that was rebuilt from the other three, not loaded.
    sealed class RebuiltAnchorHandle : IAnchorHandle
    {
        public Guid       Uuid        { get; }
        public Vector3    Position    { get; }
        public Quaternion Rotation    => Quaternion.identity;
        public bool       IsLocalized => true;

        public RebuiltAnchorHandle(Guid uuid, Vector3 position) { Uuid = uuid; Position = position; }
    }
}
