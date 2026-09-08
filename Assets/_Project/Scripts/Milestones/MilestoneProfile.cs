using UnityEngine;

/// <summary>
/// PlayerPrefs-backed facade over <see cref="MilestoneState"/>, mirroring the existing
/// <c>ProgressionProfile</c> / <c>CompetitionProfile</c> pattern. Milestone completion is a single
/// integer bitmask under its own key, so it persists across reloads and never touches BEST_SCORE,
/// lifetime runs/score, or competition records. No JSON, database, or new save manager is introduced.
/// </summary>
public static class MilestoneProfile
{
    private const string MilestoneMaskKey = "TIMERUSH_MILESTONES";

    public static MilestoneState Load()
    {
        return new MilestoneState(PlayerPrefs.GetInt(MilestoneMaskKey, 0));
    }

    public static bool IsUnlocked(MilestoneId id)
    {
        return Load().IsUnlocked(id);
    }

    public static int UnlockedCount => Load().UnlockedCount;

    /// <summary>
    /// Evaluates the supplied facts against the persisted state and saves only when something new was
    /// unlocked. Returns the evaluation so the caller can present newly unlocked milestones. Idempotent:
    /// evaluating the same facts again (for example on the second Results visit after a Continue) unlocks
    /// nothing further.
    /// </summary>
    public static MilestoneState.Evaluation EvaluateAndPersist(MilestoneFacts facts)
    {
        MilestoneState current = Load();
        MilestoneState.Evaluation evaluation = current.Evaluate(facts);

        if (evaluation.State.Mask != current.Mask)
        {
            PlayerPrefs.SetInt(MilestoneMaskKey, evaluation.State.Mask);
            PlayerPrefs.Save();
        }

        return evaluation;
    }
}
