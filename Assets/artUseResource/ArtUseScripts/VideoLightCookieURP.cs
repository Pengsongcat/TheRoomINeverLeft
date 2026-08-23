using UnityEngine;
using UnityEngine.Video;

public class VideoLightCookieURP : MonoBehaviour
{
    public Light spotLight;
    public VideoPlayer videoPlayer;
    public RenderTexture cookieRT;

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