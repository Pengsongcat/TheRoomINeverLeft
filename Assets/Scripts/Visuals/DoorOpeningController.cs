using UnityEngine;

/// <summary>Local-Z door motion: camera ready, ajar with rebound/wind, then proximity opening.</summary>
[DisallowMultipleComponent]
public sealed class DoorOpeningController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Door leaf to rotate. Defaults to this GameObject. Only its local Z is animated.")]
    public GameObject doorLeaf;

    [Header("Startup / Camera Ready")]
    public float closedAngle = 0f;
    [Min(0f)] public float cameraOriginTolerance = 0.001f;
    [Min(0f)] public float openingDelay = 3f;

    [Header("Ajar Opening")]
    public float ajarAngle = -20f;
    [Min(0.01f)] public float ajarDuration = 1.5f;

    [Header("Wind (offset relative to ajar angle)")]
    public bool enableWind = true;
    [Min(0f)] public float windAmplitude = 2f;
    public Vector2 windPauseRange = new Vector2(0.5f, 2.5f);
    public Vector2 windMoveDurationRange = new Vector2(1f, 2.5f);

    [Header("Proximity Full Opening")]
    [Tooltip("Distance from the main camera to this object in metres. Horizontal mode ignores headset height.")]
    [Min(0f)] public float triggerDistance = 0.5f;
    [Tooltip("Ignore vertical height difference between the headset and door pivot.")]
    public bool useHorizontalDistance = true;
    public float fullOpenAngle = -135f;
    [Min(0.01f)] public float fullOpenDuration = 1.5f;

    [Header("Full Open Soft Rebound")]
    [Tooltip("Overshoot beyond the full-open angle in degrees. Zero disables rebound.")]
    [Min(0f)] public float reboundAngle = 3f;
    [Tooltip("Additional settling time after the opening motion. Longer feels softer.")]
    [Min(0.01f)] public float reboundDuration = 1.1f;

    public event System.Action<int> OpeningStarted; // 1 = ajar, 2 = full opening

    enum Phase { WaitingForCamera, Delay, OpeningAjar, Wind, OpeningFull, Open }
    Phase phase;
    Transform leaf;
    Vector2 baseXY;
    float angle, elapsed, fromAngle;
    float windWait, windElapsed, windDuration, windFrom, windTarget;

    public float GetTriggerDistance(Vector3 cameraPosition)
    {
        Vector3 delta = cameraPosition - transform.position;
        if (useHorizontalDistance) delta.y = 0f;
        return delta.magnitude;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        float radius = Mathf.Max(0f, triggerDistance);
        if (!useHorizontalDistance) { Gizmos.DrawWireSphere(transform.position, radius); return; }
        Vector3 previous = transform.position + Vector3.right * radius;
        for (int i = 1; i <= 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64f;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    void Reset() { doorLeaf = gameObject; }

    void Start()
    {
        if (!doorLeaf) doorLeaf = gameObject;
        leaf = doorLeaf.transform;
        Vector3 euler = leaf.localEulerAngles;
        baseXY = new Vector2(euler.x, euler.y);
        phase = Phase.WaitingForCamera;
        elapsed = 0f;
        SetAngle(closedAngle);
    }

    void Update() { Tick(Time.deltaTime); }

    void Tick(float deltaTime, bool detectCamera = true)
    {
        if (!leaf) return;
        float dt = Mathf.Max(0f, deltaTime);
        // Resolve every frame so late-spawned or replacement scene cameras work without prefab references.
        Camera camera = detectCamera ? Camera.main : null;
        // Distance takes priority from the first frame, even before camera-ready/delay.
        if (phase != Phase.OpeningFull && phase != Phase.Open && camera &&
            GetTriggerDistance(camera.transform.position) < Mathf.Max(0f, triggerDistance))
        {
            fromAngle = angle;
            elapsed = 0f;
            phase = Phase.OpeningFull;
            OpeningStarted?.Invoke(2);
        }

        switch (phase)
        {
            case Phase.WaitingForCamera:
                if (camera && camera.transform.position.sqrMagnitude > cameraOriginTolerance * cameraOriginTolerance)
                {
                    elapsed = 0f;
                    phase = Phase.Delay;
                }
                break;
            case Phase.Delay:
                elapsed += dt;
                if (elapsed >= Mathf.Max(0f, openingDelay))
                {
                    fromAngle = angle;
                    elapsed = 0f;
                    phase = Phase.OpeningAjar;
                    OpeningStarted?.Invoke(1);
                }
                break;
            case Phase.OpeningAjar:
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, ajarDuration));
                SetAngle(Mathf.Lerp(fromAngle, ajarAngle, Ease(t)));
                if (t >= 1f) { phase = Phase.Wind; ScheduleWind(); }
                break;
            case Phase.Wind:
                if (!enableWind) { SetAngle(ajarAngle); break; }
                if (windWait > 0f) { windWait -= dt; break; }
                windElapsed += dt;
                float windT = Mathf.Clamp01(windElapsed / windDuration);
                SetAngle(ajarAngle + Mathf.Lerp(windFrom, windTarget, Ease(windT)));
                if (windT >= 1f) ScheduleWind();
                break;
            case Phase.OpeningFull:
                elapsed += dt;
                float travelTime = Mathf.Max(0.01f, fullOpenDuration);
                float distance = fullOpenAngle - fromAngle;
                float amplitude = Mathf.Min(Mathf.Max(0f, reboundAngle), Mathf.Abs(distance) * 0.1f);
                float peak = fullOpenAngle + Mathf.Sign(distance) * amplitude;
                if (elapsed < travelTime)
                {
                    SetAngle(Mathf.Lerp(fromAngle, peak, Ease(elapsed / travelTime)));
                }
                else
                {
                    float settle = amplitude > 0f
                        ? Mathf.Clamp01((elapsed - travelTime) / Mathf.Max(0.01f, reboundDuration)) : 1f;
                    float u = Ease(settle);
                    // Smooth at both joins: zero velocity/acceleration, with a small
                    // opposite-side swing whose amplitude decays to exactly zero.
                    float spring = Mathf.Cos(2f * Mathf.PI * u) * Mathf.Pow(1f - u, 3f);
                    SetAngle(fullOpenAngle + Mathf.Sign(distance) * amplitude * spring);
                    if (settle >= 1f) { SetAngle(fullOpenAngle); phase = Phase.Open; }
                }
                break;
        }
    }

