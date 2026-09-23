using UnityEngine;

// Where an audio-reactive component takes its loudness from.
//   Source: one AudioSource's output.   Headset: the AudioListener's whole mix, everything the visitor hears.
public enum ListenTo { Source, Headset }

// The loudness behind Audio Flicker Light, Audio Reactive Fog and Audio Leaf Cookie: 0..1 from the sound's volume
// (RMS × gain) + spectral flux (short spikes when the spectrum rises), smoothed with attack / release.
// Each component keeps one meter and passes its own inspector settings every frame.
// A source is read whether or not isPlaying is set (audio played by a Timeline Audio Track does not set it).
public sealed class LoudnessMeter
{
    const int Samples = 256;

    readonly float[] _samples      = new float[Samples];
    readonly float[] _spectrum     = new float[Samples];
    readonly float[] _prevSpectrum = new float[Samples];

    float _level;  // smoothed, 0..1
    bool  _primed; // _prevSpectrum holds a real frame

    public float Level => _level;

    public void Reset()
    {
        _level = 0f;
        _primed = false;
    }

    public float Update(ListenTo listenTo, AudioSource source, float gain, float fluxWeight, float lowBandWeight,
                        float highBandWeight, float attackSeconds, float releaseSeconds, float deltaTime)
    {
        float target = Raw(listenTo, source, gain, fluxWeight, lowBandWeight, highBandWeight);
        _level = SoundLightMath.Smooth(_level, target, attackSeconds, releaseSeconds, deltaTime);
        return _level;
    }

    // 0..1 loudness this frame, unsmoothed; silence or a missing / disabled source reads as 0.
    float Raw(ListenTo listenTo, AudioSource source, float gain, float fluxWeight, float lowBandWeight,
              float highBandWeight)
    {
        if (listenTo == ListenTo.Headset)
        {
            AudioListener.GetOutputData(_samples, 0);
            AudioListener.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        }
        else
        {
            if (source == null || !source.isActiveAndEnabled) return 0f;
            source.GetOutputData(_samples, 0);
            source.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        }
        if (!_primed) System.Array.Copy(_spectrum, _prevSpectrum, Samples); // no flux spike on the first frame
        _primed = true;

        float rms  = SoundLightMath.Rms(_samples);
        float flux = SoundLightMath.SpectralFlux(_spectrum, _prevSpectrum, lowBandWeight, highBandWeight);
        return SoundLightMath.Target(rms, flux, gain, fluxWeight, heard: 1f, maxIntensity: 1f);
    }

    // A source's own falloff at the AudioListener: its rolloff at its current (room-scaled) min / max distance,
    // 1 up close, 0 at and beyond max distance (checked explicitly, since the Logarithmic curve never reaches 0 on
    // its own). customRolloff is the source's Custom Rolloff curve, cached by the caller (GetCustomCurve allocates).
    // The listener is found once and kept in the caller's field.
    public static float DistanceLevel(AudioSource source, AnimationCurve customRolloff, ref AudioListener listener)
    {
        if (source == null) return 0f;
        if (listener == null) listener = Object.FindFirstObjectByType<AudioListener>();
        if (listener == null) return 0f;

        float distance = Vector3.Distance(source.transform.position, listener.transform.position);
        float rolloff = distance >= source.maxDistance ? 0f : source.rolloffMode switch
        {
            AudioRolloffMode.Custom => customRolloff != null && source.maxDistance > 0f
                                       ? customRolloff.Evaluate(distance / source.maxDistance) : 1f,
            AudioRolloffMode.Linear => SoundLightMath.LinearRolloff(distance, source.minDistance, source.maxDistance),
            _                       => SoundLightMath.LogarithmicRolloff(distance, source.minDistance),
        };
        return SoundLightMath.Heard(rolloff, source.spatialBlend);
    }

    public static AnimationCurve CustomRolloff(AudioSource source) =>
        source != null && source.rolloffMode == AudioRolloffMode.Custom
            ? source.GetCustomCurve(AudioSourceCurveType.CustomRolloff) : null;
}
