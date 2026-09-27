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
                ArriveAtEnd  = move.ArriveAtEnd,
                Follow       = move.FollowHeadset,
                FollowGatherTime = move.FollowGatherTime,
                Orbit        = move.Orbit,
                OrbitStartAngle = move.OrbitStartAngle,
                OrbitGatherTime = move.OrbitGatherTime,
                OrbitTurns   = move.OrbitTurns,
                OrbitSpeedUp = move.OrbitSpeedUp,
                OrbitNear    = move.OrbitNear,
                OrbitFar     = move.OrbitFar,
                OrbitHeight  = move.OrbitHeight,
                OrbitEndSize = move.OrbitEndSize,
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
        public bool      ArriveAtEnd;
        public bool      Follow;
        public float     FollowGatherTime;
        public bool      Orbit;
        public float     OrbitStartAngle;
        public float     OrbitGatherTime;
        public float     OrbitTurns;
        public float     OrbitSpeedUp;
        public float     OrbitNear;
        public float     OrbitFar;
        public float     OrbitHeight;
        public float     OrbitEndSize;
    }

    public List<Move> Moves;

    bool          _hasStart;
    Vector3       _start;
    Vector3?[]    _targets; // each move's destination, fixed when that move starts
    Vector3?[]    _froms;   // each Start From Headset move's start point, fixed when that move starts
    Quaternion?[] _facings; // each orbit's front, fixed when that orbit starts
    Vector3?[]    _follows; // each Follow Headset move's latest spot, kept once that move has ended

    public override void OnGraphStart(Playable playable)
    {
        _hasStart = false;
        _targets = null;
        _froms = null;
        _facings = null;
        _follows = null;
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
        _facings ??= new Quaternion?[Moves.Count];
        _follows ??= new Vector3?[Moves.Count];
        double now = playable.GetTime();
        Vector3 position = _start;
        for (int i = 0; i < Moves.Count; i++)
        {
            var move = Moves[i];
            if (now <= move.Start) break;
            if (move.FromHeadset) position = _froms[i] ??= AtHeadset(move.Offset);
            if (move.Follow)
            {
                // Beside the visitor, live while the clip runs; afterwards it stays where it last was.
                bool running = now < move.Start + move.Duration;
                Vector3 spot = running ? (_follows[i] = AtHeadset(move.Offset)).Value : _follows[i] ??= AtHeadset(move.Offset);
                float since = (float)(now - move.Start);
                float gather = Mathf.Min(move.FollowGatherTime, (float)move.Duration);
                position = since < gather ? Vector3.Lerp(position, spot, Mathf.SmoothStep(0f, 1f, since / gather)) : spot;
                continue;
            }
            if (move.Orbit)
            {
                position = OnOrbit(move, _facings[i] ??= Facing(Head()), position, (float)System.Math.Min(now - move.Start, move.Duration));
                continue;
            }
            if (move.Target == null) continue;
            _targets[i] ??= AwayFromVisitor(move.Target.position, move.MinDistance, move.IgnoreHeight);
            float elapsed = (float)System.Math.Min(now - move.Start, move.Duration);
            float speed = move.Speed;
            if (move.ArriveAtEnd)
            {
                // Wait, then walk so the target is reached as the clip ends; faster than Speed if the way is too long.
                float distance = Vector3.Distance(position, _targets[i].Value);
                float duration = (float)move.Duration;
                speed = Mathf.Max(speed, distance / Mathf.Max(duration, 0.0001f));
                float wait = speed > 0f ? duration - distance / speed : 0f;
                elapsed = Mathf.Max(0f, elapsed - wait);
            }
            position = Vector3.MoveTowards(position, _targets[i].Value, speed * elapsed);
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
    static Vector3 AtHeadset(Vector3 offset) => UnderHead(Head()) + Facing(Head()) * offset;

    static Vector3 UnderHead(Transform head)
    {
        var room = Object.FindFirstObjectByType<RoomFitter>();
        return new Vector3(head.position.x, room != null ? room.transform.position.y : 0f, head.position.z);
    }

    // Where the visitor faces, level. A camera looking straight down (a top-down Scene view) uses its up instead.
    static Quaternion Facing(Transform head)
    {
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    // Orbit: moves in from where it is onto the circle (Gather Time), then circles the headset clockwise, speeding up.
    // Close in front (Near), further behind (Far), the circle closing in to End Size. The centre follows the headset;
    // the front stays where it was.
    static Vector3 OnOrbit(Move move, Quaternion facing, Vector3 from, float elapsed)
    {
        float gather = Mathf.Min(move.OrbitGatherTime, (float)move.Duration);
        float circling = (float)move.Duration - gather;
        float u = circling > 0f ? Mathf.Clamp01((elapsed - gather) / circling) : 0f;
        float angle = MoveClip.OrbitAngle(move.OrbitStartAngle, move.OrbitTurns, move.OrbitSpeedUp, u) * Mathf.Deg2Rad;
        float radius = Mathf.Lerp(move.OrbitFar, move.OrbitNear, (1f + Mathf.Cos(angle)) / 2f)
                     * Mathf.Lerp(1f, move.OrbitEndSize, MoveClip.OrbitDone(move.OrbitSpeedUp, u));
        var onCircle = UnderHead(Head()) + facing * new Vector3(Mathf.Sin(angle) * radius, move.OrbitHeight, Mathf.Cos(angle) * radius);
        return elapsed < gather ? Vector3.Lerp(from, onCircle, Mathf.SmoothStep(0f, 1f, elapsed / gather)) : onCircle;
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
