using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Integration/simulation-level tests for the challenge sequencing layer, reusing the
/// production simulation harness (<see cref="PatternSimulation"/>) exactly as
/// <c>PatternFairnessTests.cs</c> does, now with <see cref="ChallengeDirector"/> wired in.
/// Covers required Tests C, F, H, I, J from the Challenge &amp; Run Variety Expansion phase.
/// See <c>ChallengeDirectorTests.cs</c> for the isolated director-level Tests A, B, D, E, G, K, L.
/// </summary>
public sealed class ChallengeSimulationTests
{
    // Test C: Across every difficulty band -- including a "Maximum" band far beyond the
    // configured max-difficulty duration -- the challenge-integrated simulation never
    // accepts an obstacle without a fairness-validated reachable survival action.
    [TestCase(0f)]
    [TestCase(60f)]
    [TestCase(120f)]
    [TestCase(300f)]
    public void C_ChallengeIntegratedSimulation_NeverAcceptsAnImpossiblePattern(float effectiveAliveTime)
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        GameBalanceConfig balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        TrackLayoutConfig layout = ScriptableObject.CreateInstance<TrackLayoutConfig>();

        PatternSimulationResult result = new PatternSimulation().Run(set, balance, layout, 424242u, 10000, effectiveAliveTime);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Failures, Is.Zero);
        Assert.That(result.ObstaclesSpawned, Is.GreaterThan(0));
    }

    // Test F: Advanced patterns must not appear before their configured difficulty
    // threshold -- PRESSURE must never trigger below its own configured threshold either,
    // so only the baseline family is ever selected in the true early game.
    [Test]
    public void F_BelowPressureThreshold_OnlyBaselineFamilySelected()
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        var director = new PatternDirector(set);
        var challenge = new ChallengeDirector(ChallengeConfig.Default);
        var random = new DeterministicRandom(11u);
        var buffer = new List<PatternSpawnRequest>();

        for (int beat = 0; beat < 300; beat++)
        {
            float effective = challenge.Advance(0f, random);

            Assert.That(challenge.State, Is.EqualTo(ChallengeState.Normal),
                "PRESSURE must never trigger below its configured difficulty threshold.");

            director.TickBeat(effective, random, buffer);
            Assert.That(director.LastSelectedType, Is.EqualTo(ObstaclePatternType.Single));
        }
    }

    // Test H: At the maximum configured speed (and well beyond the duration that produces
    // it), speed clamps correctly and the challenge-integrated simulation remains fair.
    [Test]
    public void H_MaximumConfiguredSpeed_SimulationRemainsFair()
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        GameBalanceConfig balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        TrackLayoutConfig layout = ScriptableObject.CreateInstance<TrackLayoutConfig>();

        float speedFarBeyondCap = balance.GetFallSpeed(9000f);
        Assert.That(speedFarBeyondCap, Is.EqualTo(balance.maxFallSpeed).Within(0.001f),
            "Speed must clamp at the configured maximum and never exceed it.");

        PatternSimulationResult result = new PatternSimulation().Run(set, balance, layout, 424242u, 10000, 9000f);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Failures, Is.Zero);
    }

    // Test I: Expanding challenge/run variety must not eliminate any of the five existing
    // pattern families -- all must still remain available at high difficulty.
    [Test]
    public void I_ChallengeExpansion_DoesNotEliminateAnyExistingFamily()
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        GameBalanceConfig balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        TrackLayoutConfig layout = ScriptableObject.CreateInstance<TrackLayoutConfig>();

        PatternSimulationResult result = new PatternSimulation().Run(set, balance, layout, 555u, 10000, 120f);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.SingleCount, Is.GreaterThan(0));
        Assert.That(result.AlternatingCount, Is.GreaterThan(0));
        Assert.That(result.DoubleLaneBlockCount, Is.GreaterThan(0));
        Assert.That(result.StaggeredCount, Is.GreaterThan(0));
        Assert.That(result.DepthLaneComboCount, Is.GreaterThan(0));
    }

    // Test J (part 1): When a candidate group truly removes every reachable survival
    // action, both the group and every possible single-obstacle fallback candidate are
    // correctly rejected -- the system fails safe (spawns nothing) rather than ever
    // spawning an invalid pattern. Mirrors PatternFairnessTests.FullyBlockedField().
    [Test]
    public void J_FullyBlockedField_RejectsBothTheGroupAndEverySingleFallbackCandidate()
    {
        var validator = new FairnessValidator();
        float[] lanes = { -2.5f, 0f, 2.5f };
        float[] depths = { -2f, 0f, 2f };
        var player = new FairnessPlayerState(0f, 0f, 0.5f, -2f, 2f, 18f, 0.12f, 7.5f, 0.14f);

        var active = new List<FairnessObstacleState>();
        for (int lane = 0; lane < 3; lane++)
        {
            for (int d = 0; d < depths.Length; d++)
            {
                active.Add(new FairnessObstacleState(lane, 4.5f, depths[d], 8.5f));
            }
        }

        var rejectedGroup = new[]
        {
            new FairnessObstacleState(1, 13.5f, 0f, 8.5f),
            new FairnessObstacleState(0, 13.5f, -2f, 8.5f),
        };

        Assert.That(
            PatternFairnessProbe.CanPlaceGroup(validator, lanes, active, player, rejectedGroup, 4f, 1f, 1.25f),
            Is.False,
            "Test setup must actually exercise a rejected group.");

        for (int lane = 0; lane < 3; lane++)
        {
            for (int d = 0; d < depths.Length; d++)
            {
                var fallbackCandidate = new[] { new FairnessObstacleState(lane, 13.5f, depths[d], 8.5f) };
                bool fallbackAllowed = PatternFairnessProbe.CanPlaceGroup(validator, lanes, active, player, fallbackCandidate, 4f, 1f, 1.25f);
                Assert.That(fallbackAllowed, Is.False,
                    $"With every reachable action already removed, no single fallback spawn at lane {lane} depth {depths[d]} should be accepted either.");
            }
        }
    }

    // Test J (part 2): In ordinary (non-adversarial) play, rejected-group fallback
    // successes can never exceed rejection attempts, and a rejected group never turns
    // into a recorded failure -- the fallback path stays inside its own safety contract.
    [Test]
    public void J_Fallback_NeverExceedsRejectedGroupAttempts_AndNeverProducesAFailure()
    {
        ObstaclePatternSet set = ObstaclePatternSet.CreateDefault();
        GameBalanceConfig balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        TrackLayoutConfig layout = ScriptableObject.CreateInstance<TrackLayoutConfig>();

        PatternSimulationResult result = new PatternSimulation().Run(set, balance, layout, 424242u, 10000, 120f);

        Assert.That(result.FallbackSuccessCount, Is.LessThanOrEqualTo(result.GroupsRejected));
        Assert.That(result.Failures, Is.Zero);
    }
}
