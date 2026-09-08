/// <summary>
/// Pure, data-only milestone definitions for the local, skill-based retention layer. A milestone is
/// a one-time goal derived entirely from measurable gameplay facts already tracked by the game
/// (lifetime runs, best score, resolved rank, and whether a run set a new personal best). There is
/// no currency, XP economy, timer, or grind: every milestone is a discrete skill achievement that
/// unlocks exactly once. Keeping this layer free of Unity and UI types makes completion fully testable.
/// </summary>
public enum MilestoneId
{
    FirstRun = 0,
    Break1000 = 1,
    Break5000 = 2,
    Break10000 = 3,
    ReachRunner = 4,
    ReachDodger = 5,
    NewPersonalBest = 6,
}

/// <summary>The kind of measurable fact a milestone is evaluated against.</summary>
public enum MilestoneKind
{
    RunsAtLeast,
    BestScoreAtLeast,
    RankIndexAtLeast,
    NewPersonalBest,
}

/// <summary>
/// Immutable snapshot of the measurable gameplay facts a milestone can depend on. The gameplay /
/// progression layer produces these facts; the milestone layer only interprets them.
/// </summary>
public readonly struct MilestoneFacts
{
    public readonly int TotalRuns;
    public readonly int BestScore;
    public readonly int RankIndex;
    public readonly bool SetNewBest;

    public MilestoneFacts(int totalRuns, int bestScore, int rankIndex, bool setNewBest)
    {
        TotalRuns = totalRuns < 0 ? 0 : totalRuns;
        BestScore = bestScore < 0 ? 0 : bestScore;
        RankIndex = rankIndex < 0 ? 0 : rankIndex;
        SetNewBest = setNewBest;
    }
}

/// <summary>A single milestone: a stable id, a player-facing title, and its unlock condition.</summary>
public readonly struct MilestoneDefinition
{
    public readonly MilestoneId Id;
    public readonly string Title;
    public readonly MilestoneKind Kind;
    public readonly int Threshold;

    public MilestoneDefinition(MilestoneId id, string title, MilestoneKind kind, int threshold)
    {
        Id = id;
        Title = title;
        Kind = kind;
        Threshold = threshold;
    }

    /// <summary>Pure predicate: is this milestone satisfied by the supplied facts?</summary>
    public bool IsSatisfied(MilestoneFacts facts)
    {
        switch (Kind)
        {
            case MilestoneKind.RunsAtLeast:
                return facts.TotalRuns >= Threshold;
            case MilestoneKind.BestScoreAtLeast:
                return facts.BestScore >= Threshold;
            case MilestoneKind.RankIndexAtLeast:
                return facts.RankIndex >= Threshold;
            case MilestoneKind.NewPersonalBest:
                return facts.SetNewBest;
            default:
                return false;
        }
    }
}

/// <summary>
/// The fixed, ordered catalog of milestones. Deliberately small (7 skill goals): an onboarding first
/// run, three single-run score clubs, two rank climbs, and the first personal best. No grind-based
/// (run-count) goals beyond the first run.
/// </summary>
public static class MilestoneCatalog
{
    public static readonly MilestoneDefinition[] All =
    {
        new MilestoneDefinition(MilestoneId.FirstRun, "First Run", MilestoneKind.RunsAtLeast, 1),
        new MilestoneDefinition(MilestoneId.Break1000, "Break 1000", MilestoneKind.BestScoreAtLeast, 1000),
        new MilestoneDefinition(MilestoneId.Break5000, "Break 5000", MilestoneKind.BestScoreAtLeast, 5000),
        new MilestoneDefinition(MilestoneId.Break10000, "Break 10000", MilestoneKind.BestScoreAtLeast, 10000),
        new MilestoneDefinition(MilestoneId.ReachRunner, "Reach RUNNER", MilestoneKind.RankIndexAtLeast, 1),
        new MilestoneDefinition(MilestoneId.ReachDodger, "Reach DODGER", MilestoneKind.RankIndexAtLeast, 2),
        new MilestoneDefinition(MilestoneId.NewPersonalBest, "New Personal Best", MilestoneKind.NewPersonalBest, 0),
    };

    public static bool TryGet(MilestoneId id, out MilestoneDefinition definition)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].Id == id)
            {
                definition = All[i];
                return true;
            }
        }

        definition = default;
        return false;
    }

    public static string TitleOf(MilestoneId id)
    {
        return TryGet(id, out MilestoneDefinition definition) ? definition.Title : id.ToString();
    }
}
