using NUnit.Framework;
using UnityEngine;

public sealed class Phase4UiFoundationTests
{
    [Test]
    public void CalculateAnchorBounds_ConvertsPixelSafeAreaToNormalizedAnchors()
    {
        SafeAreaFitter.CalculateAnchorBounds(
            new Rect(60f, 24f, 2160f, 1020f),
            2280f,
            1080f,
            out Vector2 minimum,
            out Vector2 maximum);

        Assert.That(minimum.x, Is.EqualTo(60f / 2280f).Within(0.0001f));
        Assert.That(minimum.y, Is.EqualTo(24f / 1080f).Within(0.0001f));
        Assert.That(maximum.x, Is.EqualTo(2220f / 2280f).Within(0.0001f));
        Assert.That(maximum.y, Is.EqualTo(1044f / 1080f).Within(0.0001f));
    }

    [Test]
    public void CalculateAnchorBounds_ClampsInvalidInputInsideNormalizedRange()
    {
        SafeAreaFitter.CalculateAnchorBounds(
            new Rect(-40f, -20f, 260f, 170f),
            200f,
            120f,
            out Vector2 minimum,
            out Vector2 maximum);

        Assert.That(minimum, Is.EqualTo(Vector2.zero));
        Assert.That(maximum, Is.EqualTo(Vector2.one));
    }

    [Test]
    public void ResultsPresentation_UsesTypedCollisionReasonAndNewBestFlag()
    {
        ResultsPresentation.DisplayData display = ResultsPresentation.Build(142, 142, true, RunLossReason.ObstacleCollision);

        Assert.That(display.FinalScoreText, Is.EqualTo("Score: 142"));
        Assert.That(display.BestScoreText, Is.EqualTo("Best: 142"));
        Assert.That(display.StatusText, Is.EqualTo("NEW BEST  //  IMPACT DETECTED"));
    }

    [Test]
    public void ResultsPresentation_DoesNotInventSpecificReasonWhenNoTypedReasonExists()
    {
        ResultsPresentation.DisplayData display = ResultsPresentation.Build(38, 90, false, RunLossReason.None);

        Assert.That(display.FinalScoreText, Is.EqualTo("Score: 38"));
        Assert.That(display.BestScoreText, Is.EqualTo("Best: 90"));
        Assert.That(display.StatusText, Is.EqualTo("RUN ENDED"));
    }
}
