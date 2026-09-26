using NUnit.Framework;

/// <summary>
/// Focused deterministic tests for the depth-range scaling fix (Phase K-7): keeps the player's
/// reachable depth range proportional to GameController.GetDepthVariation(), the same curve
/// ObstacleSpawner already applies to its depth-spawn offsets. Purely presentation/positional
/// math -- no fairness/pattern/spawn-timing/lane-distribution system is exercised or changed.
/// </summary>
public sealed class DepthRangeMathTests
{
    [Test]
    public void ComputeEffectiveSafeDepthRange_FullVariationIsUnchanged()
    {
        Assert.That(DepthRangeMath.ComputeEffectiveSafeDepthRange(2f, 1f), Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void ComputeEffectiveSafeDepthRange_ScalesDownWithStartingVariation()
    {
        float effective = DepthRangeMath.ComputeEffectiveSafeDepthRange(2f, 0.65f);

        Assert.That(effective, Is.EqualTo(1.3f).Within(0.0001f));
        Assert.That(effective, Is.LessThan(2f));
    }

    [Test]
    public void ComputeEffectiveSafeDepthRange_IsMonotonicNonDecreasingWithVariation()
    {
        float previous = DepthRangeMath.ComputeEffectiveSafeDepthRange(2f, 0f);

        for (float variation = 0.1f; variation <= 1f; variation += 0.1f)
        {
            float current = DepthRangeMath.ComputeEffectiveSafeDepthRange(2f, variation);
            Assert.That(current, Is.GreaterThanOrEqualTo(previous));
            previous = current;
        }
    }

    [Test]
    public void ComputeEffectiveSafeDepthRange_ClampsOutOfRangeVariation()
    {
        Assert.That(DepthRangeMath.ComputeEffectiveSafeDepthRange(2f, -5f), Is.EqualTo(0f));
        Assert.That(DepthRangeMath.ComputeEffectiveSafeDepthRange(2f, 5f), Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void ComputeEffectiveSafeDepthRange_NeverNegativeForNegativeBaseRange()
    {
        Assert.That(DepthRangeMath.ComputeEffectiveSafeDepthRange(-1f, 1f), Is.EqualTo(0f));
    }

    [Test]
    public void ComputeEffectiveSafeDepthRange_ClosesTheDepthExtremeExploitWindow()
    {
        // Real production constants (TrackLayoutConfig.asset / GameBalanceConfig.asset / both
        // colliders 1x1x1): at run start (variation=0.65) the OLD fixed range (2.0) overshot the
        // compressed obstacle-slot spread (1.5 * 0.65 = 0.975) by more than the 1.0 collision
        // margin -- 2.0 - 0.975 = 1.025 >= 1.0, i.e. the depth extreme was fully unreachable by
        // any obstacle. The fix must close that gap to below the 1.0 margin.
        const float baseSafeDepthRange = 2f;
        const float startingDepthVariation = 0.65f;
        const float outerObstacleOffset = 1.5f;

        float effectiveRange = DepthRangeMath.ComputeEffectiveSafeDepthRange(baseSafeDepthRange, startingDepthVariation);
        float outerObstacleReach = outerObstacleOffset * startingDepthVariation;
        float gap = effectiveRange - outerObstacleReach;

        Assert.That(gap, Is.LessThan(1f));
    }
}
