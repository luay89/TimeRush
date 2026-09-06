using NUnit.Framework;
using UnityEngine;

public sealed class ProgressionConfigTests
{
    private static ProgressionConfig CreateConfig()
    {
        return ProgressionConfig.CreateDefault();
    }

    [Test]
    public void ResolveRank_BelowFirstThreshold_ReturnsFirstTier()
    {
        var config = CreateConfig();

        var rank = config.ResolveRank(0);

        Assert.That(rank.index, Is.EqualTo(0));
        Assert.That(rank.name, Is.EqualTo("ROOKIE"));
        Assert.That(rank.hasNext, Is.True);
        Assert.That(rank.nextThreshold, Is.EqualTo(500));

        Object.DestroyImmediate(config);
    }

    [Test]
    public void ResolveRank_AtExactThreshold_SelectsThatTier()
    {
        var config = CreateConfig();

        var rank = config.ResolveRank(1500);

        Assert.That(rank.name, Is.EqualTo("DODGER"));
        Assert.That(rank.currentThreshold, Is.EqualTo(1500));

        Object.DestroyImmediate(config);
    }

    [Test]
    public void ResolveRank_BetweenThresholds_SelectsLowerTier()
    {
        var config = CreateConfig();

        var rank = config.ResolveRank(3499);

        Assert.That(rank.name, Is.EqualTo("DODGER"));
        Assert.That(rank.hasNext, Is.True);
        Assert.That(rank.nextThreshold, Is.EqualTo(3500));

        Object.DestroyImmediate(config);
    }

    [Test]
    public void ResolveRank_AboveTopTier_ReturnsTopWithoutNext()
    {
        var config = CreateConfig();

        var rank = config.ResolveRank(999999);

        Assert.That(rank.name, Is.EqualTo("MASTER"));
        Assert.That(rank.hasNext, Is.False);

        Object.DestroyImmediate(config);
    }

    [Test]
    public void ResolveRank_NegativeScore_ClampsToFirstTier()
    {
        var config = CreateConfig();

        var rank = config.ResolveRank(-1000);

        Assert.That(rank.index, Is.EqualTo(0));
        Assert.That(rank.name, Is.EqualTo("ROOKIE"));

        Object.DestroyImmediate(config);
    }
}
