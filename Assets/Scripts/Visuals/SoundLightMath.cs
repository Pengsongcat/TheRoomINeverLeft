using UnityEngine;

// Pure math behind SoundLight: loudness, spectral flux, attack/release smoothing and the "heard" distance factor.
// No Unity objects, so it is covered by editor tests.
public static class SoundLightMath
{
    // Root mean square of an audio buffer (0 = silence, ~0.707 = full-scale sine).
    public static float Rms(float[] samples)
    {
        if (samples == null || samples.Length == 0) return 0f;
        double sum = 0;
        foreach (var s in samples) sum += s * s;
        return (float)System.Math.Sqrt(sum / samples.Length);
    }

    // How much the spectrum rose since the previous frame: sum of positive bin differences.
    // Band weight goes linearly from lowWeight (bin 0) to highWeight (last bin), e.g. to favour mids/highs.
    // previous is updated to current, so the caller keeps one buffer per source.
    public static float SpectralFlux(float[] current, float[] previous, float lowWeight = 1f, float highWeight = 1f)
    {
        if (current == null || previous == null) return 0f;
        int n = Mathf.Min(current.Length, previous.Length);
        float flux = 0f;
        for (int i = 0; i < n; i++)
        {
            float rise = current[i] - previous[i];
            if (rise > 0f)
            {
                float w = n > 1 ? Mathf.Lerp(lowWeight, highWeight, i / (float)(n - 1)) : lowWeight;
                flux += rise * w;
            }
            previous[i] = current[i];
        }
        return flux;
    }

    // Brightness before smoothing, 0..maxIntensity.
    public static float Target(float rms, float flux, float gain, float fluxWeight, float heard, float maxIntensity) =>
        maxIntensity * Mathf.Clamp01(gain * rms + fluxWeight * flux) * Mathf.Clamp01(heard);

    // Exponential approach with separate time constants for rising (attack) and falling (release), in seconds.
    public static float Smooth(float current, float target, float attackSeconds, float releaseSeconds, float deltaTime)
    {
        float tau = target > current ? attackSeconds : releaseSeconds;
        if (tau <= 0f) return target;
        float k = 1f - Mathf.Exp(-deltaTime / tau);
        return current + (target - current) * k;
    }

    // Unity's built-in rolloffs, matching AudioRolloffMode.Linear / Logarithmic.
    public static float LinearRolloff(float distance, float minDistance, float maxDistance)
    {
        if (distance <= minDistance) return 1f;
        if (maxDistance <= minDistance) return 0f;
        return Mathf.Clamp01(1f - (distance - minDistance) / (maxDistance - minDistance));
    }

    public static float LogarithmicRolloff(float distance, float minDistance) =>
        distance <= minDistance ? 1f : minDistance / distance;

    // Point light range that lights a floor circle of poolRadius from height above the floor.
    public static float RangeForPool(float poolRadius, float height) =>
        Mathf.Sqrt(poolRadius * poolRadius + height * height);

    // A sound's own falloff blended by its spatial blend (0 = 2D, heard everywhere at full volume).
    public static float Heard(float rolloff, float spatialBlend) =>
        Mathf.Lerp(1f, Mathf.Clamp01(rolloff), Mathf.Clamp01(spatialBlend));
}
