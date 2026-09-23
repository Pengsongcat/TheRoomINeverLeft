using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Development aid for scrubbing Timelines in Edit mode. Put it on a parent: it covers every Audio Source on this
// object and its children while not playing. Steam Audio's spatializer only runs in Play mode, so a spatialized
// source is silent in Edit mode; Mode picks how they are heard instead:
//   - Unity 3D: spatializer off, Spatial Blend kept. Unity's own 3D audio: the source's rolloff curve and panning
//     at the listener (the Scene view camera, with the Scene view's audio toggle on). No HRTF, occlusion or reflections.
//   - Flat: spatializer off, Spatial Blend 0. Full volume wherever the camera is.
//   - Unchanged: the sources keep their own settings (silent if spatialized); only the spheres are drawn.
// Each source gets a sphere in the Scene view, drawn without selecting it: in its own colour while it is sounding
// (hues spread evenly in hierarchy order, so neighbours never look alike), plain grey while silent. In Play mode
// and builds it does nothing.
//
// Sounding is read from the source's output, since audio played by a Timeline Audio Track does not set isPlaying.
// The Audio Sources' own settings are never saved changed: they are put back before a scene or prefab is saved,
// before Play mode and whenever this component is disabled or removed. Nested auditions over the same source
// share it: the original settings are kept once, Flat wins over Unity 3D, and they are put back when the last one
// lets go.
[ExecuteAlways]
public class EditModeAudition : MonoBehaviour
{
    public enum Mode { Unity3D, Flat, Unchanged }

    [Tooltip("How the sources are heard in Edit mode. Unity 3D: Steam Audio spatializer off, volume follows the " +
             "Scene camera. Flat: 2D, full volume everywhere. Unchanged: own settings, only the spheres.")]
    [SerializeField] Mode _mode = Mode.Unity3D;
    [Tooltip("Sounding spheres: each source gets its own hue, spread evenly in hierarchy order. " +
             "Saturation, brightness and alpha of those colours.")]
    [SerializeField, Range(0f, 1f)] float _saturation = 0.8f;
    [SerializeField, Range(0f, 1f)] float _brightness = 1f;
    [SerializeField, Range(0f, 1f)] float _playingAlpha = 0.6f;
    [Tooltip("Sphere colour while it is silent.")]
    [SerializeField] Color _silentColour = new(0.7f, 0.7f, 0.7f, 0.25f);
    [Tooltip("Marker sphere across, metres.")]
    [SerializeField, Min(0.01f)] float _size = 0.15f;

#if UNITY_EDITOR
    const float SilenceRms = 0.0001f;

    readonly HashSet<AudioSource> _sources  = new();
    readonly HashSet<AudioSource> _held     = new(); // the sources this audition has changed, in _heldMode
    readonly List<AudioSource>    _order    = new(); // hierarchy order, which sets each source's hue
    readonly HashSet<AudioSource> _sounding = new();
    readonly float[] _samples = new float[256];
    Mode _heldMode;

    void OnEnable()
    {
        Refresh();
        EditorApplication.hierarchyChanged += Refresh;
        EditorApplication.update += Poll;
    }

    void OnDisable()
    {
        EditorApplication.hierarchyChanged -= Refresh;
        EditorApplication.update -= Poll;
        foreach (var source in _held) Listening.Release(source, _heldMode);
        _held.Clear();
        _sources.Clear();
        _sounding.Clear();
    }

