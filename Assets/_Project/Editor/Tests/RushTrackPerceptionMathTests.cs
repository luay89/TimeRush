using NUnit.Framework;

/// <summary>
/// Focused deterministic tests for presentation-only rush track perception math.
/// </summary>
public sealed class RushTrackPerceptionMathTests
{
    [Test]
    public void ResolveMarkerTravelSpeed_NonPositiveObstacleSpeed_IsZero()
    {
        Assert.That(RushTrackPerceptionMath.ResolveMarkerTravelSpeed(0f, 2f, true, true), Is.EqualTo(0f));
        Assert.That(RushTrackPerceptionMath.ResolveMarkerTravelSpeed(-5f, 2f, true, true), Is.EqualTo(0f));
    }

    [Test]
    public void ResolveMarkerTravelSpeed_IsMonotonicWithPaceWhenActiveAndEnabled()
    {
        float slow = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(6f, 1f, true, true);
        float medium = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(6f, 1.5f, true, true);
        float fast = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(6f, 2f, true, true);

        Assert.That(medium, Is.GreaterThanOrEqualTo(slow));
        Assert.That(fast, Is.GreaterThanOrEqualTo(medium));
    }

    [Test]
    public void ResolveMarkerTravelSpeed_AccessibilityGate_ForcesNeutralPace()
    {
        float fromDisabledShake = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(6f, 2f, true, false);
        float fromInactiveGameplay = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(6f, 2f, false, true);
        float neutral = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(6f, 1f, true, true);

        Assert.That(fromDisabledShake, Is.EqualTo(neutral).Within(0.0001f));
        Assert.That(fromInactiveGameplay, Is.EqualTo(neutral).Within(0.0001f));
    }

    [Test]
    public void WrapHeight_AlwaysReturnsInsideBand()
    {
        float wrappedLow = RushTrackPerceptionMath.WrapHeight(-15f, -2.75f, 14f);
        float wrappedHigh = RushTrackPerceptionMath.WrapHeight(40f, -2.75f, 14f);

        Assert.That(wrappedLow, Is.GreaterThanOrEqualTo(-2.75f));
        Assert.That(wrappedLow, Is.LessThanOrEqualTo(14f));
        Assert.That(wrappedHigh, Is.GreaterThanOrEqualTo(-2.75f));
        Assert.That(wrappedHigh, Is.LessThanOrEqualTo(14f));
    }

    [Test]
    public void ComputeApproachScale_IsNearLargeFarSmall()
    {
        float near = RushTrackPerceptionMath.ComputeApproachScale(0f, 1.32f, 0.72f);
        float middle = RushTrackPerceptionMath.ComputeApproachScale(0.5f, 1.32f, 0.72f);
        float far = RushTrackPerceptionMath.ComputeApproachScale(1f, 1.32f, 0.72f);

        Assert.That(near, Is.EqualTo(1.32f).Within(0.0001f));
        Assert.That(far, Is.EqualTo(0.72f).Within(0.0001f));
        Assert.That(middle, Is.LessThan(near));
        Assert.That(middle, Is.GreaterThan(far));
    }

    [Test]
    public void ComputeAmbientGlowBrightness_BaselinePaceIsNoBoost()
    {
        Assert.That(RushTrackPerceptionMath.ComputeAmbientGlowBrightness(1f, 1.9f), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ComputeAmbientGlowBrightness_ReachesAndNeverExceedsMaxBoost()
    {
        float atCap = RushTrackPerceptionMath.ComputeAmbientGlowBrightness(2f, 1.9f);
        float beyondCap = RushTrackPerceptionMath.ComputeAmbientGlowBrightness(10f, 1.9f);

        Assert.That(atCap, Is.EqualTo(1.9f).Within(0.0001f));
        Assert.That(beyondCap, Is.EqualTo(1.9f).Within(0.0001f));
    }
}