#if UNITY_EDITOR
    // Editor-only entry points share the actual animation state machine.
    public void BeginEditorPreview()
    {
        leaf = doorLeaf ? doorLeaf.transform : transform;
        Vector3 euler = leaf.localEulerAngles;
        baseXY = new Vector2(euler.x, euler.y);
        angle = Mathf.DeltaAngle(0f, euler.z);
        elapsed = 0f;
    }

    public void PlayEditorPreview(int mode)
    {
        elapsed = 0f;
        if (mode == 2) // Full opening can interrupt any preview from its current angle.
        {
            fromAngle = angle;
            phase = Phase.OpeningFull;
            OpeningStarted?.Invoke(2);
        }
        else
        {
            SetAngle(mode == 3 ? ajarAngle : closedAngle);
            fromAngle = angle;
            phase = mode == 0 ? Phase.Delay : mode == 3 ? Phase.Wind : Phase.OpeningAjar;
            if (mode == 3) ScheduleWind();
            if (mode == 1) OpeningStarted?.Invoke(1);
        }
    }

    public void TickEditorPreview(float deltaTime) { Tick(deltaTime, false); }
    public string EditorPreviewStatus => phase + "  Z: " + angle.ToString("F1") + "°";
#endif
    void ScheduleWind()
    {
        windFrom = angle - ajarAngle;
        windTarget = Random.Range(-Mathf.Max(0f, windAmplitude), Mathf.Max(0f, windAmplitude));
        windWait = RandomRange(windPauseRange, 0f);
        windDuration = RandomRange(windMoveDurationRange, 0.01f);
        windElapsed = 0f;
    }

    static float RandomRange(Vector2 range, float minimum)
    {
        return Random.Range(Mathf.Max(minimum, Mathf.Min(range.x, range.y)),
            Mathf.Max(minimum, Mathf.Max(range.x, range.y)));
    }

    // Quintic easing: zero velocity and acceleration at either end.
    static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (6f * t - 15f) + 10f); }

    void SetAngle(float value)
    {
        angle = value;
        leaf.localRotation = Quaternion.Euler(baseXY.x, baseXY.y, value);
    }
}






