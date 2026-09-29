using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Covers the additive Results progression surfacing: the pure presentation string reads the
/// supplied progression model, best score never regresses, and rank is driven by the authored
/// Resources/ProgressionConfig.asset (the normal runtime source) at its existing thresholds.
/// </summary>
public sealed class ResultsProgressionTests
{
    // J — Results progression presentation reads the supplied ProgressionModel values.
    [Test]
    public void BuildProgressionSummary_ReflectsModelRunsAndLifetime()
    {
        var model = new ProgressionModel(4, 1875, 0, false);
        var config = ProgressionConfig.CreateDefault();

        string summary = ResultsPresentation.BuildProgressionSummary(model, config.ResolveRank(1875));

        Assert.That(summary, Does.Contain("RUNS 4"));
        Assert.That(summary, Does.Contain("LIFETIME 1875"));
        Assert.That(summary, Does.Contain("DODGER")); // 1500 <= 1875 < 3500

        Object.DestroyImmediate(config);
    }

    // F — Best score never regresses below the current run score in the Results presentation.
    [Test]
    public void ResultsBest_NeverRegressesBelowFinalScore()
    {
        // Stored best lower than the final score: displayed best is promoted to the run score.
        ResultsPresentation.DisplayData higher = ResultsPresentation.Build(1200, 800, true, RunLossReason.None);
        Assert.That(higher.BestScoreText, Is.EqualTo("Best: 1200   //   Today: 1200"));

        // Stored best higher than the final score: displayed best remains the stored best.
        ResultsPresentation.DisplayData lower = ResultsPresentation.Build(400, 2600, false, RunLossReason.None);
        Assert.That(lower.BestScoreText, Is.EqualTo("Best: 2600   //   Today: 400"));
    }

    // Best Today shows the stored daily best when it beats this run, and never drops below the run score.
    [Test]
    public void ResultsDailyBest_UsesStoredDailyBestAndNeverRegressesBelowFinalScore()
    {
        ResultsPresentation.DisplayData storedHigher = ResultsPresentation.Build(400, 2600, false, RunLossReason.None, 900);
        Assert.That(storedHigher.BestScoreText, Is.EqualTo("Best: 2600   //   Today: 900"));

        ResultsPresentation.DisplayData storedLower = ResultsPresentation.Build(1200, 2600, false, RunLossReason.None, 700);
        Assert.That(storedLower.BestScoreText, Is.EqualTo("Best: 2600   //   Today: 1200"));
    }

    // G — Rank matches the existing ProgressionConfig thresholds using the REAL authored asset.
    [Test]
    public void RealProgressionConfigAsset_ResolvesRanksAtExistingThresholds()
    {
        var config = Resources.Load<ProgressionConfig>("ProgressionConfig");
        Assert.That(config, Is.Not.Null,
            "Resources/ProgressionConfig.asset must exist so runtime rank uses the authored asset.");

        Assert.That(config.ResolveRank(0).name, Is.EqualTo("ROOKIE"));
        Assert.That(config.ResolveRank(500).name, Is.EqualTo("RUNNER"));
        Assert.That(config.ResolveRank(1500).name, Is.EqualTo("DODGER"));
        Assert.That(config.ResolveRank(3500).name, Is.EqualTo("VETERAN"));
        Assert.That(config.ResolveRank(7000).name, Is.EqualTo("ACE"));
        Assert.That(config.ResolveRank(12000).name, Is.EqualTo("MASTER"));
        Assert.That(config.ResolveRank(999999).name, Is.EqualTo("MASTER"));
    }

    // H — Determinism: identical best score + config resolves to the identical rank.
    [Test]
    public void Rank_IsDeterministicForSameInputs()
    {
        var config = Resources.Load<ProgressionConfig>("ProgressionConfig");
        Assert.That(config, Is.Not.Null);

        ProgressionConfig.RankResult a = config.ResolveRank(4200);
        ProgressionConfig.RankResult b = config.ResolveRank(4200);

        Assert.That(a.index, Is.EqualTo(b.index));
        Assert.That(a.name, Is.EqualTo(b.name));
        Assert.That(a.currentThreshold, Is.EqualTo(b.currentThreshold));
        Assert.That(a.name, Is.EqualTo("VETERAN"));
    }

    [Test]
    public void BuildNextRunGuidance_BelowBestAndNotMaxRank_ShowsRankAndBestTargets()
    {
        var config = ProgressionConfig.CreateDefault();
        RankProgressInfo info = RankProgression.Evaluate(config, 1500);

        string guidance = ResultsPresentation.BuildNextRunGuidance(1200, 1500, info);

        Assert.That(guidance, Does.Contain("TO VETERAN"));
        Assert.That(guidance, Does.Contain("+301 TO NEW BEST"));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void BuildNextRunGuidance_MaxRankWithoutBest_ShowsNewBestTarget()
    {
        var config = ProgressionConfig.CreateDefault();
        RankProgressInfo info = RankProgression.Evaluate(config, 12000);

        string guidance = ResultsPresentation.BuildNextRunGuidance(11800, 12000, info);

        Assert.That(guidance, Is.EqualTo("NEXT RUN   //   +201 TO NEW BEST"));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void BuildChallengeStatus_FormatsOnlyPressureAndRecovery()
    {
        Assert.That(ResultsPresentation.BuildChallengeStatus(ChallengeState.Pressure), Is.EqualTo("CHALLENGE  //  PRESSURE"));
        Assert.That(ResultsPresentation.BuildChallengeStatus(ChallengeState.Recovery), Is.EqualTo("CHALLENGE  //  RECOVERY"));
        Assert.That(ResultsPresentation.BuildChallengeStatus(ChallengeState.Normal), Is.EqualTo(string.Empty));
    }
}
