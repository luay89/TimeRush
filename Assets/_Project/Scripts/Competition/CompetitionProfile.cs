using UnityEngine;

/// <summary>
/// PlayerPrefs-backed facade over the local competition records, mirroring the existing
/// <c>ProgressionProfile</c> pattern. It reuses the long-standing <c>BEST_SCORE</c> key so
/// the highest-score personal record has exactly one source of truth shared with the HUD
/// and Results best-score display — no parallel save file, no JSON, no new save system.
/// Production always routes through a <see cref="LocalScoreLeaderboard"/>, so the offline
/// implementation exercises the same competition abstraction a future online adapter would.
/// </summary>
public static class CompetitionProfile
{
    // Shared with GameController/ResultsController so best score never diverges.
    private const string HighestScoreKey = "BEST_SCORE";

    public static int HighestScore => Load().HighestScore;

    public static CompetitionRecords Load()
    {
        return new CompetitionRecords(PlayerPrefs.GetInt(HighestScoreKey, 0));
    }

    /// <summary>
    /// Submits a completed run's score through the local leaderboard and returns the
    /// resulting records plus whether a new personal record was set. A Continue rollback
    /// never touches this: the highest score reflects the score the player actually reached.
    /// </summary>
    public static CompetitionRecords.Submission SubmitRun(int score)
    {
        IScoreLeaderboard leaderboard = CreateLeaderboard();
        LeaderboardStanding standing = leaderboard.Submit(new RunOutcome(score));
        return new CompetitionRecords.Submission(new CompetitionRecords(standing.BestScore), standing.IsNewRecord);
    }

    /// <summary>Reads the current standing without recording a run.</summary>
    public static LeaderboardStanding GetStanding()
    {
        return CreateLeaderboard().GetStanding();
    }

    /// <summary>
    /// Builds the offline production leaderboard. Swapping this for an online adapter later
    /// requires no change to any gameplay caller, which only depends on the pure model.
    /// </summary>
    public static IScoreLeaderboard CreateLeaderboard()
    {
        return new LocalScoreLeaderboard(new PlayerPrefsCompetitionRecordStore());
    }
}

/// <summary>PlayerPrefs implementation of the competition record store (offline, no networking).</summary>
public sealed class PlayerPrefsCompetitionRecordStore : ICompetitionRecordStore
{
    private const string HighestScoreKey = "BEST_SCORE";

    public CompetitionRecords Load()
    {
        return new CompetitionRecords(PlayerPrefs.GetInt(HighestScoreKey, 0));
    }

    public void Save(CompetitionRecords records)
    {
        PlayerPrefs.SetInt(HighestScoreKey, records.HighestScore);
        PlayerPrefs.Save();
    }
}
