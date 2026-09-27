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
    [Tooltip("With Start From Headset or Follow Headset: the spot relative to the floor under the headset, metres: " +
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

    [Header("Follow the headset")]
    [Tooltip("On: for the whole clip the object stays at Start Offset from the headset, following the visitor as " +
             "they move and turn (e.g. a narrator who is always beside them). Target is ignored.")]
    public bool FollowHeadset;
    [Tooltip("With Follow Headset: seconds at the start of the clip spent gliding over from where it is.")]
    [Min(0f)] public float FollowGatherTime = 1f;

    [Header("Orbit around the headset")]
    [Tooltip("On: instead of heading for Target, the object moves in from where it is onto a circle around the " +
             "headset (Gather Time), then circles it clockwise, speeding up. The circle follows the headset; its " +
             "front is where the visitor faced when the orbit started.")]
    public bool Orbit;
    [Tooltip("Where on the circle it goes in, degrees: 0 = in front, 90 = right, 180 = behind, 270 = left.")]
    public float OrbitStartAngle;
    [Tooltip("Seconds at the start of the clip spent moving in onto the circle; the circling starts after.")]
    [Min(0f)] public float OrbitGatherTime = 2f;
    [Tooltip("Full turns made after the gather.")]
    [Min(0f)] public float OrbitTurns = 3f;
    [Tooltip("How many times faster it circles at the end than at the start (1 = steady).")]
    [Min(1f)] public float OrbitSpeedUp = 1f;
    [Tooltip("Distance from the headset when passing in front, metres.")]
    [Min(0f)] public float OrbitNear = 1f;
    [Tooltip("Distance from the headset when passing behind, metres.")]
    [Min(0f)] public float OrbitFar = 2f;
    [Tooltip("Height above the floor, metres.")]
    public float OrbitHeight = 1.5f;
    [Tooltip("Size of the circle at the end, as a fraction of Near/Far (1 = same size). It closes in as the turns " +
             "go by, so it tightens as it speeds up.")]
    [Range(0.05f, 1f)] public float OrbitEndSize = 1f;

    // Share of the turns done (0..1) at u = 0..1 through the circling: speed grows smoothly by Speed Up.
    public static float OrbitDone(float speedUp, float u) =>
        speedUp <= 1.0001f ? u : (Mathf.Pow(speedUp, u) - 1f) / (speedUp - 1f);

    // Angle on the circle (degrees) at u = 0..1 through the circling.
    public static float OrbitAngle(float startAngle, float turns, float speedUp, float u) =>
        startAngle + 360f * turns * OrbitDone(speedUp, u);

    public ClipCaps clipCaps => ClipCaps.None;

    public override double duration => 3.0;

    // The track's mixer does the moving; the clip only marks the move on the Timeline.
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Create(graph);
}
