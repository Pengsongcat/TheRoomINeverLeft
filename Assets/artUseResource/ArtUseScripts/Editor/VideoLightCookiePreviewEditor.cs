using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

// RenderTexture pixels are transient. Refill the editor cookie from a stable still image.
// Excluded from builds; does not change the runtime VideoPlayer.
[InitializeOnLoad]
public static class VideoLightCookiePreviewEditor
{
    static double nextUpdate;
    static readonly Dictionary<int, Texture> frames = new Dictionary<int, Texture>();

    static VideoLightCookiePreviewEditor()
    {
        EditorApplication.update += Update;
        EditorApplication.projectChanged += Refresh;
        EditorSceneManager.sceneSaved += _ => Refresh();
        EditorApplication.playModeStateChanged += _ => Refresh();
    }

    public static void Refresh()
    {
        frames.Clear();
        nextUpdate = 0;
    }

    static Texture GetFrame(VideoLightCookieURP owner)
    {
        if (owner.editorPreviewImage) return owner.editorPreviewImage;
        var source = owner.videoPlayer ? owner.videoPlayer : owner.GetComponent<VideoPlayer>();
        if (!source || source.source != VideoSource.VideoClip || !source.clip) return null;
        int id = source.clip.GetInstanceID();
        if (frames.TryGetValue(id, out var frame) && frame) return frame;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source.clip)) as VideoClipImporter;
        if (!importer) return null;
        frame = importer.GetPreviewTexture();
        if (frame) frames[id] = frame;
        return frame;
    }

    public static string Status(VideoLightCookieURP owner)
    {
        if (!owner || !owner.editorPreview) return "Preview disabled.";
        if (!owner.cookieRT) return "Assign Cookie RT to preview.";
        return GetFrame(owner) ? "Static preview ready. Automatically restored after saving." :
            "No video preview available. Assign an Editor Preview Image (required for URL videos).";
    }

    static void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup < nextUpdate) return;
        nextUpdate = EditorApplication.timeSinceStartup + 0.25;
        bool changed = false;
        foreach (var owner in Object.FindObjectsByType<VideoLightCookieURP>(FindObjectsSortMode.None))
        {
            if (!owner.isActiveAndEnabled || !owner.editorPreview || !owner.cookieRT || EditorUtility.IsPersistent(owner)) continue;
            var frame = GetFrame(owner);
            if (!frame) continue;
            var previous = RenderTexture.active;
            try
            {
                if (!owner.cookieRT.IsCreated()) owner.cookieRT.Create();
                Graphics.Blit(frame, owner.cookieRT);
                owner.cookieRT.IncrementUpdateCount();
                changed = true;
            }
            finally { RenderTexture.active = previous; }
        }
        if (changed) SceneView.RepaintAll();
    }
}

[CustomEditor(typeof(VideoLightCookieURP)), CanEditMultipleObjects]
public class VideoLightCookieURPInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (Application.isPlaying) return;
        EditorGUILayout.HelpBox("编辑器静态预览：勾选 Editor Preview，保存场景后自动恢复画面。\n默认使用视频资源预览帧；也可指定 Editor Preview Image。进入 Play Mode 后交回视频播放器。", MessageType.Info);
        if (targets.Length == 1) EditorGUILayout.HelpBox(VideoLightCookiePreviewEditor.Status((VideoLightCookieURP)target), MessageType.None);
        if (GUILayout.Button("Refresh Preview / 刷新预览")) VideoLightCookiePreviewEditor.Refresh();
    }
}
