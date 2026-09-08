/// <summary>
/// Local competition abstraction. This is the seam a future online leaderboard would
/// implement: gameplay submits a <see cref="RunOutcome"/> and reads a
/// <see cref="LeaderboardStanding"/> without knowing whether the backing store is local
/// PlayerPrefs or a remote service. The production implementation
/// (<see cref="LocalScoreLeaderboard"/>) stays fully offline and never references any
/// networking, Unity multiplayer, or cloud-save APIs.
/// </summary>

/// <summary>Immutable, transport-agnostic description of a completed run being submitted.</summary>
public readonly struct RunOutcome
{
    public readonly int Score;

    public RunOutcome(int score)
    {
        Score = score < 0 ? 0 : score;
    }
}

/// <summary>Immutable result of a submission or standings query.</summary>
public readonly struct LeaderboardStanding
{
    /// <summary>The player's best local score.</summary>
    public readonly int BestScore;

    /// <summary>True when the just-submitted run beat the previous best.</summary>
    public readonly bool IsNewRecord;

    public LeaderboardStanding(int bestScore, bool isNewRecord)
    {
        BestScore = bestScore < 0 ? 0 : bestScore;
        IsNewRecord = isNewRecord;
    }
}

/// <summary>
/// Competition boundary. An online leaderboard adapter can implement this later without
/// any change to gameplay code, because callers only ever see pure value types.
/// </summary>
public interface IScoreLeaderboard
{
    /// <summary>Submits a run's outcome and returns the resulting standing.</summary>
    LeaderboardStanding Submit(RunOutcome outcome);

    /// <summary>Reads the current standing without submitting a run.</summary>
    LeaderboardStanding GetStanding();
}

/// <summary>
/// Persistence seam for the local leaderboard, injected so the leaderboard logic stays
/// pure and testable against an in-memory store.
/// </summary>
public interface ICompetitionRecordStore
{
    CompetitionRecords Load();
    void Save(CompetitionRecords records);
}

/// <summary>
/// Offline production leaderboard. All logic is pure aside from the injected store, so a
/// future replacement only has to swap the store or provide an alternative
/// <see cref="IScoreLeaderboard"/>; gameplay stays untouched and network-free.
/// </summary>
public sealed class LocalScoreLeaderboard : IScoreLeaderboard
{
    private readonly ICompetitionRecordStore store;

    public LocalScoreLeaderboard(ICompetitionRecordStore store)
    {
        this.store = store ?? throw new System.ArgumentNullException(nameof(store));
    }

    public LeaderboardStanding Submit(RunOutcome outcome)
    {
        CompetitionRecords current = store.Load();
        CompetitionRecords.Submission result = current.Register(outcome.Score);

        if (result.IsNewRecord)
        {
            store.Save(result.Records);
        }

        return new LeaderboardStanding(result.Records.HighestScore, result.IsNewRecord);
    }

    public LeaderboardStanding GetStanding()
    {
        CompetitionRecords current = store.Load();
        return new LeaderboardStanding(current.HighestScore, false);
    }
}
