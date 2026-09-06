using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PatternFairnessTests
{
    private static readonly float[] Lanes = { -2.5f, 0f, 2.5f };
    private readonly FairnessValidator validator = new FairnessValidator();

    private static FairnessPlayerState Player()
    {
        return new FairnessPlayerState(0f, 0f, 0.5f, -2f, 2f, 18f, 0.12f, 7.5f, 0.14f);
    }

    private bool CanPlace(IReadOnlyList<FairnessObstacleState> active, params FairnessObstacleState[] candidates)
    {
        return PatternFairnessProbe.CanPlaceGroup(validator, Lanes, active, Player(), candidates, 4f, 1f, 1.25f);
    }

    private static List<FairnessObstacleState> FullyBlockedField()
    {
        var active = new List<FairnessObstacleState>();
        for (int lane = 0; lane < 3; lane++)
        {
            active.Add(new FairnessObstacleState(lane, 4.5f, -2f, 8.5f));
            active.Add(new FairnessObstacleState(lane, 4.5f, 0f, 8.5f));
            active.Add(new FairnessObstacleState(lane, 4.5f, 2f, 8.5f));
        }
        return active;
    }

    [Test]
    public void BaselineSingle_InOpenField_IsAllowed()
    {
        Assert.That(CanPlace(new List<FairnessObstacleState>(), new FairnessObstacleState(0, 13.5f, 0f, 8.5f)), Is.True);
    }

    [Test]
    public void DoubleLaneBlock_LeavesAReachableSurvivalAction()
    {
        bool allowed = CanPlace(
            new List<FairnessObstacleState>(),
            new FairnessObstacleState(0, 13.5f, 0f, 8.5f),
            new FairnessObstacleState(1, 13.5f, 0f, 8.5f));

        Assert.That(allowed, Is.True, "Blocking two lanes must still leave the third reachable.");
    }

    [Test]
    public void GroupThatRemovesEveryAction_IsRejected()
    {
        Assert.That(CanPlace(FullyBlockedField(), new FairnessObstacleState(1, 13.5f, 0f, 8.5f)), Is.False);
    }

    [Test]
    public void FairnessIsAppliedToEveryCandidate_NotJustTheFirst()
    {
        // First candidate is fair; the second is inside the reaction window and must sink the group.
        bool allowed = CanPlace(
            new List<FairnessObstacleState>(),
            new FairnessObstacleState(2, 13.5f, 0f, 8.5f),
            new FairnessObstacleState(0, 4.5f, 0f, 8.5f));

        Assert.That(allowed, Is.False);
    }

    [Test]
    public void ScratchReuse_MatchesFreshAllocation()
    {
        var active = new List<FairnessObstacleState>();
        var candidates = new[]
        {
            new FairnessObstacleState(0, 13.5f, 0f, 8.5f),
            new FairnessObstacleState(1, 13.5f, 0f, 8.5f),
        };
        var scratch = new List<FairnessObstacleState>();

        bool withScratch = PatternFairnessProbe.CanPlaceGroup(validator, Lanes, active, Player(), candidates, 4f, 1f, 1.25f, scratch);
        bool fresh = PatternFairnessProbe.CanPlaceGroup(validator, Lanes, active, Player(), candidates, 4f, 1f, 1.25f);

        Assert.That(withScratch, Is.EqualTo(fresh));
    }

    [Test]
    public void EmptyCandidateGroup_IsRejected()
    {
        Assert.That(CanPlace(new List<FairnessObstacleState>()), Is.False);
    }

    [TestCase(0f)]
    [TestCase(60f)]
    [TestCase(120f)]
    public void PatternSimulation_AllDifficultyBands_NeverAcceptImpossibleObstacle(float effectiveAliveTime)
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        GameBalanceConfig balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        TrackLayoutConfig layout = ScriptableObject.CreateInstance<TrackLayoutConfig>();

        PatternSimulationResult result = new PatternSimulation().Run(set, balance, layout, 424242u, 10000, effectiveAliveTime);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Failures, Is.Zero);
        Assert.That(result.ObstaclesSpawned, Is.GreaterThan(0));
    }

    [Test]
    public void PatternSimulation_HighDifficulty_ProducesMultipleFamilies()
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        GameBalanceConfig balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        TrackLayoutConfig layout = ScriptableObject.CreateInstance<TrackLayoutConfig>();

        PatternSimulationResult result = new PatternSimulation().Run(set, balance, layout, 99u, 10000, 120f);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DistinctPatternFamilies, Is.GreaterThanOrEqualTo(3), "High difficulty should exercise several pattern families.");
    }

    [Test]
    public void PatternSimulation_SameSeed_IsDeterministic()
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        GameBalanceConfig balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        TrackLayoutConfig layout = ScriptableObject.CreateInstance<TrackLayoutConfig>();

        PatternSimulationResult first = new PatternSimulation().Run(set, balance, layout, 12321u, 5000, 90f);
        PatternSimulationResult second = new PatternSimulation().Run(set, balance, layout, 12321u, 5000, 90f);

        Assert.That(second.ObstaclesSpawned, Is.EqualTo(first.ObstaclesSpawned));
        Assert.That(second.GroupsAccepted, Is.EqualTo(first.GroupsAccepted));
        Assert.That(second.GroupsRejected, Is.EqualTo(first.GroupsRejected));
        Assert.That(second.AlternatingCount, Is.EqualTo(first.AlternatingCount));
        Assert.That(second.DoubleLaneBlockCount, Is.EqualTo(first.DoubleLaneBlockCount));
        Assert.That(second.StaggeredCount, Is.EqualTo(first.StaggeredCount));
        Assert.That(second.DepthLaneComboCount, Is.EqualTo(first.DepthLaneComboCount));
        Assert.That(first.Failures, Is.Zero);
    }
}
