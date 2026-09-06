using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Exercises the PlayerPrefs-backed <see cref="ProgressionProfile"/> persistence facade end to end:
/// fresh state, run counting, Continue rollback, and reload round-trip. Real PlayerPrefs values are
/// snapshotted in setup and restored in teardown so a developer's live progression is never clobbered.
/// </summary>
public sealed class ProgressionProfileTests
{
    private const string TotalRunsKey = "TIMERUSH_TOTAL_RUNS";
    private const string LifetimeScoreKey = "TIMERUSH_LIFETIME_SCORE";
    private const string LastRunScoreKey = "TIMERUSH_LAST_RUN_SCORE";
    private const string LastRunCountedKey = "TIMERUSH_LAST_RUN_COUNTED";

    private bool hadTotalRuns, hadLifetime, hadLastRun, hadCounted;
    private int savedTotalRuns, savedLastRun, savedCounted;
    private string savedLifetime;

    [SetUp]
    public void SetUp()
    {
        hadTotalRuns = PlayerPrefs.HasKey(TotalRunsKey);
        hadLifetime = PlayerPrefs.HasKey(LifetimeScoreKey);
        hadLastRun = PlayerPrefs.HasKey(LastRunScoreKey);
        hadCounted = PlayerPrefs.HasKey(LastRunCountedKey);

        savedTotalRuns = PlayerPrefs.GetInt(TotalRunsKey, 0);
        savedLifetime = PlayerPrefs.GetString(LifetimeScoreKey, "0");
        savedLastRun = PlayerPrefs.GetInt(LastRunScoreKey, 0);
        savedCounted = PlayerPrefs.GetInt(LastRunCountedKey, 0);

        ClearKeys();
    }

    [TearDown]
    public void TearDown()
    {
        ClearKeys();

        if (hadTotalRuns) PlayerPrefs.SetInt(TotalRunsKey, savedTotalRuns);
        if (hadLifetime) PlayerPrefs.SetString(LifetimeScoreKey, savedLifetime);
        if (hadLastRun) PlayerPrefs.SetInt(LastRunScoreKey, savedLastRun);
        if (hadCounted) PlayerPrefs.SetInt(LastRunCountedKey, savedCounted);
        PlayerPrefs.Save();
    }

    private static void ClearKeys()
    {
        PlayerPrefs.DeleteKey(TotalRunsKey);
        PlayerPrefs.DeleteKey(LifetimeScoreKey);
        PlayerPrefs.DeleteKey(LastRunScoreKey);
        PlayerPrefs.DeleteKey(LastRunCountedKey);
        PlayerPrefs.Save();
    }

    // A — Fresh profile
    [Test]
    public void FreshProfile_StartsAtZeroWithDefaultLastRunState()
    {
        ProgressionModel model = ProgressionProfile.Load();

        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
        Assert.That(model.LastRunScore, Is.EqualTo(0));
        Assert.That(model.LastRunCounted, Is.False);
    }

    // B — Completed run increments lifetime runs exactly once
    [Test]
    public void RecordRun_IncrementsTotalRunsExactlyOnce()
    {
        ProgressionProfile.RecordRun(150);

        ProgressionModel model = ProgressionProfile.Load();
        Assert.That(model.TotalRuns, Is.EqualTo(1));
        Assert.That(model.LifetimeScore, Is.EqualTo(150));
        Assert.That(model.LastRunCounted, Is.True);
    }

    // C — Score accumulates across multiple runs
    [Test]
    public void RecordRun_AccumulatesLifetimeScoreAcrossRuns()
    {
        ProgressionProfile.RecordRun(100);
        ProgressionProfile.RecordRun(250);
        ProgressionProfile.RecordRun(75);

        ProgressionModel model = ProgressionProfile.Load();
        Assert.That(model.TotalRuns, Is.EqualTo(3));
        Assert.That(model.LifetimeScore, Is.EqualTo(425));
    }

    // D — Continue rollback removes the just-recorded run exactly once
    [Test]
    public void RollbackLastRun_RemovesRecordedRunExactlyOnce()
    {
        ProgressionProfile.RecordRun(320);
        ProgressionProfile.RollbackLastRun();

        ProgressionModel model = ProgressionProfile.Load();
        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
        Assert.That(model.LastRunCounted, Is.False);
    }

    [Test]
    public void RollbackLastRun_TwiceDoesNotUndercount()
    {
        ProgressionProfile.RecordRun(500);
        ProgressionProfile.RollbackLastRun();
        ProgressionProfile.RollbackLastRun();

        ProgressionModel model = ProgressionProfile.Load();
        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
    }

    // E — Continued run is counted exactly once with its final score
    [Test]
    public void ContinuedRun_IsCountedExactlyOnceWithFinalScore()
    {
        ProgressionProfile.RecordRun(100); // first death
        ProgressionProfile.RollbackLastRun(); // player continued
        ProgressionProfile.RecordRun(260); // final death, full score

        ProgressionModel model = ProgressionProfile.Load();
        Assert.That(model.TotalRuns, Is.EqualTo(1));
        Assert.That(model.LifetimeScore, Is.EqualTo(260));
    }

    // I — Persistence: write progression, reload, values identical
    [Test]
    public void Persistence_ReloadReturnsIdenticalValues()
    {
        ProgressionProfile.RecordRun(100);
        ProgressionProfile.RecordRun(900);

        ProgressionModel first = ProgressionProfile.Load();
        // A second independent Load reads freshly from the persisted PlayerPrefs store,
        // simulating a profile reload; the values must be identical.
        ProgressionModel reloaded = ProgressionProfile.Load();

        Assert.That(reloaded.TotalRuns, Is.EqualTo(first.TotalRuns));
        Assert.That(reloaded.LifetimeScore, Is.EqualTo(first.LifetimeScore));
        Assert.That(reloaded.LastRunScore, Is.EqualTo(first.LastRunScore));
        Assert.That(reloaded.LastRunCounted, Is.EqualTo(first.LastRunCounted));
        Assert.That(reloaded.TotalRuns, Is.EqualTo(2));
        Assert.That(reloaded.LifetimeScore, Is.EqualTo(1000));
    }

    // J (profile-backed) — Results presentation reads the actual persisted profile values
    [Test]
    public void ResultsSummary_ReadsActualPersistedProfileValues()
    {
        ProgressionProfile.RecordRun(700);
        ProgressionProfile.RecordRun(300);

        ProgressionModel model = ProgressionProfile.Load();
        ProgressionConfig config = ProgressionConfig.CreateDefault();
        string summary = ResultsPresentation.BuildProgressionSummary(model, config.ResolveRank(700));

        Assert.That(summary, Does.Contain("RUNS 2"));
        Assert.That(summary, Does.Contain("LIFETIME 1000"));

        Object.DestroyImmediate(config);
    }
}
