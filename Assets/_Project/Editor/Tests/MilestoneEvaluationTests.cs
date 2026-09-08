using NUnit.Framework;

/// <summary>
/// Pure evaluation tests for the skill-based milestone layer (no PlayerPrefs, no Unity). Covers the
/// deterministic unlock rules: first run, single-run score clubs, rank climbs, personal best, the
/// below-threshold negative case, and idempotent re-evaluation. These are the A–F cases of the
/// progression-depth phase.
/// </summary>
public sealed class MilestoneEvaluationTests
{
    // A: The onboarding "First Run" milestone unlocks exactly once, on the first recorded run.
    [Test]
    public void A_FirstRun_UnlocksOnce()
    {
        MilestoneState.Evaluation first = MilestoneState.Empty.Evaluate(
            new MilestoneFacts(totalRuns: 1, bestScore: 0, rankIndex: 0, setNewBest: false));

        Assert.IsTrue(first.State.IsUnlocked(MilestoneId.FirstRun));
        Assert.Contains(MilestoneId.FirstRun, first.NewlyUnlocked);

        MilestoneState.Evaluation second = first.State.Evaluate(
            new MilestoneFacts(totalRuns: 2, bestScore: 0, rankIndex: 0, setNewBest: false));

        Assert.IsFalse(second.HasNewUnlocks, "First Run must not unlock again on a later run.");
    }

    // B: Crossing a single-run score threshold unlocks the corresponding score milestone(s).
    [Test]
    public void B_ScoreThreshold_Unlocks()
    {
        MilestoneState.Evaluation atThousand = MilestoneState.Empty.Evaluate(
            new MilestoneFacts(totalRuns: 1, bestScore: 1000, rankIndex: 0, setNewBest: false));

        Assert.IsTrue(atThousand.State.IsUnlocked(MilestoneId.Break1000));
        Assert.IsFalse(atThousand.State.IsUnlocked(MilestoneId.Break5000));

        MilestoneState.Evaluation atFiveThousand = atThousand.State.Evaluate(
            new MilestoneFacts(totalRuns: 2, bestScore: 5000, rankIndex: 0, setNewBest: false));

        Assert.IsTrue(atFiveThousand.State.IsUnlocked(MilestoneId.Break5000));
        Assert.Contains(MilestoneId.Break5000, atFiveThousand.NewlyUnlocked);
        Assert.IsFalse(atFiveThousand.State.IsUnlocked(MilestoneId.Break10000));
    }

    // C: Staying just below a threshold unlocks nothing for that threshold.
    [Test]
    public void C_BelowThreshold_DoesNotUnlock()
    {
        MilestoneState.Evaluation result = MilestoneState.Empty.Evaluate(
            new MilestoneFacts(totalRuns: 1, bestScore: 999, rankIndex: 0, setNewBest: false));

        Assert.IsFalse(result.State.IsUnlocked(MilestoneId.Break1000));
        Assert.IsFalse(result.State.IsUnlocked(MilestoneId.Break5000));
    }

    // D: Re-evaluating identical facts against an already-satisfied state yields no new unlocks.
    [Test]
    public void D_RepeatEvaluation_NoDuplicate()
    {
        MilestoneFacts facts = new MilestoneFacts(totalRuns: 3, bestScore: 1200, rankIndex: 1, setNewBest: false);

        MilestoneState.Evaluation first = MilestoneState.Empty.Evaluate(facts);
        int countAfterFirst = first.State.UnlockedCount;
        Assert.IsTrue(first.HasNewUnlocks);

        MilestoneState.Evaluation second = first.State.Evaluate(facts);
        Assert.IsFalse(second.HasNewUnlocks);
        Assert.AreEqual(countAfterFirst, second.State.UnlockedCount);
        Assert.AreEqual(first.State.Mask, second.State.Mask);
    }

    // E: Reaching a rank index unlocks the rank-climb milestones at or below that index.
    [Test]
    public void E_RankIndex_Unlocks()
    {
        MilestoneState.Evaluation runner = MilestoneState.Empty.Evaluate(
            new MilestoneFacts(totalRuns: 1, bestScore: 600, rankIndex: 1, setNewBest: false));

        Assert.IsTrue(runner.State.IsUnlocked(MilestoneId.ReachRunner));
        Assert.IsFalse(runner.State.IsUnlocked(MilestoneId.ReachDodger));

        MilestoneState.Evaluation dodger = runner.State.Evaluate(
            new MilestoneFacts(totalRuns: 2, bestScore: 1600, rankIndex: 2, setNewBest: false));

        Assert.IsTrue(dodger.State.IsUnlocked(MilestoneId.ReachDodger));
        Assert.Contains(MilestoneId.ReachDodger, dodger.NewlyUnlocked);
    }

    // F: Setting a new personal best unlocks the personal-best milestone.
    [Test]
    public void F_NewPersonalBest_Unlocks()
    {
        MilestoneState.Evaluation result = MilestoneState.Empty.Evaluate(
            new MilestoneFacts(totalRuns: 1, bestScore: 250, rankIndex: 0, setNewBest: true));

        Assert.IsTrue(result.State.IsUnlocked(MilestoneId.NewPersonalBest));
        Assert.Contains(MilestoneId.NewPersonalBest, result.NewlyUnlocked);
    }
}
