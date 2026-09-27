using System.Collections.Generic;
using UnityEngine;

/// <summary>Snap this object's world Y to recent eye height when any monitored light turns on.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class LightOnEyeHeight : MonoBehaviour
{
    [Tooltip("Seconds of Main Camera world height to average. Uses available history during startup.")]
    [Min(0.01f)] [SerializeField] float _historySeconds = 5f;
    [Tooltip("Intensity at or below this value counts as off. Disabled/inactive lights also count as off.")]
    [Min(0f)] [SerializeField] float _offThreshold = 0f;
    [Tooltip("Include the Light on this object as well as lights in all descendants.")]
    [SerializeField] bool _includeSelf = true;
    [Tooltip("Optional world-space offset from the averaged camera height.")]
    [SerializeField] float _heightOffset;

    struct Sample
    {
        public double time;
        public float height;
        public Sample(double time, float height) { this.time = time; this.height = height; }
    }

    readonly List<Sample> _samples = new List<Sample>(1024);
    readonly List<Light> _lights = new List<Light>();
    readonly Dictionary<Light, bool> _wasOn = new Dictionary<Light, bool>();
    Camera _camera;

    void OnEnable()
    {
        _samples.Clear();
        _wasOn.Clear();
        _camera = null;
        RefreshLights();
    }

    void OnDisable()
    {
        _samples.Clear();
        _wasOn.Clear();
        _camera = null;
    }

    void OnTransformChildrenChanged() { RefreshLights(); }

    [ContextMenu("Refresh Monitored Lights")]
    public void RefreshLights()
    {
        GetComponentsInChildren<Light>(true, _lights);
        if (!_includeSelf) _lights.RemoveAll(light => light.transform == transform);
        // Keep existing transition states when the hierarchy changes.
        var removed = new List<Light>();
        foreach (var entry in _wasOn)
            if (!entry.Key || !_lights.Contains(entry.Key)) removed.Add(entry.Key);
        foreach (var light in removed) _wasOn.Remove(light);
    }

    void LateUpdate()
    {
        var main = Camera.main;
        if (main != _camera)
        {
            _camera = main;
            _samples.Clear(); // Never mix two cameras' histories.
        }
        if (_camera) RecordHeight(Time.unscaledTimeAsDouble, _camera.transform.position.y);
        CheckLights();
    }

    void RecordHeight(double now, float height)
    {
        if (_samples.Count > 0 && now <= _samples[_samples.Count - 1].time) _samples.Clear();
        _samples.Add(new Sample(now, height));
        double cutoff = now - Mathf.Max(0.01f, _historySeconds);
        int remove = 0;
        // Retain the sample just before the boundary so the window can be interpolated.
        while (remove + 1 < _samples.Count && _samples[remove + 1].time <= cutoff) remove++;
        if (remove > 0) _samples.RemoveRange(0, remove);
    }

    float AverageHeight()
    {
        if (_samples.Count == 0) return transform.position.y - _heightOffset;
        var last = _samples[_samples.Count - 1];
        double start = System.Math.Max(_samples[0].time, last.time - Mathf.Max(0.01f, _historySeconds));
        double duration = last.time - start;
        if (duration <= 0) return last.height;
        double area = 0;
        for (int i = 1; i < _samples.Count; i++)
        {
            var a = _samples[i - 1];
            var b = _samples[i];
            double left = System.Math.Max(start, a.time);
            if (b.time <= left) continue;
            float leftHeight = Mathf.Lerp(a.height, b.height, (float)((left - a.time) / (b.time - a.time)));
            area += (leftHeight + b.height) * 0.5 * (b.time - left);
        }
        return (float)(area / duration);
    }

    void CheckLights()
    {
        bool started = false;
        foreach (var light in _lights)
        {
            if (!light) continue;
            bool on = light.isActiveAndEnabled && light.intensity > _offThreshold;
            _wasOn.TryGetValue(light, out bool previouslyOn);
            started |= on && !previouslyOn;
            _wasOn[light] = on;
        }
        if (!started || _samples.Count == 0) return;
        var position = transform.position;
        position.y = AverageHeight() + _heightOffset;
        transform.position = position;
    }
}
