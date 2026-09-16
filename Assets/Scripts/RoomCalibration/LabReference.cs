using System;
using UnityEngine;

[Serializable]
public class LabReference
{
    [Tooltip("Design room width in metres (x axis).")]
    public float width = 5.2f;

    [Tooltip("Design room length in metres (z axis).")]
    public float length = 8.7f;

    [Tooltip("Lab centre x offset from scene origin.")]
    public float centreX = -0.21f;

    [Tooltip("Lab centre z offset from scene origin.")]
    public float centreZ = 0.30f;
}
