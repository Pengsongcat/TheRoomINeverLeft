using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

// Manages placing, undoing, and validating the four room corner anchors during calibration.
// Activate() / Deactivate() are called by CalibrationFlow to start and stop input listening.
public class RoomCornerCalibrator : MonoBehaviour
{
    [SerializeField] InputActionReference _placeAction;
    [SerializeField] InputActionReference _undoAction;
    [SerializeField] Transform _controllerTip; // right controller tip or hand tracking point

    public event Action<RoomRect> OnCornersAccepted;
    public event Action<string>   OnShapeCheckFailed;

    public int CornerCount => _positions.Count;

    readonly List<Vector3>       _positions = new();
    readonly List<IAnchorHandle> _anchors   = new();
    bool _active;
    bool _placing; // prevent re-entrant placement

    public void Activate()
    {
        _active  = true;
        _placing = false;
        _positions.Clear();
        _anchors.Clear();
        _placeAction.action.Enable();
        _undoAction.action.Enable();
        _placeAction.action.performed += OnPlace;
        _undoAction.action.performed  += OnUndo;
    }

    public void Deactivate()
    {
        _active = false;
        _placeAction.action.performed -= OnPlace;
        _undoAction.action.performed  -= OnUndo;
    }

    // Erase all placed anchors without saving (e.g. when restarting calibration).
    public async void ClearAnchors()
    {
        if (_anchors.Count == 0) return;
        var service = XRPlatformServices.Instance.AnchorService;
        await service.EraseAnchorsAsync(_anchors);
        _anchors.Clear();
        _positions.Clear();
    }

    public IAnchorHandle[] GetAnchors()   => _anchors.ToArray();
    public Vector3[]       GetPositions() => _positions.ToArray();

    // ─── Input handlers ────────────────────────────────────────────────────────

    void OnPlace(InputAction.CallbackContext _)
    {
        if (!_active || _placing || _positions.Count >= 4) return;
        PlaceCornerAsync(_controllerTip.position);
    }

    void OnUndo(InputAction.CallbackContext _)
    {
        if (!_active || _positions.Count == 0) return;
        UndoLastAsync();
    }

    // ─── Async helpers (fire-and-forget; errors logged) ────────────────────────

    async void PlaceCornerAsync(Vector3 position)
    {
        _placing = true;
        try
        {
            var service = XRPlatformServices.Instance.AnchorService;
            var handle  = await service.CreateAnchorAsync(position, Quaternion.identity);
            if (handle == null)
            {
                Debug.LogWarning("RoomCornerCalibrator: anchor creation failed.");
                return;
            }
            _positions.Add(handle.Position); // use the anchor's world-locked position
            _anchors.Add(handle);

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
        var last = _anchors[_anchors.Count - 1];
        _anchors.RemoveAt(_anchors.Count - 1);
        _positions.RemoveAt(_positions.Count - 1);

        var service = XRPlatformServices.Instance.AnchorService;
        await service.EraseAnchorsAsync(new[] { last });
    }

    void ValidateAndNotify()
    {
        if (!RoomShapeCheck.Validate(_positions.ToArray(), out var reason))
        {
            OnShapeCheckFailed?.Invoke(reason);
            // Remove the last anchor so the user can try again from 3 corners.
            UndoLastAsync();
            return;
        }
        var rect = new RoomRect(_positions[0], _positions[1], _positions[2], _positions[3]);
        OnCornersAccepted?.Invoke(rect);
    }
}
