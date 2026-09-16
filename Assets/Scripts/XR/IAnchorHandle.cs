using System;
using UnityEngine;

public interface IAnchorHandle
{
    Guid Uuid { get; }
    Vector3 Position { get; }
    Quaternion Rotation { get; }
    bool IsLocalized { get; }
}
