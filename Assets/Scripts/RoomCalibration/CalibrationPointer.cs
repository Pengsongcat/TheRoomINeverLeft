using UnityEngine;

// Shows a sphere on the floor where the controller ray will place the next corner.
// Visible only while CalibrationFlow is in the Calibrating state and the ray hits the floor.
public class CalibrationPointer : MonoBehaviour
{
    [SerializeField] CalibrationFlow      _flow;
    [SerializeField] RoomCornerCalibrator _calibrator;
    [SerializeField] float _sphereRadius = 0.04f;
    [SerializeField] Color _color        = Color.cyan;

    GameObject _marker;

    void Awake()
    {
        _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _marker.name = "PointerMarker";
        Destroy(_marker.GetComponent<Collider>());
        _marker.transform.localScale = Vector3.one * (_sphereRadius * 2f);
        _marker.GetComponent<Renderer>().material.color = _color;
        _marker.SetActive(false);
    }

    void Update()
    {
        if (_flow.CurrentState != CalibrationFlow.State.Calibrating ||
            !_calibrator.TryGetFloorHit(out var point))
        {
            _marker.SetActive(false);
            return;
        }
        _marker.transform.position = point;
        _marker.SetActive(true);
    }

    void OnDestroy() => Destroy(_marker);
}
