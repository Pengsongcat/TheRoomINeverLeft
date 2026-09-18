using NUnit.Framework;
using UnityEngine;

public class SoundLightMathTests
{
    const float Eps = 1e-4f;

    [Test]
    public void Rms_Silence_IsZero() =>
        Assert.AreEqual(0f, SoundLightMath.Rms(new float[256]), Eps);

    [Test]
    public void Rms_ConstantSignal_IsItsMagnitude()
    {
        var s = new float[256];
        for (int i = 0; i < s.Length; i++) s[i] = i % 2 == 0 ? 0.5f : -0.5f;
        Assert.AreEqual(0.5f, SoundLightMath.Rms(s), Eps);
    }

    [Test]
    public void SpectralFlux_CountsOnlyRises_AndUpdatesPrevious()
    {
        var prev = new[] { 0.2f, 0.5f, 0.1f };
        var cur  = new[] { 0.4f, 0.1f, 0.1f };
        Assert.AreEqual(0.2f, SoundLightMath.SpectralFlux(cur, prev), Eps);
        CollectionAssert.AreEqual(cur, prev);
        Assert.AreEqual(0f, SoundLightMath.SpectralFlux(cur, prev), Eps); // same spectrum again: no flux
    }

    [Test]
    public void SpectralFlux_BandWeights_RampFromLowToHigh()
    {
        var prev = new float[3];
        var cur  = new[] { 1f, 1f, 1f };
        // weights 0, 1, 2 → flux 3
        Assert.AreEqual(3f, SoundLightMath.SpectralFlux(cur, prev, lowWeight: 0f, highWeight: 2f), Eps);
    }

    [Test]
    public void Target_IsClampedToMaxIntensity_AndScaledByHeard()
    {
        Assert.AreEqual(3f,   SoundLightMath.Target(rms: 1f, flux: 1f, gain: 4f, fluxWeight: 2f, heard: 1f,   maxIntensity: 3f), Eps);
        Assert.AreEqual(1.5f, SoundLightMath.Target(rms: 1f, flux: 1f, gain: 4f, fluxWeight: 2f, heard: 0.5f, maxIntensity: 3f), Eps);
        Assert.AreEqual(0f,   SoundLightMath.Target(rms: 1f, flux: 1f, gain: 4f, fluxWeight: 2f, heard: 0f,   maxIntensity: 3f), Eps);
    }

    [Test]
    public void Smooth_AttackIsFasterThanRelease()
    {
        float up   = SoundLightMath.Smooth(0f, 1f, attackSeconds: 0.02f, releaseSeconds: 0.15f, deltaTime: 1f / 72f);
        float down = 1f - SoundLightMath.Smooth(1f, 0f, attackSeconds: 0.02f, releaseSeconds: 0.15f, deltaTime: 1f / 72f);
        Assert.Greater(up, down);
        Assert.Greater(up, 0f);
        Assert.Less(up, 1f);
    }

    [Test]
    public void Smooth_OneTimeConstant_Reaches63Percent() =>
        Assert.AreEqual(1f - Mathf.Exp(-1f), SoundLightMath.Smooth(0f, 1f, 0.1f, 0.1f, 0.1f), Eps);

    [Test]
    public void Smooth_ZeroTimeConstant_JumpsToTarget() =>
        Assert.AreEqual(0.7f, SoundLightMath.Smooth(0f, 0.7f, 0f, 0f, 0.01f), Eps);

    [Test]
    public void LinearRolloff_FullInsideMin_ZeroAtMax()
    {
        Assert.AreEqual(1f,   SoundLightMath.LinearRolloff(0.5f, 1f, 5f), Eps);
        Assert.AreEqual(0.5f, SoundLightMath.LinearRolloff(3f,   1f, 5f), Eps);
        Assert.AreEqual(0f,   SoundLightMath.LinearRolloff(9f,   1f, 5f), Eps);
    }

    [Test]
    public void LogarithmicRolloff_IsMinOverDistance()
    {
        Assert.AreEqual(1f,    SoundLightMath.LogarithmicRolloff(0.5f, 1f), Eps);
        Assert.AreEqual(0.25f, SoundLightMath.LogarithmicRolloff(4f,   1f), Eps);
    }

    [Test]
    public void RangeForPool_ReachesPoolEdgeFromHeight() =>
        Assert.AreEqual(5f, SoundLightMath.RangeForPool(poolRadius: 4f, height: 3f), Eps);

    [Test]
    public void Heard_2DSoundIgnoresRolloff()
    {
        Assert.AreEqual(1f,   SoundLightMath.Heard(rolloff: 0f, spatialBlend: 0f), Eps);
        Assert.AreEqual(0f,   SoundLightMath.Heard(rolloff: 0f, spatialBlend: 1f), Eps);
        Assert.AreEqual(0.6f, SoundLightMath.Heard(rolloff: 0.2f, spatialBlend: 0.5f), Eps);
    }
}
