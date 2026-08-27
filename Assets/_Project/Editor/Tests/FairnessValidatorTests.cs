using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class FairnessValidatorTests
{
    private static readonly float[] Lanes = { -2.5f, 0f, 2.5f };
    private readonly FairnessValidator validator = new FairnessValidator();

    [Test]
    public void ThreeLaneBlock_IsRejectedWhenNoReachableDepthActionExists()
    {
        var active = new List<FairnessObstacleState>();
        AddAllLaneDepthThreats(active);
        FairnessDecision decision = validator.Evaluate(CreateContext(active, new FairnessObstacleState(1, 13.5f, 0f, 8.5f)));

        Assert.That(decision.IsAllowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo(FairnessRejectionReason.NoReachableAction));
    }

    [Test]
    public void CandidateInsideReactionWindow_IsRejected()
    {
        FairnessDecision decision = validator.Evaluate(CreateContext(new List<FairnessObstacleState>(), new FairnessObstacleState(1, 4.5f, 0f, 8.5f)));

        Assert.That(decision.IsAllowed, Is.False);
        Assert.That(decision.Reason, Is.EqualTo(FairnessRejectionReason.LateReaction));
    }

    [Test]
    public void ConsecutiveLaneProtection_RejectsTheSameLane()
    {
        Assert.That(validator.IsImmediateLaneRepeat(1, 1, true), Is.True);
        Assert.That(validator.IsImmediateLaneRepeat(2, 1, true), Is.False);
        Assert.That(validator.IsImmediateLaneRepeat(1, 1, false), Is.False);
    }

    [Test]
    public void ExistingObstacleCombination_RejectsAnUnreachableCandidate()
    {
        var active = new List<FairnessObstacleState>();
        AddAllLaneDepthThreats(active);
        FairnessDecision decision = validator.Evaluate(CreateContext(active, new FairnessObstacleState(0, 13.5f, -1.5f, 8.5f)));

        Assert.That(decision.IsAllowed, Is.False);
    }

    [Test]
    public void SameSeed_ProducesTheSameSimulationResult()
    {
        GameBalanceConfig balance = CreateBalance();
        TrackLayoutConfig layout = CreateLayout();
        var simulation = new FairnessSimulation();

        FairnessSimulationResult first = simulation.Run(balance, layout, 8675309u, 1000, 60f);
        FairnessSimulationResult second = simulation.Run(balance, layout, 8675309u, 1000, 60f);

        Assert.That(second.Accepted, Is.EqualTo(first.Accepted));
        Assert.That(second.Rejected, Is.EqualTo(first.Rejected));
        Assert.That(second.LeftChoices, Is.EqualTo(first.LeftChoices));
        Assert.That(second.CenterChoices, Is.EqualTo(first.CenterChoices));
        Assert.That(second.RightChoices, Is.EqualTo(first.RightChoices));
        Assert.That(first.Failures, Is.Zero);
    }

    [TestCase(0f)]
    [TestCase(60f)]
    [TestCase(120f)]
    public void DifficultyBandSimulation_CompletesWithoutAcceptingFairnessFailures(float effectiveAliveTime)
    {
        FairnessSimulationResult result = new FairnessSimulation().Run(CreateBalance(), CreateLayout(), 424242u, 10000, effectiveAliveTime);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Failures, Is.Zero);
        Assert.That(result.Accepted, Is.GreaterThan(0));
    }

    private static FairnessValidationContext CreateContext(IReadOnlyList<FairnessObstacleState> active, FairnessObstacleState candidate)
    {
        var player = new FairnessPlayerState(0f, 0f, 0.5f, -2f, 2f, 18f, 0.12f, 7.5f, 0.14f);
        return new FairnessValidationContext(Lanes, active, player, candidate, 4f, 1f, 1.25f);
    }

    private static void AddAllLaneDepthThreats(List<FairnessObstacleState> active)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            active.Add(new FairnessObstacleState(lane, 4.5f, -2f, 8.5f));
            active.Add(new FairnessObstacleState(lane, 4.5f, 0f, 8.5f));
            active.Add(new FairnessObstacleState(lane, 4.5f, 2f, 8.5f));
        }
    }

    private static GameBalanceConfig CreateBalance()
    {
        return ScriptableObject.CreateInstance<GameBalanceConfig>();
    }

    private static TrackLayoutConfig CreateLayout()
    {
        return ScriptableObject.CreateInstance<TrackLayoutConfig>();
    }
}
