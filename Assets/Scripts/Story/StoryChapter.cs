using System.Collections.Generic;
using UnityEngine;

// A chapter made of scenes the visitor can play in any order. All scenes wait (guide sounds loop) until one is
// triggered; all guide sounds fade out and other triggers disarm. The triggered guide fades twice as long,
// while its Timeline begins when the other guides finish fading. Unplayed scenes then resume waiting.
//
// When the chapter opens, no trigger may start under the visitor. Each scene's Trigger + Guide go back to their
// designed spots; if the visitor (Player Body, tag Player) is within a trigger's radius + body radius + Clearance,
// the Trigger + Guide pairs are turned together around the chapter's centre (the room centre) by the smallest angle
// that clears them all. A trigger near the centre barely moves when turned, so any still too close is then pushed
// straight away from the visitor. The scenes' own sounds and visuals stay where they were designed.
public class StoryChapter : StoryPart
{
    const float AngleStep = 5f;

    [Tooltip("Space kept between the visitor's body and the edge of any trigger when the chapter opens, metres.")]
    [SerializeField, Min(0f)] float _clearance = 0.3f;
    [SerializeField] string _playerTag = "Player";

    StoryScene[] _scenes;
    StoryScene _activeScene;
    int _pendingOtherGuides;
    bool _activeTimelineStarted;
    bool _activeTimelineFinished;
    bool _activeGuideFaded;

    // Designed local positions of every scene's Trigger and Guide, restored each time the chapter opens.
    readonly Dictionary<Transform, Vector3> _designed = new();

    void Awake()
    {
        _scenes = GetComponentsInChildren<StoryScene>(includeInactive: true);
        foreach (var scene in _scenes)
        {
            scene.Triggered += OnSceneTriggered;
            scene.Finished  += OnSceneFinished;
            foreach (var spot in Spot(scene))
                if (spot != null) _designed[spot] = spot.localPosition;
        }
    }

    static Transform[] Spot(StoryScene scene) => new[]
    {
        scene.Trigger != null ? scene.Trigger.transform : null,
        scene.Guide   != null ? scene.Guide.transform   : null,
    };

    public override void Begin()
    {
        _activeScene = null;
        if (_scenes.Length == 0)
        {
            Debug.LogWarning($"[Story] {name} has no scenes.");
            RaiseFinished();
            return;
        }

        KeepTriggersClearOfVisitor();

        foreach (var scene in _scenes)
        {
            bool alreadyActive = scene.gameObject.activeSelf;
            scene.ResetProgress();
            scene.gameObject.SetActive(true);
            if (alreadyActive) scene.ResumeWaiting();
        }
    }

    // A scene playing → skip that scene (the chapter then carries on as usual); otherwise skip the whole chapter.
    public override void Skip()
    {
        if (_activeScene != null && !_activeScene.Done) _activeScene.Skip();
        else RaiseFinished();
    }

    void OnSceneTriggered(StoryScene triggered)
    {
        _activeScene = triggered;
        _activeTimelineStarted = false;
        _activeTimelineFinished = false;
        _activeGuideFaded = false;
        _pendingOtherGuides = 0;

        foreach (var scene in _scenes)
            if (scene != triggered && !scene.Done && scene.gameObject.activeInHierarchy)
                _pendingOtherGuides++;

        triggered.FadeOutGuide(2f, () =>
        {
            if (_activeScene != triggered) return;
            _activeGuideFaded = true;
            if (_activeTimelineFinished) CompleteScene(triggered);
        });

        foreach (var scene in _scenes)
        {
            if (scene == triggered || scene.Done || !scene.gameObject.activeInHierarchy) continue;
            scene.FadeOutGuide(1f, () =>
            {
                if (_activeScene != triggered) return;
                scene.gameObject.SetActive(false);
                _pendingOtherGuides--;
                TryStartTimeline(triggered);
            });
        }
        TryStartTimeline(triggered);
    }

    void TryStartTimeline(StoryScene triggered)
    {
        if (_activeScene != triggered || _activeTimelineStarted || _pendingOtherGuides > 0) return;
        _activeTimelineStarted = true;
        triggered.StartTimeline();
    }

    void OnSceneFinished(StoryScene finished)
    {
        if (_activeScene != finished) return;
        _activeTimelineFinished = true;
        if (_activeGuideFaded) CompleteScene(finished);
    }

