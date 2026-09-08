using NUnit.Framework;

/// <summary>
/// Tests the local competition leaderboard abstraction with a pure in-memory store, proving that a
/// future online leaderboard adapter could consume the same <see cref="RunOutcome"/> /
/// <see cref="LeaderboardStanding"/> model with no dependency on gameplay, Unity, or networking.
/// </summary>
public sealed class ScoreLeaderboardTests
{
    /// <summary>Pure in-memory store; no PlayerPrefs, no Unity, no networking.</summary>
    private sealed class InMemoryStore : ICompetitionRecordStore
    {
        private CompetitionRecords records;

        public InMemoryStore(int initialBest)
        {
            records = new CompetitionRecords(initialBest);
        }

        public int SaveCount { get; private set; }

        public CompetitionRecords Load() => records;

        public void Save(CompetitionRecords value)
        {
            records = value;
            SaveCount++;
        }
    }

    // A future leaderboard adapter (here a trivial offline stub) can consume the model with
    // no knowledge of gameplay or transport — this is the online-ready seam.
    private sealed class StubOnlineLeaderboardAdapter : IScoreLeaderboard
    {
        private int best;

        public LeaderboardStanding Submit(RunOutcome outcome)
        {
            bool isRecord = outcome.Score > best;
            if (isRecord)
            {
                best = outcome.Score;
            }

            return new LeaderboardStanding(best, isRecord);
        }

        public LeaderboardStanding GetStanding() => new LeaderboardStanding(best, false);
    }

    [Test]
    public void Submit_HigherScore_UpdatesStandingAndPersists()
    {
        var store = new InMemoryStore(100);
        var leaderboard = new LocalScoreLeaderboard(store);

        LeaderboardStanding standing = leaderboard.Submit(new RunOutcome(250));

        Assert.That(standing.IsNewRecord, Is.True);
        Assert.That(standing.BestScore, Is.EqualTo(250));
        Assert.That(store.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public void Submit_EqualScore_DoesNotPersistOrReportRecord()
    {
        var store = new InMemoryStore(250);
        var leaderboard = new LocalScoreLeaderboard(store);

        LeaderboardStanding standing = leaderboard.Submit(new RunOutcome(250));

        Assert.That(standing.IsNewRecord, Is.False);
        Assert.That(standing.BestScore, Is.EqualTo(250));
        Assert.That(store.SaveCount, Is.EqualTo(0));
    }

    [Test]
    public void GetStanding_ReadsBestWithoutSubmitting()
    {
        var store = new InMemoryStore(777);
        var leaderboard = new LocalScoreLeaderboard(store);

        LeaderboardStanding standing = leaderboard.GetStanding();

        Assert.That(standing.BestScore, Is.EqualTo(777));
        Assert.That(standing.IsNewRecord, Is.False);
        Assert.That(store.SaveCount, Is.EqualTo(0));
    }

    // I — A future leaderboard adapter can consume the local score model without any
    // gameplay/networking coupling: the exact same RunOutcome drives both implementations.
    [Test]
    public void FutureAdapter_ConsumesSameModelAsLocalLeaderboard()
    {
        RunOutcome outcome = new RunOutcome(1234);

        IScoreLeaderboard local = new LocalScoreLeaderboard(new InMemoryStore(0));
        IScoreLeaderboard futureOnline = new StubOnlineLeaderboardAdapter();

        LeaderboardStanding localStanding = local.Submit(outcome);
        LeaderboardStanding onlineStanding = futureOnline.Submit(outcome);

        Assert.That(localStanding.BestScore, Is.EqualTo(onlineStanding.BestScore));
        Assert.That(localStanding.IsNewRecord, Is.EqualTo(onlineStanding.IsNewRecord));
        Assert.That(localStanding.BestScore, Is.EqualTo(1234));
    }

    [Test]
    public void Constructor_RejectsNullStore()
    {
        Assert.That(() => new LocalScoreLeaderboard(null), Throws.ArgumentNullException);
    }
}
