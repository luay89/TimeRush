using NUnit.Framework;

/// <summary>
/// Focused deterministic tests for the depth-position camera readability cue (Phase K-7). Purely
/// presentation-layer; no gameplay/fairness/pattern/progression system is exercised or changed.
/// </summary>
public sealed class DepthPerceptionMathTests
{
    [Test]
    public void ComputeNormalizedDepth_CenterIsZero()
    {
        Assert.That(DepthPerceptionMath.ComputeNormalizedDepth(0f, 0f, -2f, 2f), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ComputeNormalizedDepth_ReachesBoundsAtExtremes()
    {
        Assert.That(DepthPerceptionMath.ComputeNormalizedDepth(-2f, 0f, -2f, 2f), Is.EqualTo(-1f).Within(0.0001f));
        Assert.That(DepthPerceptionMath.ComputeNormalizedDepth(2f, 0f, -2f, 2f), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ComputeNormalizedDepth_ClampsBeyondRange()
    {
        Assert.That(DepthPerceptionMath.ComputeNormalizedDepth(10f, 0f, -2f, 2f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(DepthPerceptionMath.ComputeNormalizedDepth(-10f, 0f, -2f, 2f), Is.EqualTo(-1f).Within(0.0001f));
    }

    [Test]
    public void ComputeNormalizedDepth_DegenerateRangeNeverThrowsOrIsNaN()
    {
        float result = DepthPerceptionMath.ComputeNormalizedDepth(0f, 0f, 1f, 1f);
        Assert.That(float.IsNaN(result), Is.False);
    }

    [Test]
    public void ComputeDepthFieldOfViewBoost_IsSymmetricAndBoundedByMax()
    {
        float atForward = DepthPerceptionMath.ComputeDepthFieldOfViewBoost(1f, 1.6f);
        float atBackward = DepthPerceptionMath.ComputeDepthFieldOfViewBoost(-1f, 1.6f);
        float atCenter = DepthPerceptionMath.ComputeDepthFieldOfViewBoost(0f, 1.6f);

        Assert.That(atForward, Is.EqualTo(1.6f).Within(0.0001f));
        Assert.That(atBackward, Is.EqualTo(1.6f).Within(0.0001f));
        Assert.That(atCenter, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ComputeDepthLagOffset_IsSignedAndBoundedByMax()
    {
        Assert.That(DepthPerceptionMath.ComputeDepthLagOffset(1f, 0.6f), Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(DepthPerceptionMath.ComputeDepthLagOffset(-1f, 0.6f), Is.EqualTo(-0.6f).Within(0.0001f));
        Assert.That(DepthPerceptionMath.ComputeDepthLagOffset(0.5f, 0.6f), Is.EqualTo(0.3f).Within(0.0001f));
    }
}