    // Mode changed in the inspector; other components may not be changed from OnValidate itself.
    void OnValidate() => EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Refresh(); };

    // Takes in sources added under this object and lets go of ones removed or moved out; on a Mode change, lets go
    // of them all and takes them in again in the new mode.
    void Refresh()
    {
        if (this == null) return;
        var found = GetComponentsInChildren<AudioSource>(includeInactive: true);
        var now = new HashSet<AudioSource>(found);
        var hold = _mode == Mode.Unchanged ? new HashSet<AudioSource>() : now;

        if (_heldMode != _mode)
        {
            foreach (var source in _held) Listening.Release(source, _heldMode);
            _held.Clear();
            _heldMode = _mode;
        }
        foreach (var source in _held) if (!hold.Contains(source)) Listening.Release(source, _heldMode);
        foreach (var source in hold) if (!_held.Contains(source)) Listening.Acquire(source, _heldMode);
        _held.Clear();
        _held.UnionWith(hold);

        _sources.Clear();
        _sources.UnionWith(now);
        _sounding.IntersectWith(now);
        _order.Clear();
        _order.AddRange(found);
    }

    // Repaints the Scene view when a source starts or stops sounding, so the colour follows a Timeline scrub.
    void Poll()
    {
        if (Application.isPlaying) return;
        bool changed = false;
        foreach (var source in _sources)
        {
            bool sounding = Sounding(source);
            changed |= sounding ? _sounding.Add(source) : _sounding.Remove(source);
        }
        if (changed) SceneView.RepaintAll();
    }

    bool Sounding(AudioSource source)
    {
        if (source == null || !source.isActiveAndEnabled) return false;
        if (source.isPlaying) return true;
        source.GetOutputData(_samples, 0);
        float sum = 0f;
        foreach (float s in _samples) sum += s * s;
        return Mathf.Sqrt(sum / _samples.Length) > SilenceRms;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        for (int i = 0; i < _order.Count; i++)
        {
            var source = _order[i];
            if (source == null || !source.gameObject.activeInHierarchy) continue;
            var playing = Color.HSVToRGB((float)i / _order.Count, _saturation, _brightness);
            playing.a = _playingAlpha;
            Gizmos.color = _sounding.Contains(source) ? playing : _silentColour;
            Gizmos.DrawSphere(source.transform.position, _size * 0.5f);
        }
    }

    // The changed sources and their own settings, shared by every audition in the editor.
    static class Listening
    {
        class Original
        {
            public float SpatialBlend;
            public bool  Spatialize;
            public int   FlatUsers, Unity3DUsers;
            public Mode  Applied = Mode.Unchanged; // what the source is set to now; Unchanged = its own settings
            public Mode  Wanted => FlatUsers > 0 ? Mode.Flat : Unity3DUsers > 0 ? Mode.Unity3D : Mode.Unchanged;
        }

        static readonly Dictionary<AudioSource, Original> Sources = new();

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorSceneManager.sceneSaving += (scene, _) => Set(s => s.gameObject.scene == scene, on: false);
            EditorSceneManager.sceneSaved  += scene      => Set(s => s.gameObject.scene == scene, on: true);
            PrefabStage.prefabSaving       += root       => Set(s => s.transform.IsChildOf(root.transform), on: false);
            PrefabStage.prefabSaved        += root       => Set(s => s.transform.IsChildOf(root.transform), on: true);
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode) Set(_ => true, on: false);
                if (change == PlayModeStateChange.EnteredEditMode) Set(_ => true, on: true);
            };
        }

        public static void Acquire(AudioSource source, Mode mode)
        {
            if (source == null || mode == Mode.Unchanged) return;
            if (!Sources.TryGetValue(source, out var o))
                Sources[source] = o = new Original { SpatialBlend = source.spatialBlend, Spatialize = source.spatialize };
            if (mode == Mode.Flat) o.FlatUsers++; else o.Unity3DUsers++;
            Apply(source, o, o.Wanted);
        }

        public static void Release(AudioSource source, Mode mode)
        {
            if (source == null || mode == Mode.Unchanged || !Sources.TryGetValue(source, out var o)) return;
            if (mode == Mode.Flat) o.FlatUsers--; else o.Unity3DUsers--;
            Apply(source, o, o.Wanted);
            if (o.Wanted == Mode.Unchanged) Sources.Remove(source);
        }

        static void Set(System.Func<AudioSource, bool> which, bool on)
        {
            foreach (var pair in Sources)
                if (pair.Key != null && which(pair.Key)) Apply(pair.Key, pair.Value, on ? pair.Value.Wanted : Mode.Unchanged);
        }

        static void Apply(AudioSource source, Original o, Mode mode)
        {
            if (mode != Mode.Unchanged && (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)) return;
            if (mode == o.Applied) return;
            source.spatialBlend = mode == Mode.Flat      ? 0f : o.SpatialBlend;
            source.spatialize   = mode == Mode.Unchanged && o.Spatialize;
            o.Applied = mode;
        }
    }
#endif
}
