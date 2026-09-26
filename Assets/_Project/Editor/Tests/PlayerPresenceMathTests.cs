using NUnit.Framework;

/// <summary>
/// Focused deterministic tests for the player's ambient presence math (emission/pulse). Purely
/// presentation-layer; no gameplay/fairness/pattern/progression system is exercised or changed.
/// </summary>
public sealed class PlayerPresenceMathTests
{
    [Test]
    public void ComputeEmissionBrightness_BaselinePaceIsNoBoost()
    {
        Assert.That(PlayerPresenceMath.ComputeEmissionBrightness(1f, 2f, 1.8f), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ComputeEmissionBrightness_ReachesAndNeverExceedsMaxBoost()
    {
        float atReference = PlayerPresenceMath.ComputeEmissionBrightness(2f, 2f, 1.8f);
        float beyondReference = PlayerPresenceMath.ComputeEmissionBrightness(10f, 2f, 1.8f);

        Assert.That(atReference, Is.EqualTo(1.8f).Within(0.0001f));
        Assert.That(beyondReference, Is.EqualTo(1.8f).Within(0.0001f));
    }

    [Test]
    public void ComputeEmissionBrightness_IsMonotonicNonDecreasingWithPace()
    {
        float previous = PlayerPresenceMath.ComputeEmissionBrightness(1f, 2f, 1.8f);

        for (float pace = 1.1f; pace <= 2f; pace += 0.1f)
        {
            float current = PlayerPresenceMath.ComputeEmissionBrightness(pace, 2f, 1.8f);
            Assert.That(current, Is.GreaterThanOrEqualTo(previous));
            previous = current;
        }
    }

    [Test]
    public void ComputePulseMultiplier_ReduceFlashingForcesNoOscillation()
    {
        Assert.That(PlayerPresenceMath.ComputePulseMultiplier(0.37f, 1.1f, 0.06f, true), Is.EqualTo(1f));
        Assert.That(PlayerPresenceMath.ComputePulseMultiplier(1.9f, 1.1f, 0.06f, true), Is.EqualTo(1f));
    }

    [Test]
    public void ComputePulseMultiplier_StaysWithinBoundedAmplitude()
    {
        for (float t = 0f; t < 5f; t += 0.13f)
        {
            float pulse = PlayerPresenceMath.ComputePulseMultiplier(t, 1.1f, 0.06f, false);
            Assert.That(pulse, Is.GreaterThanOrEqualTo(1f - 0.06f - 0.0001f));
            Assert.That(pulse, Is.LessThanOrEqualTo(1f + 0.06f + 0.0001f));
        }
    }
}
