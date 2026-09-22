using UnityEngine;
using UnityEngine.Video;

public class VideoLightCookieURP : MonoBehaviour
{
    public Light spotLight;
    public VideoPlayer videoPlayer;
    public RenderTexture cookieRT;

    [Header("Editor Preview (does not affect Play Mode)")]
    [Tooltip("Keep the video cookie visible while editing, including after saving the scene.")]
    public bool editorPreview;
    [Tooltip("Optional still image. If empty, use the video asset's native preview frame.")]
    public Texture2D editorPreviewImage;
    [Tooltip("Additional players whose own Target Textures need an editor preview, e.g. a separate beam cookie. Each uses its own video preview frame.")]
    public VideoPlayer[] additionalEditorPreviewPlayers = new VideoPlayer[0];

    void Start()
    {
        videoPlayer.renderMode    = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = cookieRT;
        videoPlayer.isLooping     = true;
        videoPlayer.Play();
        spotLight.cookie = cookieRT;
    }

    void LateUpdate()
    {
        // 没有这行,URP 的 cookie atlas 会缓存,画面定格在第一帧
        cookieRT.IncrementUpdateCount();
    }
}
