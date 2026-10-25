using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

// Controls the looping guide sound; the chapter decides when to fade it.
[RequireComponent(typeof(AudioSource))]
public sealed class GuideSoundFader : MonoBehaviour
{
    [SerializeField, Min(0f)] float _fadeInDuration = 1f;
    [Tooltip("Fade-out time for this guide when its own trigger is activated.")]
    [SerializeField, Min(0f)] float _triggeredFadeOutDuration = 4f;
    [Tooltip("Fade-out time for this guide when a different guide is triggered.")]
    [FormerlySerializedAs("_fadeOutDuration")]
    [SerializeField, Min(0f)] float _untriggeredFadeOutDuration = 2f;
    [SerializeField] AnimationCurve _fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve _fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [Tooltip("Optional. When set, the guide plays through Wwise and the AudioSource stays silent; " +
             "the fades scale this object's Wwise output (the mix level lives in Wwise).")]
    [SerializeField] AK.Wwise.Event _wwiseEvent;

    AudioSource _source;
    Coroutine _fade;
    float _fullVolume;
    Action _onFadeOutComplete;
    bool _usesWwise;
    uint _wwisePlayingId;
    float _wwiseVolume = 1f;

    void Awake() => EnsureInitialised();

    void EnsureInitialised()
    {
        if (_source != null) return;
        _source = GetComponent<AudioSource>();
        _usesWwise = _wwiseEvent != null && _wwiseEvent.IsValid();
        _fullVolume = _usesWwise ? 1f : _source.volume;
    }

    bool IsPlaying => _usesWwise ? _wwisePlayingId != AkUnitySoundEngine.AK_INVALID_PLAYING_ID : _source.isPlaying;

    float Volume
    {
        get => _usesWwise ? _wwiseVolume : _source.volume;
        set
        {
            if (!_usesWwise) { _source.volume = value; return; }
            _wwiseVolume = value;
            foreach (var listener in AkAudioListener.DefaultListeners.ListenerList)
                AkUnitySoundEngine.SetGameObjectOutputBusVolume(gameObject, listener.gameObject, value);
        }
    }

    void Play()
    {
        if (!_usesWwise) { _source.Play(); return; }
        _wwisePlayingId = _wwiseEvent.Post(gameObject, (uint)AkCallbackType.AK_EndOfEvent, OnWwiseEventEnd);
    }

    void Stop()
    {
        if (!_usesWwise) { _source.Stop(); return; }
        if (IsPlaying) _wwiseEvent.Stop(gameObject);
        _wwisePlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    }

    // Ignores the end of an earlier playback that was already stopped and replayed.
    void OnWwiseEventEnd(object cookie, AkCallbackType type, AkCallbackInfo info)
    {
        if (info is AkEventCallbackInfo e && e.playingID == _wwisePlayingId)
            _wwisePlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    }

    public void FadeIn()
    {
        EnsureInitialised();
        CancelFade();
        if (!IsPlaying)
        {
            Volume = 0f;
            Play();
        }
        _fade = StartCoroutine(FadeTo(_fullVolume, _fadeInDuration, false));
    }

    public void FadeOut(bool wasTriggered, Action onComplete = null)
    {
        EnsureInitialised();
        CancelFade();
        _onFadeOutComplete = onComplete;
        if (!IsPlaying)
        {
            FinishFadeOut();
            return;
        }
        float duration = wasTriggered ? _triggeredFadeOutDuration : _untriggeredFadeOutDuration;
        _fade = StartCoroutine(FadeTo(0f, Mathf.Max(0f, duration), true));
    }

    public void StopImmediate()
    {
        EnsureInitialised();
        CancelFade();
        Stop();
        Volume = _fullVolume;
    }

    void OnDisable() { if (_source != null) StopImmediate(); }

    void CancelFade()
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = null;
        _onFadeOutComplete = null;
    }

    IEnumerator FadeTo(float target, float duration, bool stopAtEnd)
    {
        float start = Volume;
        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float blend = stopAtEnd ? 1f - _fadeOutCurve.Evaluate(progress)
                                        : _fadeInCurve.Evaluate(progress);
                Volume = Mathf.Lerp(start, target, blend);
                yield return null;
            }
        }
        Volume = target;
        _fade = null;
        if (stopAtEnd) FinishFadeOut();
    }

    void FinishFadeOut()
    {
        Stop();
        var callback = _onFadeOutComplete;
        _onFadeOutComplete = null;
        callback?.Invoke();
    }
}
