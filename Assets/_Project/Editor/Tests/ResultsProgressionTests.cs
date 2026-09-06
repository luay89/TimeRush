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
        Assert.That(higher.BestScoreText, Is.EqualTo("Best: 1200"));

        // Stored best higher than the final score: displayed best remains the stored best.
        ResultsPresentation.DisplayData lower = ResultsPresentation.Build(400, 2600, false, RunLossReason.None);
        Assert.That(lower.BestScoreText, Is.EqualTo("Best: 2600"));
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
}
