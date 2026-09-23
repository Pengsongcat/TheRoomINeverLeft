using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Development aid for scrubbing Timelines in Edit mode. While not playing, the Audio Source is heard flat (2D,
// Steam Audio spatializer off), so a Timeline's audio is heard wherever the Scene camera or listener is, and a
// sphere marks the sound in the Scene view without selecting it. In Play mode and builds it does nothing.
//
// The Audio Source's own settings are never saved changed: they are put back before a scene or prefab is saved,
// before Play mode and whenever this component is disabled or removed.
[ExecuteAlways]
[RequireComponent(typeof(AudioSource))]
public class EditModeAudition : MonoBehaviour
{
    [SerializeField] Color _colour = new(1f, 0.6f, 0.1f, 0.6f);
    [Tooltip("Marker sphere across, metres.")]
    [SerializeField, Min(0.01f)] float _size = 0.15f;

#if UNITY_EDITOR
    AudioSource _source;
    bool  _flat;
    float _spatialBlend;
    bool  _spatialize;

    void OnEnable()
    {
        _source = GetComponent<AudioSource>();
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved  += OnSceneSaved;
        PrefabStage.prefabSaving       += OnPrefabSaving;
        PrefabStage.prefabSaved        += OnPrefabSaved;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        MakeFlat();
    }

    void OnDisable()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaved  -= OnSceneSaved;
        PrefabStage.prefabSaving       -= OnPrefabSaving;
        PrefabStage.prefabSaved        -= OnPrefabSaved;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Restore();
    }

    void MakeFlat()
    {
        if (_flat || Application.isPlaying || _source == null) return;
        _spatialBlend = _source.spatialBlend;
        _spatialize   = _source.spatialize;
        _source.spatialBlend = 0f;
        _source.spatialize   = false;
        _flat = true;
    }

    void Restore()
    {
        if (!_flat || _source == null) return;
        _source.spatialBlend = _spatialBlend;
        _source.spatialize   = _spatialize;
        _flat = false;
    }

    void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) { if (scene == gameObject.scene) Restore(); }
    void OnPrefabSaving(GameObject root) { if (transform.IsChildOf(root.transform)) Restore(); }
    void OnSceneSaved(UnityEngine.SceneManagement.Scene scene) => MakeFlat();
    void OnPrefabSaved(GameObject root) => MakeFlat();

    void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode) Restore();
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Gizmos.color = _colour;
        Gizmos.DrawSphere(transform.position, _size * 0.5f);
    }
#endif
}
