using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// TRINL > Listening Mode: makes Audio Sources audible while scrubbing Timelines in Edit mode, by hand. Steam Audio's
// spatializer only runs in Play mode, so a spatialized source is silent in Edit mode.
//   - Select objects in the Hierarchy (their Audio Sources and all children's are covered) and press Flat (2D,
//     full volume) or Unity 3D (spatializer off, Unity's own rolloff and panning at the Scene view camera).
//   - The first time a source is changed, its own Spatial Blend (whole curve) and Spatialize are stored in
//     Library/ListeningMode.asset (ListeningModeStore), so they survive script reloads and editor restarts.
//     Switching a source between Flat and Unity 3D keeps that first, real original.
//   - Restore puts the stored settings back, and removes the Prefab override left behind when the value matches
//     the prefab again. Restore Selected / Restore All. A source deleted while listening (its scene is open but it
//     is gone) is skipped and its record dropped; one whose scene isn't open is kept until that scene is.
// Nothing happens on its own: restore before saving, applying overrides or entering Play mode. The window counts
// the changed sources, and a scene save with changed sources in it logs a warning.
public class ListeningModeWindow : EditorWindow
{
    Vector2 _scroll;

    [MenuItem("TRINL/Listening Mode")]
    static void Open() => GetWindow<ListeningModeWindow>("Listening Mode");

    [InitializeOnLoadMethod]
    static void WarnOnSave() =>
        EditorSceneManager.sceneSaving += (scene, _) =>
        {
            int n = ListeningModeStore.instance.Records.Count(r => r.ScenePath == scene.path);
            if (n > 0) Debug.LogWarning($"[Listening Mode] {scene.name} saved with {n} Audio Sources still in listening mode. Open TRINL > Listening Mode and Restore, then save again.");
        };

    void OnSelectionChange() => Repaint();
    void OnFocus() => Repaint();

