using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

// TRINL > Save Video Frame as PNG: saves one frame of a Video Clip as a texture next to the clip, e.g. a still
// leaf image for Audio Leaf Cookie. Pick the clip (or select it / a Video Player first) and the time, then Save.
// The frame is decoded by a hidden Video Player in Edit mode.
public class VideoFrameSaver : EditorWindow
{
    const double TimeoutSeconds = 10;

    VideoClip _clip;
    float     _time;
    string    _status = "";

    VideoPlayer _player;
    double      _startedAt;

    [MenuItem("TRINL/Save Video Frame as PNG")]
    static void Open()
    {
        var window = GetWindow<VideoFrameSaver>("Save Video Frame");
        window._clip = Selection.activeObject as VideoClip;
        if (window._clip == null && Selection.activeGameObject != null &&
            Selection.activeGameObject.TryGetComponent<VideoPlayer>(out var player))
            window._clip = player.clip;
    }

    void OnGUI()
    {
        _clip = (VideoClip)EditorGUILayout.ObjectField("Video Clip", _clip, typeof(VideoClip), false);
        float length = _clip != null ? (float)_clip.length : 0f;
        _time = EditorGUILayout.Slider("Time (s)", _time, 0f, Mathf.Max(length - 0.05f, 0f));

        using (new EditorGUI.DisabledScope(_clip == null || _player != null))
            if (GUILayout.Button("Save")) Begin();

        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
    }

    void Begin()
    {
        var go = new GameObject("Video Frame Saver") { hideFlags = HideFlags.HideAndDontSave };
        _player = go.AddComponent<VideoPlayer>();
        _player.playOnAwake         = false;
        _player.clip                = _clip;
        _player.renderMode          = VideoRenderMode.APIOnly;
        _player.audioOutputMode     = VideoAudioOutputMode.None;
        _player.skipOnDrop          = false;
        _player.sendFrameReadyEvents = true;
        _player.prepareCompleted += p => { p.time = _time; p.Play(); };
        _player.frameReady += OnFrameReady;
        _player.Prepare();

        _startedAt = EditorApplication.timeSinceStartup;
        _status = "Decoding…";
        EditorApplication.update += Pump;
    }

    // Video Players only advance in Edit mode while the player loop is asked to run.
    void Pump()
    {
        if (_player == null) { EditorApplication.update -= Pump; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - _startedAt > TimeoutSeconds) Finish("Timed out decoding the frame.");
    }

    void OnFrameReady(VideoPlayer player, long frame)
    {
        if (player != _player || player.time + 0.001 < _time) return; // stale, or still before the chosen time
        player.Pause();

        var source = player.texture;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32,
                                            RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        string clipPath = AssetDatabase.GetAssetPath(_clip);
        string path = Path.Combine(Path.GetDirectoryName(clipPath) ?? "Assets",
                                   $"{_clip.name} Frame {player.time:0.00}s.png").Replace('\\', '/');
        File.WriteAllBytes(path, image.EncodeToPNG());
        DestroyImmediate(image);

        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.wrapMode      = TextureWrapMode.Clamp; // a cookie must not repeat at its edges
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        Finish($"Saved {path}");
    }

    void Finish(string status)
    {
        EditorApplication.update -= Pump;
        if (_player != null)
        {
            var go = _player.gameObject; // not destroyed inside its own frameReady callback
            EditorApplication.delayCall += () => { if (go != null) DestroyImmediate(go); };
        }
        _player = null;
        _status = status;
        Repaint();
    }

    void OnDisable() => Finish("");
}
