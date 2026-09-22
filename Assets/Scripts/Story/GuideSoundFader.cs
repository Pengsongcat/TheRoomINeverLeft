using System;
using System.Collections;
using UnityEngine;

// Controls the looping guide sound; the chapter decides when to fade it.
[RequireComponent(typeof(AudioSource))]
public sealed class GuideSoundFader : MonoBehaviour
{
    [SerializeField, Min(0f)] float _fadeInDuration = 1f;
    [SerializeField, Min(0f)] float _fadeOutDuration = 1f;
    [SerializeField] AnimationCurve _fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve _fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    AudioSource _source;
    Coroutine _fade;
    float _fullVolume;
    Action _onFadeOutComplete;

    void Awake() => EnsureInitialised();

    void EnsureInitialised()
    {
        if (_source != null) return;
        _source = GetComponent<AudioSource>();
        _fullVolume = _source.volume;
    }

    public void FadeIn()
    {
        EnsureInitialised();
        CancelFade();
        if (!_source.isPlaying)
        {
            _source.volume = 0f;
            _source.Play();
        }
        _fade = StartCoroutine(FadeTo(_fullVolume, _fadeInDuration, false));
    }

    public void FadeOut(float durationMultiplier = 1f, Action onComplete = null)
    {
        EnsureInitialised();
        CancelFade();
        _onFadeOutComplete = onComplete;
        if (!_source.isPlaying)
        {
            FinishFadeOut();
            return;
        }
        _fade = StartCoroutine(FadeTo(0f, _fadeOutDuration * Mathf.Max(0f, durationMultiplier), true));
    }

    public void StopImmediate()
    {
        EnsureInitialised();
        CancelFade();
        _source.Stop();
        _source.volume = _fullVolume;
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
        float start = _source.volume;
        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float blend = stopAtEnd ? 1f - _fadeOutCurve.Evaluate(progress)
                                        : _fadeInCurve.Evaluate(progress);
                _source.volume = Mathf.Lerp(start, target, blend);
                yield return null;
            }
        }
        _source.volume = target;
        _fade = null;
        if (stopAtEnd) FinishFadeOut();
    }

    void FinishFadeOut()
    {
        _source.Stop();
        var callback = _onFadeOutComplete;
        _onFadeOutComplete = null;
        callback?.Invoke();
    }
}
