using System.Text;
using UnityEngine;

// Room scaling lines, read from the Room's RoomFitter:
//   Measured room size / Scale method / Scale rate / Current room size (the design space as it is now).
public class RoomScaleText : DebugTextSource
{
    [Tooltip("The Room's RoomFitter. Set on the scene instance of the Debugger.")]
    [SerializeField] RoomFitter _room;

    public override void Write(StringBuilder text, float windowSeconds)
    {
        if (_room == null)
        {
            text.Append("Room: not assigned");
            return;
        }

        var current = _room.CurrentSize;
        if (_room.Calibrated)
        {
            var measured = _room.MeasuredSize;
            text.Append($"Measured room size: {measured.x:F2} × {measured.y:F2} m\n");
            text.Append($"Scale method: {RoomFitter.ScaleMethod}\n");
            text.Append($"Scale rate: {_room.Scale:F2}\n");
        }
        else
        {
            text.Append("Measured room size: not calibrated\n");
            text.Append("Scale method: none\n");
            text.Append($"Scale rate: {_room.Scale:F2}\n");
        }
        text.Append($"Current room size: {current.x:F2} × {current.y:F2} m");
    }
}