    void OnGUI()
    {
        var records = ListeningModeStore.instance.Records;
        var selected = SelectedSources();

        EditorGUILayout.LabelField($"Selection: {selected.Count} Audio Source(s)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(selected.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Flat", "2D, spatializer off: full volume wherever the camera is."))) Apply(selected, ListeningModeStore.Mode.Flat);
            if (GUILayout.Button(new GUIContent("Unity 3D", "Spatializer off, Spatial Blend kept: Unity's rolloff and panning at the Scene view camera."))) Apply(selected, ListeningModeStore.Mode.Unity3D);
            if (GUILayout.Button("Restore Selected")) Restore(selected.Select(FindRecord).Where(r => r != null).ToList());
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"In listening mode: {records.Count}", EditorStyles.boldLabel);
        if (records.Count > 0)
            EditorGUILayout.HelpBox("Restore before saving, applying Prefab overrides or entering Play mode, or the listening settings are saved.", MessageType.Warning);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var r in records.ToList())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var source = Resolve(r);
                EditorGUILayout.LabelField(r.Mode == ListeningModeStore.Mode.Flat ? "Flat" : "Unity 3D", GUILayout.Width(60));
                var why = SceneOpen(r) ? "Removed from the scene; Restore just drops it" : "Scene not open";
                if (GUILayout.Button(new GUIContent(source ? r.Label : r.Label + (SceneOpen(r) ? "  (removed)" : "  (scene not open)"), source ? "Select" : why), source ? EditorStyles.label : EditorStyles.miniLabel) && source)
                    Selection.activeObject = source.gameObject;
                if (GUILayout.Button("Restore", GUILayout.Width(60))) Restore(new List<ListeningModeStore.Record> { r });
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(records.Count == 0))
            if (GUILayout.Button("Restore All")) Restore(records.ToList());
    }

    static List<AudioSource> SelectedSources() =>
        Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<AudioSource>(true)).Distinct().ToList();

    static string Id(AudioSource s) => GlobalObjectId.GetGlobalObjectIdSlow(s).ToString();

    // By id first. A source added since the scene was last saved gets a new id when the scene is saved, so fall back to
    // its hierarchy path in its scene (and keep the new id).
    static AudioSource Resolve(ListeningModeStore.Record r)
    {
        if (GlobalObjectId.TryParse(r.Id, out var gid) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) is AudioSource byId) return byId;
        if (!SceneOpen(r)) return null;
        int colon = r.Label.IndexOf(": ");
        if (colon < 0) return null;
        var names = r.Label.Substring(colon + 2).Split('/');
        var t = EditorSceneManager.GetSceneByPath(r.ScenePath).GetRootGameObjects().FirstOrDefault(g => g.name == names[0])?.transform;
        for (int i = 1; t != null && i < names.Length; i++) t = t.Find(names[i]);
        var byPath = t != null ? t.GetComponent<AudioSource>() : null;
        if (byPath != null) r.Id = Id(byPath);
        return byPath;
    }

    // The record of a source, if it has one (by id, or by path when the id went stale on a scene save).
    static ListeningModeStore.Record FindRecord(AudioSource s)
    {
        var records = ListeningModeStore.instance.Records;
        var id = Id(s);
        return records.FirstOrDefault(r => r.Id == id) ?? records.FirstOrDefault(r => r.ScenePath == s.gameObject.scene.path && Resolve(r) == s);
    }

    static bool SceneOpen(ListeningModeStore.Record r) =>
        !string.IsNullOrEmpty(r.ScenePath) && EditorSceneManager.GetSceneByPath(r.ScenePath).isLoaded;

    static string Label(AudioSource s)
    {
        var t = s.transform; var path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return $"{s.gameObject.scene.name}: {path}";
    }

    static void Apply(List<AudioSource> sources, ListeningModeStore.Mode mode)
    {
        var store = ListeningModeStore.instance;
        foreach (var s in sources)
        {
            var r = FindRecord(s);
            if (r == null) // first time: keep its real settings
            {
                r = new ListeningModeStore.Record
                {
                    Id = Id(s), Label = Label(s), ScenePath = s.gameObject.scene.path,
                    Spatialize = s.spatialize,
                    SpatialBlend = s.spatialBlend,
                    CurveTimes  = s.GetCustomCurve(AudioSourceCurveType.SpatialBlend).keys.Select(k => k.time).ToArray(),
                    CurveValues = s.GetCustomCurve(AudioSourceCurveType.SpatialBlend).keys.Select(k => k.value).ToArray(),
                };
                store.Records.Add(r);
            }
            r.Mode = mode;
            Undo.RecordObject(s, "Listening Mode");
            s.spatialize = false;
            if (mode == ListeningModeStore.Mode.Flat) s.spatialBlend = 0f;
            else SetBlend(s, r);
            PrefabUtility.RecordPrefabInstancePropertyModifications(s);
        }
        store.Save();
    }

    static void Restore(List<ListeningModeStore.Record> records)
    {
        var store = ListeningModeStore.instance;
        foreach (var r in records)
        {
            var s = Resolve(r);
            if (s == null)
            {
                // Scene open but the source is gone (neither its id nor its path finds it): it was deleted while
                // listening, so there is nothing to restore.
                if (SceneOpen(r)) { Debug.Log($"[Listening Mode] {r.Label}: no longer exists, record removed."); store.Records.Remove(r); }
                else Debug.LogWarning($"[Listening Mode] {r.Label}: open its scene to restore it.");
                continue;
            }
            Undo.RecordObject(s, "Restore Listening Mode");
            SetBlend(s, r);
            s.spatialize = r.Spatialize;
            // Bring the instance's recorded overrides up to date first, or a stale one (e.g. saved by a script reload
            // while listening) is put back after the clean-up.
            PrefabUtility.RecordPrefabInstancePropertyModifications(s);
            DropMatchingOverrides(s);
            store.Records.Remove(r);
        }
        store.Save();
    }

    // The stored Spatial Blend: its whole curve if it had several keys, otherwise the single value.
    static void SetBlend(AudioSource s, ListeningModeStore.Record r)
    {
        if (r.CurveTimes != null && r.CurveTimes.Length > 1)
            s.SetCustomCurve(AudioSourceCurveType.SpatialBlend, new AnimationCurve(r.CurveTimes.Select((t, i) => new Keyframe(t, r.CurveValues[i])).ToArray()));
        else s.spatialBlend = r.SpatialBlend;
    }

    // Changing a prefab instance's source leaves an override even once the value is put back; remove it when the
    // value matches the prefab again, so later prefab changes still reach the instance.
    static void DropMatchingOverrides(AudioSource s)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(s)) return;
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource(s);
        if (prefab == null) return;
        var so = new SerializedObject(s);
        var spatialize = so.FindProperty("Spatialize");
        if (spatialize.prefabOverride && prefab.spatialize == s.spatialize)
            PrefabUtility.RevertPropertyOverride(spatialize, InteractionMode.UserAction);
        var blend = so.FindProperty("panLevelCustomCurve");
        if (blend.prefabOverride && CurvesMatch(prefab.GetCustomCurve(AudioSourceCurveType.SpatialBlend), s.GetCustomCurve(AudioSourceCurveType.SpatialBlend)))
            PrefabUtility.RevertPropertyOverride(blend, InteractionMode.UserAction);
    }

    static bool CurvesMatch(AnimationCurve a, AnimationCurve b) =>
        a.length == b.length && a.keys.Zip(b.keys, (x, y) => Mathf.Approximately(x.time, y.time) && Mathf.Approximately(x.value, y.value)).All(ok => ok);
}
