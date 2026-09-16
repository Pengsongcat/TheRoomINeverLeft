using UnityEngine;

// Wires CalibrationFlow.OnCalibrationAccepted → RoomLayoutApplier + AudioRangeScaler.
// Must live in Assembly-CSharp (not an asmdef) to reference AudioRangeScaler.
public class CalibrationConnector : MonoBehaviour
{
    [SerializeField] CalibrationFlow   _flow;
    [SerializeField] RoomLayoutApplier _applier;
    [SerializeField] AudioRangeScaler  _scaler;

    void OnEnable()  => _flow.OnCalibrationAccepted += OnAccepted;
    void OnDisable() => _flow.OnCalibrationAccepted -= OnAccepted;

    void OnAccepted(RoomRect rect)
    {
        _applier.Apply(rect);
        _scaler.Scale(_applier.LastFit.rangeFactor);
    }
}
