using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// One move of a Move Track. The clip's start is when the move begins; when the clip ends, the object stops. With
// Start From Headset, the object first jumps to Start Offset from the headset. A target too close to the visitor is
// swapped for a spot Min Distance From Visitor away; the target object itself is not moved.
public class MoveClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("Where this move heads. Place it under Room so it follows the room fit.")]
    public ExposedReference<Transform> Target;
    [Tooltip("Metres per second.")]
    [Min(0f)] public float Speed = 1f;
    [Tooltip("On: this move starts at Start Offset from the headset (taken when the move starts), instead of from " +
             "where the object is.")]
    public bool StartFromHeadset;
    [Tooltip("With Start From Headset: where the move starts, relative to the floor under the headset, metres: " +
             "X right, Y up from the floor, Z forward (level, ignoring head tilt).")]
    public Vector3 StartOffset = new Vector3(0f, 0f, 0.3f);
    [Tooltip("A target closer than this to the visitor when the move starts, metres, is swapped for a spot this far " +
             "away (visitor → target direction, kept on the room floor). 0 = off.")]
    [Min(0f)] public float MinDistanceFromVisitor = 1f;
    [Tooltip("On: Min Distance is measured on the floor, ignoring height. Off: measured in 3D.")]
    public bool IgnoreHeight = true;
    [Tooltip("On: the object waits where it is, then moves at Speed so it reaches the target exactly when the clip " +
             "ends (faster than Speed if the way is too long for the clip). Use it when something on the Timeline " +
             "must happen on arrival (e.g. a door), whatever the distance turns out to be. Off: it sets off at the " +
             "clip's start and stops on arrival.")]
    public bool ArriveAtEnd;

    public ClipCaps clipCaps => ClipCaps.None;

    public override double duration => 3.0;

    // The track's mixer does the moving; the clip only marks the move on the Timeline.
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Create(graph);
}
