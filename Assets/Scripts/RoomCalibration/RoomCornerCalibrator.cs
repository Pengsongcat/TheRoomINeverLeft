using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

// Manages placing, undoing, and validating the four room corner anchors during calibration.
// Corners are placed where the controller ray meets the system floor (XRRig.FloorY).
// Each corner is checked before its anchor is created; the full shape is checked after the 4th.
// Activate() / Deactivate() are called by CalibrationFlow to start and stop input listening.
public class RoomCornerCalibrator : MonoBehaviour
{
    [SerializeField] InputActionReference _placeAction;
    [SerializeField] InputActionReference _undoAction;

    [Header("Floor Pointer")]
    [SerializeField] Transform _rayOrigin; // controller pointer; ray goes along its forward axis
    [SerializeField] float     _maxRayDistance = 10f;
    [SerializeField] bool      _rayIsRightHand = true; // which controller vibrates

    public event Action<RoomRect> OnCornersAccepted;
    public event Action<string, int> OnShapeCheckFailed; // reason, bad side index (-1 = whole shape)

    public event Action<int, Vector3> OnCornerPending; // index, position — anchor is being created
    public event Action<int, Vector3> OnCornerPlaced;  // index, anchored position
    public event Action<int>          OnCornerRemoved; // index
    public event Action<string>       OnCornerRejected;
    public event Action               OnCornersCleared;

    public int  CornerCount => _positions.Count;
    public bool IsPlacing   => _placing;

    readonly List<Vector3>       _positions = new();
    readonly List<IAnchorHandle> _anchors   = new();
    bool _active;
    bool _placing;    // prevent re-entrant placement
    int  _generation; // bumped on every restart, so a slow anchor creation can tell it is stale

    public void Activate()
    {
        Unsubscribe(); // safe to call again while already active
        _generation++;
        _active  = true;
        _placing = false;
        _positions.Clear();
        _anchors.Clear();
        _placeAction.action.Enable();
        _undoAction.action.Enable();
        _placeAction.action.performed += OnPlace;
        _undoAction.action.performed  += OnUndo;
        OnCornersCleared?.Invoke();
    }

    public void Deactivate()
    {
        _active = false;
        Unsubscribe();
    }

    void Unsubscribe()
    {
        _placeAction.action.performed -= OnPlace;
        _undoAction.action.performed  -= OnUndo;
    }

    // Erase all placed anchors without saving (e.g. when restarting calibration).
    public async void ClearAnchors()
    {
        // Copy and clear first, so a following Activate() can't empty the list before it is erased.
        var toErase = _anchors.ToArray();
        _generation++;
        _anchors.Clear();
        _positions.Clear();
        OnCornersCleared?.Invoke();

        if (toErase.Length == 0) return;
        await XRPlatformServices.Instance.AnchorService.EraseAnchorsAsync(toErase);
    }

    public IAnchorHandle[] GetAnchors()   => _anchors.ToArray();
    public Vector3[]       GetPositions() => _positions.ToArray();

    // ─── Input handlers ────────────────────────────────────────────────────────

    public bool TryGetFloorHit(out Vector3 point)
    {
        point = default;
        if (_rayOrigin == null) return false;
        float floorY = XRPlatformServices.Instance.Rig.FloorY;
        return FloorPlane.TryRaycast(_rayOrigin.position, _rayOrigin.forward, floorY, _maxRayDistance, out point);
    }

    void OnPlace(InputAction.CallbackContext _)
    {
        if (!_active || _placing) return;
        if (_rayOrigin == null)
        {
            Debug.LogWarning("RoomCornerCalibrator: ray origin is not assigned.");
            return;
        }
        if (_positions.Count >= 4)
        {
            Reject("All 4 corners placed. B = undo last corner, hold B = start over.");
            return;
        }
        if (!TryGetFloorHit(out var point))
        {
            Reject("Point the controller at the floor.");
            return;
        }
        if (!RoomShapeCheck.ValidateNext(_positions, point, out var reason))
        {
            Reject(reason);
            return;
        }
        PlaceCornerAsync(point);
    }

    void OnUndo(InputAction.CallbackContext _)
    {
        if (!_active || _placing || _positions.Count == 0) return;
        UndoLastAsync();
    }

    void Reject(string reason)
    {
        ControllerHaptics.Reject(_rayIsRightHand);
        OnCornerRejected?.Invoke(reason);
    }

    // ─── Async helpers (fire-and-forget; errors logged) ────────────────────────

    async void PlaceCornerAsync(Vector3 position)
    {
        _placing = true;
        int index      = _positions.Count;
        int generation = _generation;
        OnCornerPending?.Invoke(index, position);
        try
        {
            var service = XRPlatformServices.Instance.AnchorService;
            var handle  = await service.CreateAnchorAsync(position, Quaternion.identity);
            if (generation != _generation || !_active)
            {
                // Calibration was restarted while the anchor was being created: drop it.
                if (handle != null) await service.EraseAnchorsAsync(new[] { handle });
                return;
            }
            if (handle == null)
            {
                Debug.LogWarning("RoomCornerCalibrator: anchor creation failed.");
                OnCornerRemoved?.Invoke(index);
                Reject("Could not create the anchor. Try again.");
                return;
            }

            _positions.Add(handle.Position); // use the anchor's world-locked position
            _anchors.Add(handle);
            ControllerHaptics.Confirm(_rayIsRightHand);
            OnCornerPlaced?.Invoke(index, handle.Position);

            if (_positions.Count == 4)
                ValidateAndNotify();
        }
        finally
        {
            _placing = false;
        }
    }

    async void UndoLastAsync()
    {
        int index = _anchors.Count - 1;
        var last  = _anchors[index];
        _anchors.RemoveAt(index);
        _positions.RemoveAt(index);
        OnCornerRemoved?.Invoke(index);

        var service = XRPlatformServices.Instance.AnchorService;
        await service.EraseAnchorsAsync(new[] { last });
    }

    // After the 4th corner. On failure all four corners stay, so staff can see the problem
    // and undo (B) or start over (hold B).
    void ValidateAndNotify()
    {
        if (!RoomShapeCheck.Validate(_positions.ToArray(), out var reason, out int badSide))
        {
            ControllerHaptics.Reject(_rayIsRightHand);
            OnShapeCheckFailed?.Invoke(reason, badSide);
            return;
        }
        var rect = new RoomRect(_positions[0], _positions[1], _positions[2], _positions[3]);
        OnCornersAccepted?.Invoke(rect);
    }
}

// Short controller vibrations through the Input System, so no headset-specific API is needed.
public static class ControllerHaptics
{
    public static void Confirm(bool rightHand = true) => Pulse(rightHand, 0.3f, 0.05f);
    public static void Reject(bool rightHand = true)  => Pulse(rightHand, 0.8f, 0.25f);

    public static void Pulse(bool rightHand, float amplitude, float duration)
    {
        var usage = rightHand ? CommonUsages.RightHand : CommonUsages.LeftHand;
        InputSystem.GetDevice<XRControllerWithRumble>(usage)?.SendImpulse(amplitude, duration);
    }
}
