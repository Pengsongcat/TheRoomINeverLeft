using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Timeline track that moves its bound object, starting from where the object is when the Timeline starts. Each Move
// Clip heads for its target at its speed and stops where it is when the clip ends; the next clip starts from there,
// unless it starts from the headset. Gaps between clips are pauses. Per-move settings are on the Move Clip.
[TrackColor(0.2f, 0.75f, 0.8f)]
[TrackClipType(typeof(MoveClip))]
[TrackBindingType(typeof(Transform))]
public class MoveTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer = ScriptPlayable<MoveMixer>.Create(graph, inputCount);
        var behaviour = mixer.GetBehaviour();

        var resolver = graph.GetResolver();
        var moves = new List<MoveMixer.Move>();
        foreach (var clip in GetClips())
        {
            var move = (MoveClip)clip.asset;
            moves.Add(new MoveMixer.Move
            {
                Start    = clip.start,
                Duration = clip.duration,
                Target   = move.Target.Resolve(resolver),
                Speed    = move.Speed,
                FromHeadset = move.StartFromHeadset,
                Offset      = move.StartOffset,
                MinDistance = move.MinDistanceFromVisitor,
                IgnoreHeight = move.IgnoreHeight,
            });
        }
        moves.Sort((a, b) => a.Start.CompareTo(b.Start));
        behaviour.Moves = moves;
        return mixer;
    }

    // Lets Timeline put the object back where it was when an Edit Mode preview ends.
    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        if (director.GetGenericBinding(this) is not Transform bound) return;
        driver.AddFromName<Transform>(bound.gameObject, "m_LocalPosition.x");
        driver.AddFromName<Transform>(bound.gameObject, "m_LocalPosition.y");
        driver.AddFromName<Transform>(bound.gameObject, "m_LocalPosition.z");
    }
}

// Every frame, replays the moves up to now from the start point, so scrubbing shows the right spot.
public class MoveMixer : PlayableBehaviour
{
    public struct Move
    {
        public double    Start;
        public double    Duration;
        public Transform Target;
        public float     Speed;
        public bool      FromHeadset;
        public Vector3   Offset;
        public float     MinDistance;
        public bool      IgnoreHeight;
    }

    public List<Move> Moves;

    bool       _hasStart;
    Vector3    _start;
    Vector3?[] _targets; // each move's destination, fixed when that move starts
    Vector3?[] _froms;   // each Start From Headset move's start point, fixed when that move starts

    public override void OnGraphStart(Playable playable)
    {
        _hasStart = false;
        _targets = null;
        _froms = null;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (playerData is not Transform mover) return;
        if (!_hasStart)
        {
            _start = mover.position;
            _hasStart = true;
        }

        _targets ??= new Vector3?[Moves.Count];
        _froms   ??= new Vector3?[Moves.Count];
        double now = playable.GetTime();
        Vector3 position = _start;
        for (int i = 0; i < Moves.Count; i++)
        {
            var move = Moves[i];
            if (now <= move.Start) break;
            if (move.FromHeadset) position = _froms[i] ??= AtHeadset(move.Offset);
            if (move.Target == null) continue;
            _targets[i] ??= AwayFromVisitor(move.Target.position, move.MinDistance, move.IgnoreHeight);
            float travelled = (float)System.Math.Min(now - move.Start, move.Duration);
            position = Vector3.MoveTowards(position, _targets[i].Value, move.Speed * travelled);
        }
        mover.position = position;
    }

    // The headset (Main Camera). In Edit Mode where there is none (scrubbing a test scene), the Scene view camera.
    static Transform Head()
    {
        if (Camera.main != null) return Camera.main.transform;
#if UNITY_EDITOR
        var view = UnityEditor.SceneView.lastActiveSceneView;
        if (view != null) return view.camera.transform;
#endif
        return null;
    }

    // Offset from the floor under the headset, turned to where the visitor faces.
    static Vector3 AtHeadset(Vector3 offset)
    {
        var head = Head();
        var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(head.forward, Vector3.up), Vector3.up);
        var room = Object.FindFirstObjectByType<RoomFitter>();
        var underHead = new Vector3(head.position.x, room != null ? room.transform.position.y : 0f, head.position.z);
        return underHead + facing * offset;
    }

    // A target closer than minDistance to the visitor is swapped for a spot that far away, kept on the room floor.
    static Vector3 AwayFromVisitor(Vector3 target, float minDistance, bool ignoreHeight)
    {
        var head = Head();
        Vector3 away = target - head.position;
        if (ignoreHeight) away = Vector3.ProjectOnPlane(away, Vector3.up);
        if (minDistance <= 0f || away.magnitude >= minDistance) return target;
        if (away.sqrMagnitude < 0.0001f) away = Vector3.ProjectOnPlane(head.forward, Vector3.up);

        Vector3 spot = head.position + away.normalized * minDistance;
        if (ignoreHeight) spot.y = target.y;
        return OnRoomFloor(spot);
    }

    // Clamps a point into Room's design floor (the fitted square). Left as it is where there is no Room (test scenes).
    static Vector3 OnRoomFloor(Vector3 point)
    {
        var room = Object.FindFirstObjectByType<RoomFitter>();
        if (room == null) return point;
        Vector3 local = room.transform.InverseTransformPoint(point);
        Vector2 half = room.DesignSize / 2f;
        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.z = Mathf.Clamp(local.z, -half.y, half.y);
        return room.transform.TransformPoint(local);
    }
}