    void CompleteScene(StoryScene finished)
    {
        _activeScene = null;
        finished.gameObject.SetActive(false);

        bool allDone = true;
        foreach (var scene in _scenes)
        {
            if (scene.Done) continue;
            allDone = false;
            if (scene.gameObject.activeSelf) scene.ResumeWaiting();
            else scene.gameObject.SetActive(true);
        }

        if (allDone) RaiseFinished();
    }

    // ---- Keeping the triggers clear of the visitor when the chapter opens ----

    void KeepTriggersClearOfVisitor()
    {
        foreach (var pair in _designed)
            if (pair.Key != null) pair.Key.localPosition = pair.Value;

        var body = GameObject.FindWithTag(_playerTag);
        if (body == null) return;
        Vector3 visitor = body.transform.position;
        float bodyRadius = BodyRadius(body);

        var triggers = new List<(StoryScene scene, float reach)>();
        foreach (var scene in _scenes)
            if (scene.Trigger != null)
                triggers.Add((scene, TriggerRadius(scene.Trigger) + bodyRadius + _clearance));
        if (triggers.Count == 0 || Clearance(triggers, visitor, 0f) >= 0f) return;

        // Smallest turn that clears every trigger; failing that, the turn that leaves the most room.
        float angle = 0f, bestAngle = 0f, bestClearance = float.NegativeInfinity;
        bool cleared = false;
        for (float step = AngleStep; step <= 180f && !cleared; step += AngleStep)
        {
            foreach (float a in new[] { step, -step })
            {
                float c = Clearance(triggers, visitor, a);
                if (c > bestClearance) { bestClearance = c; bestAngle = a; }
                if (c >= 0f) { angle = a; cleared = true; break; }
            }
        }
        if (!cleared) angle = bestAngle;

        foreach (var (scene, _) in triggers)
            foreach (var spot in Spot(scene))
                if (spot != null) spot.position = Turned(spot.position, angle);

        // Anything still too close (a trigger near the centre) is pushed straight away from the visitor.
        int pushed = 0;
        foreach (var (scene, reach) in triggers)
        {
            Vector3 away = Flat(scene.Trigger.transform.position - visitor);
            float gap = away.magnitude;
            if (gap >= reach) continue;
            Vector3 dir = gap > 0.001f ? away / gap : Flat(scene.Trigger.transform.position - transform.position).normalized;
            if (dir == Vector3.zero) dir = Flat(transform.forward).normalized;
            Vector3 shift = dir * (reach - gap);
            foreach (var spot in Spot(scene))
                if (spot != null) spot.position += shift;
            pushed++;
        }

        Debug.Log($"[Story] {name}: triggers turned {angle:0}° around the centre" +
                  (pushed > 0 ? $", {pushed} pushed away" : "") + " to clear the visitor.");
    }

    // Smallest gap (negative = overlap) between the visitor and any trigger's reach, with the layout turned by angle.
    float Clearance(List<(StoryScene scene, float reach)> triggers, Vector3 visitor, float angle)
    {
        float min = float.PositiveInfinity;
        foreach (var (scene, reach) in triggers)
        {
            Vector3 p = Turned(scene.Trigger.transform.position, angle);
            min = Mathf.Min(min, Flat(p - visitor).magnitude - reach);
        }
        return min;
    }

    // Turns a point around the vertical axis through the chapter's origin (the room centre).
    Vector3 Turned(Vector3 point, float angle)
    {
        Vector3 centre = transform.position;
        return centre + Quaternion.AngleAxis(angle, transform.up) * (point - centre);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // World radius of a trigger as it fires now (Room Scaled Ranges has already set it for the room).
    static float TriggerRadius(SceneTrigger trigger)
    {
        var s = trigger.transform.lossyScale;
        if (trigger.TryGetComponent<SphereCollider>(out var sphere)) // a sphere takes its largest scale axis
            return sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        var box = trigger.GetComponent<Collider>().bounds.extents;
        return Mathf.Max(box.x, box.z);
    }

    static float BodyRadius(GameObject body)
    {
        if (!body.TryGetComponent<CapsuleCollider>(out var capsule)) return 0.2f;
        var s = body.transform.lossyScale;
        return capsule.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
    }
}
