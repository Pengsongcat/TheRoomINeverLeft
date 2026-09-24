using System;
using System.Collections.Generic;
using UnityEngine;

// A chapter made of scenes the visitor can play in any order. All scenes wait (guide sounds loop) until one is
// triggered; all guide sounds fade out and other triggers disarm. Each guide uses its own triggered or
// untriggered fade-out time; the Timeline begins when the other guides finish fading.
//
// When the chapter opens, no trigger may start under the visitor. Each scene's Trigger + Guide go back to their
// designed spots; if the visitor (Player Body, tag Player) is within a trigger's radius + body radius + Clearance,
// the Trigger + Guide pairs are turned together around the chapter's centre (the room centre) by the smallest angle
// that clears them all. A trigger near the centre barely moves when turned, so any still too close is then pushed
// out to the edge of the visitor's reach, straight away from them if that spot is free, otherwise the nearest spot
// round the visitor that does not overlap another trigger. The scenes' own sounds and visuals stay where they were
// designed.
public class StoryChapter : StoryPart
{
    const float AngleStep = 5f;
    const float PushStep  = 10f; // degrees between the spots tried for a pushed trigger

    [Tooltip("Space kept between the visitor's body and the edge of any trigger when the chapter opens, metres.")]
    [SerializeField, Min(0f)] float _clearance = 0.3f;
    [SerializeField] string _playerTag = "Player";

    // Development toggle, set in Edit Mode and read when Play starts: an unticked scene is left off and the chapter
    // runs as if it were not there. The list fills itself from the child scenes.
    [Serializable]
    struct SceneToggle
    {
        [HideInInspector] public string name;
        public StoryScene scene;
        public bool included;
    }

    [Tooltip("Untick a scene to leave it out of this chapter (development). Read when Play starts.")]
    [SerializeField] List<SceneToggle> _sceneToggles = new();

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
        var included = new List<StoryScene>();
        foreach (var scene in GetComponentsInChildren<StoryScene>(includeInactive: true))
        {
            if (IsIncluded(scene)) included.Add(scene);
            else scene.gameObject.SetActive(false);
        }
        _scenes = included.ToArray();

        foreach (var scene in _scenes)
        {
            scene.Triggered += OnSceneTriggered;
            scene.Finished  += OnSceneFinished;
            scene.CanTrigger = _ => _activeScene == null;
            foreach (var spot in Spot(scene))
                if (spot != null) _designed[spot] = spot.localPosition;
        }
    }

    // A scene missing from the list (added since the list last filled) takes part.
    bool IsIncluded(StoryScene scene)
    {
        foreach (var toggle in _sceneToggles)
            if (toggle.scene == scene) return toggle.included;
        return true;
    }

    // Keeps the toggle list matching the child scenes: new scenes arrive ticked, removed ones drop out, the rest keep
    // their tick. Only rewritten when something changed, so a prefab instance gains no needless overrides.
    void OnValidate()
    {
        var scenes = GetComponentsInChildren<StoryScene>(includeInactive: true);
        bool changed = scenes.Length != _sceneToggles.Count;
        for (int i = 0; !changed && i < scenes.Length; i++)
            changed = _sceneToggles[i].scene != scenes[i] || _sceneToggles[i].name != scenes[i].name;
        if (!changed) return;

        var synced = new List<SceneToggle>(scenes.Length);
        foreach (var scene in scenes)
            synced.Add(new SceneToggle { name = scene.name, scene = scene, included = IsIncluded(scene) });
        _sceneToggles = synced;
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

        triggered.FadeOutGuide(true, () =>
        {
            if (_activeScene != triggered) return;
            _activeGuideFaded = true;
            if (_activeTimelineFinished) CompleteScene(triggered);
        });

        foreach (var scene in _scenes)
        {
            if (scene == triggered || scene.Done || !scene.gameObject.activeInHierarchy) continue;
            scene.FadeOutGuide(false, () =>
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

        // Anything still too close (a trigger near the centre) is pushed out to the edge of the visitor's reach, at
        // the spot nearest straight-away-from-the-visitor that does not overlap another trigger.
        int pushed = 0, overlapping = 0;
        foreach (var (scene, reach) in triggers)
        {
            Vector3 away = Flat(scene.Trigger.transform.position - visitor);
            float gap = away.magnitude;
            if (gap >= reach) continue;
            Vector3 dir = gap > 0.001f ? away / gap : Flat(scene.Trigger.transform.position - transform.position).normalized;
            if (dir == Vector3.zero) dir = Flat(transform.forward).normalized;

            Vector3 from = scene.Trigger.transform.position;
            Vector3 to = PushTarget(scene, triggers, visitor, dir, reach, from.y, out bool clear);
            if (!clear) overlapping++;
            foreach (var spot in Spot(scene))
                if (spot != null) spot.position += to - from;
            pushed++;
        }

        Debug.Log($"[Story] {name}: triggers turned {angle:0}° around the centre" +
                  (pushed > 0 ? $", {pushed} pushed away" : "") + " to clear the visitor." +
                  (overlapping > 0 ? $" {overlapping} could not avoid overlapping another trigger (room too small)." : ""));
    }

    // Where a trigger too close to the visitor goes: on the circle of its reach around the visitor, trying straight
    // away first, then PushStep further round each way. The first spot that keeps clear of every other trigger (their
    // radii not overlapping) wins; failing that, the spot that overlaps least.
    Vector3 PushTarget(StoryScene pushed, List<(StoryScene scene, float reach)> triggers, Vector3 visitor,
                       Vector3 away, float reach, float height, out bool clear)
    {
        float radius = TriggerRadius(pushed.Trigger);
        Vector3 best = default;
        float bestGap = float.NegativeInfinity;
        for (float step = 0f; step <= 180f; step += PushStep)
        {
            foreach (float a in step == 0f ? new[] { 0f } : new[] { step, -step })
            {
                Vector3 candidate = visitor + Quaternion.AngleAxis(a, transform.up) * away * reach;
                candidate.y = height;

                float gap = float.PositiveInfinity; // smallest space to another trigger's edge
                foreach (var (other, _) in triggers)
                    if (other != pushed)
                        gap = Mathf.Min(gap, Flat(candidate - other.Trigger.transform.position).magnitude
                                             - radius - TriggerRadius(other.Trigger));
                if (gap >= 0f) { clear = true; return candidate; }
                if (gap > bestGap) { bestGap = gap; best = candidate; }
            }
        }
        clear = false;
        return best;
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
