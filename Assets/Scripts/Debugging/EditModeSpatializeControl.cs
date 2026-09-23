using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Scene-wide audio preview. Put one instance in a scene that needs Timeline scrubbing.
/// Disable Enable In Edit Mode before editing a source's intended runtime settings.
/// The supplied prefab is tagged EditorOnly, and all behaviour below is editor-only.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("TRINL/Debugging/Edit Mode Spatialize Control")]
public sealed class EditModeSpatializeControl : MonoBehaviour
{
    [Tooltip("Temporarily override audio in this scene only. Uncheck to restore and edit runtime settings.")]
    [SerializeField] bool _enableInEditMode = true;

    [Tooltip("Spatial Blend used for editing: 0 = 2D, 1 = 3D. Original values are restored for Play Mode and saves.")]
    [SerializeField, Range(0f, 1f)] float _editModeSpatialBlend = 0f;

#if UNITY_EDITOR
    // Never serialize the temporary preview or record it as a prefab override.
    sealed class Original
    {
        readonly bool spatialize;
        readonly float blend;
        readonly AnimationCurve blendCurve;

        public Original(AudioSource source)
        {
            spatialize = source.spatialize;
            blend = source.spatialBlend;
            // Preserve the whole curve as well as the value (Timeline sources may have custom curves).
            blendCurve = source.GetCustomCurve(AudioSourceCurveType.SpatialBlend);
        }

        public void Restore(AudioSource source)
        {
            if (!source) return;
            source.spatialize = spatialize;
            source.spatialBlend = blend;
            if (blendCurve != null)
                source.SetCustomCurve(AudioSourceCurveType.SpatialBlend, blendCurve);
        }
    }

    static readonly Dictionary<int, EditModeSpatializeControl> Owners = new();
    readonly Dictionary<AudioSource, Original> _originals = new();
    readonly List<AudioSource> _sources = new();
    readonly List<AudioSource> _removed = new();
    bool _suspended;
    bool _announced;
    bool _duplicateWarning;
    int _ownedScene;
    int _lastCount;
    float _lastBlend;
    double _nextRefresh;

    void OnEnable()
    {
        _suspended = EditorApplication.isPlayingOrWillChangePlaymode;
        EditorApplication.update += Refresh;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        PrefabUtility.prefabInstanceApplying += OnPrefabApplying;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += BeforeReload;
        _nextRefresh = 0;
    }

    void OnDisable()
    {
        EditorApplication.update -= Refresh;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorSceneManager.sceneClosing -= OnSceneClosing;
        PrefabUtility.prefabInstanceApplying -= OnPrefabApplying;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        EditorApplication.quitting -= BeforeReload;
        if (!EditorApplication.isPlaying) Restore("control disabled", true);
        else _originals.Clear();
        ReleaseScene();
    }

    // OnValidate can run off the main thread. The next editor update performs the actual work.
    void OnValidate() => _nextRefresh = 0;

    void Refresh()
    {
        if (_suspended || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;
        if (EditorApplication.timeSinceStartup < _nextRefresh) return;
        _nextRefresh = EditorApplication.timeSinceStartup + 0.5;

        Scene scene = gameObject.scene;
        if (!_enableInEditMode || !isActiveAndEnabled || !scene.IsValid() || !scene.isLoaded
            || EditorUtility.IsPersistent(this) || EditorSceneManager.IsPreviewScene(scene)
            || PrefabStageUtility.GetPrefabStage(gameObject) != null)
        {
            Restore("edit preview disabled", true);
            ReleaseScene();
            return;
        }

        if (Owners.TryGetValue(scene.handle, out var owner) && owner && owner != this)
        {
            if (!_duplicateWarning)
                Debug.LogWarning($"[Edit Mode Audio] {scene.name}: another EditModeSpatializeControl already owns this scene.", this);
            _duplicateWarning = true;
            return;
        }
        if (_ownedScene != scene.handle) ReleaseScene();
        Owners[scene.handle] = this;
        _ownedScene = scene.handle;
        _duplicateWarning = false;

        _sources.Clear();
        foreach (var root in scene.GetRootGameObjects())
            _sources.AddRange(root.GetComponentsInChildren<AudioSource>(true));

        // Restore a source moved out of this scene, and discard destroyed sources.
        _removed.Clear();
        foreach (var entry in _originals)
            if (!entry.Key || entry.Key.gameObject.scene != scene)
            {
                entry.Value.Restore(entry.Key);
                _removed.Add(entry.Key);
            }
        foreach (var source in _removed) _originals.Remove(source);

        float previewBlend = Mathf.Clamp01(_editModeSpatialBlend);
        foreach (var source in _sources)
        {
            if (!_originals.ContainsKey(source)) _originals.Add(source, new Original(source));
            if (source.spatialize) source.spatialize = false;
            if (source.spatialBlend != previewBlend) source.spatialBlend = previewBlend;
        }

        if (!_announced || _lastCount != _sources.Count || _lastBlend != previewBlend)
            Debug.Log($"[Edit Mode Audio] {scene.name}: preview applied to {_sources.Count} AudioSources "
                + $"(Spatialize off, Spatial Blend {previewBlend:0.##}).", this);
        _announced = true;
        _lastCount = _sources.Count;
        _lastBlend = previewBlend;
    }

    void Restore(string reason, bool forget = false)
    {
        foreach (var entry in _originals) entry.Value.Restore(entry.Key);
        if (reason != null && _announced)
        {
            Debug.Log($"[Edit Mode Audio] {gameObject.scene.name}: restored {_originals.Count} AudioSources ({reason}).", this);
            _announced = false;
        }
        if (forget) _originals.Clear();
    }

    void ReleaseScene()
    {
        if (Owners.TryGetValue(_ownedScene, out var owner) && owner == this) Owners.Remove(_ownedScene);
        _ownedScene = 0;
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Restore BEFORE Unity copies the scene for Play Mode / invokes runtime Awake.
            _suspended = true;
            Restore("entering Play Mode");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // This callback also runs when domain reload and scene reload are disabled.
            _suspended = false;
            _nextRefresh = 0;
            Refresh();
        }
    }

    void OnSceneSaving(Scene scene, string path)
    {
        if (scene == gameObject.scene && !EditorApplication.isPlaying) Restore(null);
    }

    void OnSceneSaved(Scene scene)
    {
        if (scene == gameObject.scene) _nextRefresh = 0;
    }

    void OnSceneClosing(Scene scene, bool removingScene)
    {
        if (scene != gameObject.scene) return;
        _suspended = true;
        if (!EditorApplication.isPlaying) Restore("scene closing", true);
        ReleaseScene();
    }

    void OnPrefabApplying(GameObject instance)
    {
        if (instance.scene != gameObject.scene || EditorApplication.isPlaying) return;
        Restore(null);
        _nextRefresh = 0;
    }

    void BeforeReload()
    {
        _suspended = true;
        // The original values survive recompilation because they are restored before state is lost.
        if (!EditorApplication.isPlaying) Restore("editor reload/close", true);
    }

    // Build from intended settings even when the currently open scene is being previewed.
    public sealed class RestoreBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;
        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (var owner in Owners.Values)
                if (owner) owner.Restore("building player");
        }
    }
#endif
}
