using NUnit.Framework;

/// <summary>
/// Pure tests for <see cref="RankProgression"/>, which derives next-rank progression from the existing
/// rank ladder without altering thresholds. Covers mid-ladder progress (I) and the MAX RANK terminal
/// case (J) using the default rank tiers.
/// </summary>
public sealed class RankProgressionTests
{
    // I: With the default ladder (ROOKIE 0, RUNNER 500, DODGER 1500, ...), a best of 1000 sits in
    // RUNNER halfway to DODGER: 500 points earned of a 1000-point span, 500 remaining, 50% progress.
    [Test]
    public void I_NextRankProgress_IsCorrect()
    {
        ProgressionConfig config = ProgressionConfig.CreateDefault();

        RankProgressInfo info = RankProgression.Evaluate(config, 1000);

        Assert.IsFalse(info.IsMaxRank);
        Assert.AreEqual(1, info.RankIndex);
        Assert.AreEqual("RUNNER", info.CurrentRankName);
        Assert.AreEqual("DODGER", info.NextRankName);
        Assert.AreEqual(500, info.CurrentThreshold);
        Assert.AreEqual(1500, info.NextThreshold);
        Assert.AreEqual(500, info.PointsToNext);
        Assert.AreEqual(0.5f, info.Progress01, 0.0001f);
    }

    // J: A best beyond the top threshold resolves to the final rank with a clean MAX RANK signal:
    // no next rank, zero points to go, full progress.
    [Test]
    public void J_MaxRank_IsReportedCleanly()
    {
        ProgressionConfig config = ProgressionConfig.CreateDefault();

        RankProgressInfo info = RankProgression.Evaluate(config, 15000);

        Assert.IsTrue(info.IsMaxRank);
        Assert.AreEqual("MASTER", info.CurrentRankName);
        Assert.AreEqual(0, info.PointsToNext);
        Assert.AreEqual(1f, info.Progress01, 0.0001f);
    }
}
