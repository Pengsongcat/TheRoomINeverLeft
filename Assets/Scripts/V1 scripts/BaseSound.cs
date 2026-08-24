using UnityEngine;
using System;
using System.Collections;

public class BaseSound : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("The AudioClip to play. If left empty, it will use the default Clip on the child AudioSource.")]
    public AudioClip audioClip;

    [Range(0f, 1f)]
    public float targetVolume = 1f;

    [Tooltip("Delay in seconds before the audio starts playing after FadeIn is called.")]
    public float initialWait = 0f;
    public float fadeInTime = 1f;
    public float fadeOutTime = 1f;

    [Header("Acoustic Curves")]
    [Tooltip("Curve defining the Fade In volume change over time. Evaluated from 0 to 1.")]
    public AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Curve defining the Fade Out volume change over time. Evaluated from 0 to 1.")]
    public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Space(10)]
    public bool isLooping = false;

    [Header("Debug Settings")]
    [Tooltip("If true, automatically calls FadeIn() when the GameObject starts.")]
    public bool playOnStart = false;
    public bool inactivateOnFadeOut = true;

    // C# native event: Triggered when the Fade Out process is completely finished
    public event Action OnFadeOutFinished;

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

    private AudioSource audioSource;
    private Coroutine fadeRoutine;
    private Coroutine autoFadeOutRoutine;

    private void Awake()
    {
        audioSource = GetComponentInChildren<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError($"[{gameObject.name}] AudioSource not found! Please ensure an AudioSource component is attached to a child object.");
            return;
        }

        if (audioClip != null)
        {
            audioSource.clip = audioClip;
        }

        audioSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        if (audioSource != null)
        {
            audioSource.loop = isLooping;
            audioSource.volume = 0f;
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            FadeIn();
        }
    }

    /// <summary>
    /// 对外接口：统一修改 AudioClip, Loop 状态 和 目标音量，并立刻播放
    /// </summary>
    public void PlayNewClip(AudioClip newClip, bool loop, float volume)
    {
        if (audioSource == null) return;

        this.audioClip = newClip;
        this.isLooping = loop;
        this.targetVolume = volume;

        audioSource.clip = newClip;
        audioSource.loop = loop;

        FadeIn();
    }

    /// <summary>
    /// 对外接口：触发完整的 Fade In
    /// </summary>
    public void FadeIn()
    {
        if (audioSource == null || audioSource.clip == null) return;

        // FadeIn 需要彻底重置所有状态，所以停止所有协程
        StopAllRoutines();
        fadeRoutine = StartCoroutine(FadeInSequence());
    }

    /// <summary>
    /// 对外接口：彻底淡出到 0 并停止播放
    /// </summary>
    public void FadeOut()
    {
        if (audioSource == null) return;

        // FadeOut 是终结指令，停止所有协程
        StopAllRoutines();
        fadeRoutine = StartCoroutine(DoFade(0f, fadeOutTime, useFadeOutCurve: true, stopAtEnd: true));
    }

    public void StopImmediate()
    {
        if (audioSource == null) return;
        StopAllRoutines();
        fadeRoutine = StartCoroutine(DoFade(0f, 0f, useFadeOutCurve: true, stopAtEnd: true));
    }

    /// <summary>
    /// 新增：专门提供给 Unity Inspector (UnityEvent) 拖拽使用的单参数版本
    /// </summary>
    public void FadeToVolume(float targetVol)
    {
        FadeToVolume(targetVol, -1f);
    }

    /// <summary>
    /// 新增接口：平滑过渡到任意指定音量（不中断播放，不破坏自动停止逻辑）
    /// </summary>
    /// <param name="targetVol">目标音量 (0~1)</param>
    /// <param name="customDuration">渐变时间，填负数则使用面板的 fadeOutTime/fadeInTime</param>
    public void FadeToVolume(float targetVol, float customDuration = -1f)
    {
        if (audioSource == null) return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        bool stopAtEnd = targetVol <= 0.001f;

        if (stopAtEnd && autoFadeOutRoutine != null)
        {
            StopCoroutine(autoFadeOutRoutine);
            autoFadeOutRoutine = null;
        }

        if (!stopAtEnd && !audioSource.isPlaying && !isLooping && autoFadeOutRoutine == null)
        {
            autoFadeOutRoutine = StartCoroutine(AutoFadeOutTimer());
        }

        bool useFadeOutCurve = targetVol < audioSource.volume;
        float duration = customDuration >= 0f ? customDuration : (useFadeOutCurve ? fadeOutTime : fadeInTime);

        fadeRoutine = StartCoroutine(DoFade(targetVol, duration, useFadeOutCurve, stopAtEnd));
    }

    /// <summary>
    /// 辅助方法：清理所有协程并置空引用
    /// </summary>
    private void StopAllRoutines()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        if (autoFadeOutRoutine != null)
        {
            StopCoroutine(autoFadeOutRoutine);
            autoFadeOutRoutine = null;
        }
    }

    private IEnumerator FadeInSequence()
    {
        if (initialWait > 0f)
        {
            yield return new WaitForSeconds(initialWait);
        }

        if (!isLooping)
        {
            autoFadeOutRoutine = StartCoroutine(AutoFadeOutTimer());
        }

        yield return StartCoroutine(DoFade(targetVolume, fadeInTime, useFadeOutCurve: false, stopAtEnd: false));
    }

    private IEnumerator DoFade(float endVolume, float duration, bool useFadeOutCurve, bool stopAtEnd)
    {
        if (!stopAtEnd && !audioSource.isPlaying && endVolume > 0f)
        {
            audioSource.Play();
        }

        float startVolume = audioSource.volume;
        float timeElapsed = 0f;

        if (duration > 0f)
        {
            while (timeElapsed < duration)
            {
                timeElapsed += Time.deltaTime;
                float normalizedTime = timeElapsed / duration;

                float curveValue = useFadeOutCurve
                    ? fadeOutCurve.Evaluate(normalizedTime)
                    : fadeInCurve.Evaluate(normalizedTime);

                float lerpT = useFadeOutCurve ? (1f - curveValue) : curveValue;

                audioSource.volume = Mathf.Lerp(startVolume, endVolume, lerpT);
                yield return null;
            }
        }

        audioSource.volume = endVolume;

        if (stopAtEnd)
        {
            audioSource.Stop();
            OnFadeOutFinished?.Invoke();

            if (inactivateOnFadeOut)
            {
                gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator AutoFadeOutTimer()
    {
        float waitTime = Mathf.Max(0f, audioSource.clip.length - fadeOutTime);
        yield return new WaitForSeconds(waitTime);
        FadeOut();
    }
}