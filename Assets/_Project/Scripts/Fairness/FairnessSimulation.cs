using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development-only deterministic scenario runner. It never drives normal gameplay random state.
/// </summary>
public sealed class FairnessSimulation
{
    private readonly FairnessValidator validator = new FairnessValidator();
    private readonly List<FairnessObstacleState> activeObstacles = new List<FairnessObstacleState>(12);
    private readonly int[] laneChoiceCounts = new int[3];

    public FairnessSimulationResult Run(GameBalanceConfig balance, TrackLayoutConfig layout, uint seed, int scenarioCount, float effectiveAliveTime)
    {
        if (!balance || !layout || !layout.IsValid(out _) || scenarioCount <= 0)
        {
            return FairnessSimulationResult.Invalid;
        }

        var random = new DeterministicRandom(seed);
        float[] lanes = layout.CopyLanePositions();
        float[] depths = layout.CopyDepthOffsets();
        float interval = balance.GetSpawnInterval(effectiveAliveTime);
        float speed = balance.GetFallSpeed(effectiveAliveTime);
        float depthVariation = Mathf.Lerp(balance.startingDepthVariation, 1f, balance.GetDifficultyProgress(effectiveAliveTime));
        var player = new FairnessPlayerState(0f, 0f, 0.5f, -layout.SafeDepthRange, layout.SafeDepthRange, 18f, 0.12f, 7.5f, 0.14f);

        activeObstacles.Clear();
        System.Array.Clear(laneChoiceCounts, 0, laneChoiceCounts.Length);

        int accepted = 0;
        int rejected = 0;
        int failures = 0;

        for (int scenario = 0; scenario < scenarioCount; scenario++)
        {
            AdvanceActiveObstacles(interval);

            int lane = random.NextInt(lanes.Length);
            float depth = depths[random.NextInt(depths.Length)] * depthVariation;
            var candidate = new FairnessObstacleState(lane, balance.spawnHeight, depth, speed);
            var context = new FairnessValidationContext(lanes, activeObstacles, player, candidate, balance.dangerRange, balance.minimumReactionSeconds, balance.minimumDepthSeparation);
            FairnessDecision decision = validator.Evaluate(context);

            if (!decision.IsAllowed)
            {
                rejected++;
                continue;
            }

            // Every accepted candidate is immediately re-evaluated before being recorded.
            // This makes a simulation failure a direct contract breach rather than a visual inference.
            if (!validator.Evaluate(context).IsAllowed)
            {
                failures++;
                continue;
            }

            accepted++;
            laneChoiceCounts[lane]++;
            activeObstacles.Add(candidate);
        }

        return new FairnessSimulationResult(scenarioCount, accepted, rejected, failures, laneChoiceCounts[0], laneChoiceCounts[1], laneChoiceCounts[2]);
    }

    private void AdvanceActiveObstacles(float seconds)
    {
        for (int i = activeObstacles.Count - 1; i >= 0; i--)
        {
            FairnessObstacleState obstacle = activeObstacles[i];
            float nextHeight = obstacle.Height - obstacle.Speed * seconds;

            if (nextHeight < -1f)
            {
                activeObstacles.RemoveAt(i);
                continue;
            }

            activeObstacles[i] = new FairnessObstacleState(obstacle.LaneIndex, nextHeight, obstacle.Depth, obstacle.Speed);
        }
    }
}

public readonly struct FairnessSimulationResult
{
    public static FairnessSimulationResult Invalid => new FairnessSimulationResult(0, 0, 0, 1, 0, 0, 0);

    public readonly int Scenarios;
    public readonly int Accepted;
    public readonly int Rejected;
    public readonly int Failures;
    public readonly int LeftChoices;
    public readonly int CenterChoices;
    public readonly int RightChoices;

    public FairnessSimulationResult(int scenarios, int accepted, int rejected, int failures, int leftChoices, int centerChoices, int rightChoices)
    {
        Scenarios = scenarios;
        Accepted = accepted;
        Rejected = rejected;
        Failures = failures;
        LeftChoices = leftChoices;
        CenterChoices = centerChoices;
        RightChoices = rightChoices;
    }

    public bool IsValid => Scenarios > 0 && Accepted + Rejected == Scenarios && Failures == 0;
}
