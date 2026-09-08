using NUnit.Framework;
using UnityEngine;

/// <summary>
/// End-to-end persistence tests for the local competition layer over real PlayerPrefs, exercised
/// through the exact call order GameController uses at game over and Continue. The player's live
/// records (BEST_SCORE and the progression keys) are snapshotted in setup and restored in teardown
/// so nothing is clobbered. Covers lifetime accuracy, Continue single-counting, failed-Continue
/// integrity, rank compatibility, and reload persistence.
/// </summary>
public sealed class CompetitionProfileTests
{
    private const string BestScoreKey = "BEST_SCORE";
    private const string TotalRunsKey = "TIMERUSH_TOTAL_RUNS";
    private const string LifetimeScoreKey = "TIMERUSH_LIFETIME_SCORE";
    private const string LastRunScoreKey = "TIMERUSH_LAST_RUN_SCORE";
    private const string LastRunCountedKey = "TIMERUSH_LAST_RUN_COUNTED";

    private bool hadBest, hadTotalRuns, hadLifetime, hadLastRun, hadCounted;
    private int savedBest, savedTotalRuns, savedLastRun, savedCounted;
    private string savedLifetime;

    [SetUp]
    public void SetUp()
    {
        hadBest = PlayerPrefs.HasKey(BestScoreKey);
        hadTotalRuns = PlayerPrefs.HasKey(TotalRunsKey);
        hadLifetime = PlayerPrefs.HasKey(LifetimeScoreKey);
        hadLastRun = PlayerPrefs.HasKey(LastRunScoreKey);
        hadCounted = PlayerPrefs.HasKey(LastRunCountedKey);

        savedBest = PlayerPrefs.GetInt(BestScoreKey, 0);
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

        if (hadBest) PlayerPrefs.SetInt(BestScoreKey, savedBest);
        if (hadTotalRuns) PlayerPrefs.SetInt(TotalRunsKey, savedTotalRuns);
        if (hadLifetime) PlayerPrefs.SetString(LifetimeScoreKey, savedLifetime);
        if (hadLastRun) PlayerPrefs.SetInt(LastRunScoreKey, savedLastRun);
        if (hadCounted) PlayerPrefs.SetInt(LastRunCountedKey, savedCounted);
        PlayerPrefs.Save();
    }

    private static void ClearKeys()
    {
        PlayerPrefs.DeleteKey(BestScoreKey);
        PlayerPrefs.DeleteKey(TotalRunsKey);
        PlayerPrefs.DeleteKey(LifetimeScoreKey);
        PlayerPrefs.DeleteKey(LastRunScoreKey);
        PlayerPrefs.DeleteKey(LastRunCountedKey);
        PlayerPrefs.Save();
    }

    // Mirrors GameController.TriggerGameOverInternal: submit the run to the competition
    // leaderboard, then record it into cross-run progression.
    private static CompetitionRecords.Submission SimulateDeath(int score)
    {
        CompetitionRecords.Submission submission = CompetitionProfile.SubmitRun(score);
        ProgressionProfile.RecordRun(score);
        return submission;
    }

    // Mirrors GameController.ContinueRun: only the progression run is rolled back; the highest
    // score personal record is never undone because the player genuinely reached it.
    private static void SimulateContinue()
    {
        ProgressionProfile.RollbackLastRun();
    }

    // D — Lifetime statistics remain correct across multiple runs.
    [Test]
    public void MultipleRuns_LifetimeAndHighestRemainCorrect()
    {
        SimulateDeath(100);
        SimulateDeath(250);
        SimulateDeath(75);

        ProgressionModel progression = ProgressionProfile.Load();
        Assert.That(progression.TotalRuns, Is.EqualTo(3));
        Assert.That(progression.LifetimeScore, Is.EqualTo(425));
        Assert.That(CompetitionProfile.HighestScore, Is.EqualTo(250));
    }

    // E — Continue does not double-count a run (single logical run, counted once with final score).
    [Test]
    public void Continue_CountsLogicalRunExactlyOnce()
    {
        SimulateDeath(100);   // first death
        SimulateContinue();   // player continues; progression rolled back, best kept
        SimulateDeath(260);   // final death with full score

        ProgressionModel progression = ProgressionProfile.Load();
        Assert.That(progression.TotalRuns, Is.EqualTo(1));
        Assert.That(progression.LifetimeScore, Is.EqualTo(260));
        Assert.That(CompetitionProfile.HighestScore, Is.EqualTo(260));
    }

    // F — Failed/closed Continue does not corrupt records (no rollback, no re-record).
    [Test]
    public void FailedContinue_LeavesRecordsIntact()
    {
        SimulateDeath(420);
        // The rewarded ad was unavailable / closed without reward: ContinueRun is never invoked,
        // so no rollback and no second record happens.

        ProgressionModel progression = ProgressionProfile.Load();
        Assert.That(progression.TotalRuns, Is.EqualTo(1));
        Assert.That(progression.LifetimeScore, Is.EqualTo(420));
        Assert.That(CompetitionProfile.HighestScore, Is.EqualTo(420));

        // An independent reload reads the same intact values.
        Assert.That(CompetitionProfile.Load().HighestScore, Is.EqualTo(420));
    }

    // G — Rank progression remains compatible with the existing ProgressionConfig.
    [Test]
    public void HighestScore_ResolvesExpectedRankFromProgressionConfig()
    {
        SimulateDeath(1500);

        ProgressionConfig config = ProgressionConfig.CreateDefault();
        ProgressionConfig.RankResult rank = config.ResolveRank(CompetitionProfile.HighestScore);

        Assert.That(rank.name, Is.EqualTo("DODGER"));

        Object.DestroyImmediate(config);
    }

    // H — Persistence reload preserves competition records.
    [Test]
    public void Persistence_ReloadPreservesHighestScore()
    {
        CompetitionProfile.SubmitRun(1234);

        // A fresh Load and a fresh leaderboard both read the persisted PlayerPrefs value,
        // simulating an app relaunch reading the same store.
        Assert.That(CompetitionProfile.Load().HighestScore, Is.EqualTo(1234));
        Assert.That(CompetitionProfile.GetStanding().BestScore, Is.EqualTo(1234));

        // A lower subsequent run must not regress the persisted record.
        CompetitionProfile.SubmitRun(1000);
        Assert.That(CompetitionProfile.HighestScore, Is.EqualTo(1234));
    }

    [Test]
    public void SubmitRun_NewRecordFlagMatchesPersistedBest()
    {
        CompetitionRecords.Submission first = CompetitionProfile.SubmitRun(300);
        CompetitionRecords.Submission lower = CompetitionProfile.SubmitRun(150);
        CompetitionRecords.Submission higher = CompetitionProfile.SubmitRun(900);

        Assert.That(first.IsNewRecord, Is.True);
        Assert.That(lower.IsNewRecord, Is.False);
        Assert.That(higher.IsNewRecord, Is.True);
        Assert.That(CompetitionProfile.HighestScore, Is.EqualTo(900));
    }
}
