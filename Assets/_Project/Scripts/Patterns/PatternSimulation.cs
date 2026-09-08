using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development-only deterministic pattern sampler. It reuses the exact production
/// pieces (<see cref="PatternDirector"/>, <see cref="ChallengeDirector"/>,
/// <see cref="PatternFairnessProbe"/> and the single <see cref="FairnessValidator"/>
/// authority) to confirm that, across many beats and every difficulty band, no obstacle
/// is ever added without a validated reachable survival action -- and that the challenge
/// sequencing layer never changes that guarantee. It never drives normal gameplay random state.
/// </summary>
public sealed class PatternSimulation
{
    private readonly FairnessValidator validator = new FairnessValidator();
    private readonly List<FairnessObstacleState> active = new List<FairnessObstacleState>(16);
    private readonly List<FairnessObstacleState> candidates = new List<FairnessObstacleState>(4);
    private readonly List<FairnessObstacleState> scratch = new List<FairnessObstacleState>(24);
    private readonly List<PatternSpawnRequest> requests = new List<PatternSpawnRequest>(4);
    private float spawnHeight = 13.5f;

    public PatternSimulationResult Run(ObstaclePatternSet set, GameBalanceConfig balance, TrackLayoutConfig layout, uint seed, int beats, float effectiveAliveTime)
    {
        if (!set || !balance || !layout || !layout.IsValid(out _) || beats <= 0)
        {
            return PatternSimulationResult.Invalid;
        }

        var random = new DeterministicRandom(seed);
        var director = new PatternDirector(set);
        var challenge = new ChallengeDirector(balance.GetChallengeConfig());
        float[] lanes = layout.CopyLanePositions();
        float[] depths = layout.CopyDepthOffsets();
        float interval = balance.GetSpawnInterval(effectiveAliveTime);
        float speed = balance.GetFallSpeed(effectiveAliveTime);
        float progress = balance.GetDifficultyProgress(effectiveAliveTime);
        float variation = Mathf.Lerp(balance.startingDepthVariation, 1f, progress);
        spawnHeight = balance.spawnHeight;
        var player = new FairnessPlayerState(0f, 0f, 0.5f, -layout.SafeDepthRange, layout.SafeDepthRange, 18f, 0.12f, 7.5f, 0.14f);

        active.Clear();

        int spawned = 0;
        int groupsAccepted = 0;
        int groupsRejected = 0;
        int fallbackSuccesses = 0;
        int failures = 0;
        var typeCounts = new int[5];

        int normalBeats = 0;
        int pressureBeats = 0;
        int recoveryBeats = 0;
        int immediateRepeats = 0;
        int previousType = -1;
        int previousPreviousType = -1;
        var transitionKeys = new HashSet<int>();
        var tripleKeys = new HashSet<int>();

        for (int beat = 0; beat < beats; beat++)
        {
            AdvanceActive(interval);

            bool wasMidPattern = director.IsMidPattern;

            // The challenge layer only reshapes the same difficulty01 signal PatternDirector
            // already gates and weights patterns on; it never touches fairness, speed, or interval.
            float effectiveDifficulty = challenge.Advance(progress, random);

            switch (challenge.State)
            {
                case ChallengeState.Pressure: pressureBeats++; break;
                case ChallengeState.Recovery: recoveryBeats++; break;
                default: normalBeats++; break;
            }

            director.TickBeat(effectiveDifficulty, random, requests);

            if (!wasMidPattern)
            {
                int currentType = (int)director.LastSelectedType;

                if (previousType >= 0)
                {
                    transitionKeys.Add(previousType * 5 + currentType);
                    if (currentType == previousType)
                    {
                        immediateRepeats++;
                    }
                }

                if (previousType >= 0 && previousPreviousType >= 0)
                {
                    tripleKeys.Add((previousPreviousType * 5 + previousType) * 5 + currentType);
                }

                previousPreviousType = previousType;
                previousType = currentType;
            }

            if (requests.Count == 0)
            {
                continue;
            }

            bool baseline = requests.Count == 1 && requests[0].IsBaseline;
            BuildCandidates(baseline, lanes, depths, variation, speed, random);

            if (candidates.Count == 0)
            {
                continue;
            }

            bool valid = PatternFairnessProbe.CanPlaceGroup(validator, lanes, active, player, candidates, balance.dangerRange, balance.minimumReactionSeconds, balance.minimumDepthSeparation, scratch);

            if (!valid)
            {
                groupsRejected++;
                // Reject the candidate group and fall back to a single validated spawn.
                if (TrySingleFallback(lanes, depths, variation, speed, random, player, balance, ref spawned, ref failures, typeCounts))
                {
                    fallbackSuccesses++;
                }
                continue;
            }

            // Contract re-check: a validated group must still validate before it is recorded.
            if (!PatternFairnessProbe.CanPlaceGroup(validator, lanes, active, player, candidates, balance.dangerRange, balance.minimumReactionSeconds, balance.minimumDepthSeparation, scratch))
            {
                failures++;
                continue;
            }

            groupsAccepted++;
            typeCounts[(int)director.LastSelectedType] += 1;

            for (int i = 0; i < candidates.Count; i++)
            {
                active.Add(candidates[i]);
                spawned++;
            }
        }

        return new PatternSimulationResult(
            beats, spawned, groupsAccepted, groupsRejected, failures, typeCounts,
            normalBeats, pressureBeats, recoveryBeats,
            fallbackSuccesses, transitionKeys.Count, tripleKeys.Count, immediateRepeats);
    }

    private void BuildCandidates(bool baseline, float[] lanes, float[] depths, float variation, float speed, DeterministicRandom random)
    {
        candidates.Clear();

        if (baseline)
        {
            int lane = random.NextInt(lanes.Length);
            float depth = depths[random.NextInt(depths.Length)] * variation;
            candidates.Add(new FairnessObstacleState(lane, spawnHeight, depth, speed));
            return;
        }

        for (int i = 0; i < requests.Count; i++)
        {
            PatternSpawnRequest request = requests[i];
            int lane = Mathf.Clamp(request.Lane, 0, lanes.Length - 1);
            int depthIndex = request.DepthIndex >= 0 && request.DepthIndex < depths.Length ? request.DepthIndex : random.NextInt(depths.Length);
            float depth = depths[depthIndex] * variation;
            candidates.Add(new FairnessObstacleState(lane, spawnHeight, depth, speed));
        }
    }

    private bool TrySingleFallback(float[] lanes, float[] depths, float variation, float speed, DeterministicRandom random, in FairnessPlayerState player, GameBalanceConfig balance, ref int spawned, ref int failures, int[] typeCounts)
    {
        int lane = random.NextInt(lanes.Length);
        float depth = depths[random.NextInt(depths.Length)] * variation;
        candidates.Clear();
        candidates.Add(new FairnessObstacleState(lane, spawnHeight, depth, speed));

        if (!PatternFairnessProbe.CanPlaceGroup(validator, lanes, active, player, candidates, balance.dangerRange, balance.minimumReactionSeconds, balance.minimumDepthSeparation, scratch))
        {
            // No fair single spawn this beat; nothing is placed.
            return false;
        }

        typeCounts[(int)ObstaclePatternType.Single] += 1;
        active.Add(candidates[0]);
        spawned++;
        return true;
    }

    private void AdvanceActive(float seconds)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            FairnessObstacleState obstacle = active[i];
            float nextHeight = obstacle.Height - obstacle.Speed * seconds;

            if (nextHeight < -1f)
            {
                active.RemoveAt(i);
                continue;
            }

            active[i] = new FairnessObstacleState(obstacle.LaneIndex, nextHeight, obstacle.Depth, obstacle.Speed);
        }
    }
}

public readonly struct PatternSimulationResult
{
    public static PatternSimulationResult Invalid => new PatternSimulationResult(0, 0, 0, 0, 1, new int[5], 0, 0, 0, 0, 0, 0, 0);

    public readonly int Beats;
    public readonly int ObstaclesSpawned;
    public readonly int GroupsAccepted;
    public readonly int GroupsRejected;
    public readonly int Failures;
    public readonly int SingleCount;
    public readonly int AlternatingCount;
    public readonly int DoubleLaneBlockCount;
    public readonly int StaggeredCount;
    public readonly int DepthLaneComboCount;

    /// <summary>Beats where the challenge sequencing layer reported NORMAL/PRESSURE/RECOVERY.</summary>
    public readonly int ChallengeNormalBeats;
    public readonly int ChallengePressureBeats;
    public readonly int ChallengeRecoveryBeats;

    /// <summary>Number of rejected candidate groups that still produced a safe single-spawn fallback.</summary>
    public readonly int FallbackSuccessCount;

    /// <summary>Distinct (previous-family, selected-family) transitions observed across selection events.</summary>
    public readonly int UniqueTransitionCount;

    /// <summary>Distinct 3-selection family sequences observed (measures sequence-level variety).</summary>
    public readonly int UniqueTripleSequenceCount;

    /// <summary>Selection events where the newly selected family equals the immediately preceding one.</summary>
    public readonly int ImmediateRepeatCount;

    public PatternSimulationResult(
        int beats, int obstaclesSpawned, int groupsAccepted, int groupsRejected, int failures, int[] typeCounts,
        int challengeNormalBeats = 0, int challengePressureBeats = 0, int challengeRecoveryBeats = 0,
        int fallbackSuccessCount = 0, int uniqueTransitionCount = 0, int uniqueTripleSequenceCount = 0, int immediateRepeatCount = 0)
    {
        Beats = beats;
        ObstaclesSpawned = obstaclesSpawned;
        GroupsAccepted = groupsAccepted;
        GroupsRejected = groupsRejected;
        Failures = failures;
        SingleCount = typeCounts != null && typeCounts.Length > 0 ? typeCounts[0] : 0;
        AlternatingCount = typeCounts != null && typeCounts.Length > 1 ? typeCounts[1] : 0;
        DoubleLaneBlockCount = typeCounts != null && typeCounts.Length > 2 ? typeCounts[2] : 0;
        StaggeredCount = typeCounts != null && typeCounts.Length > 3 ? typeCounts[3] : 0;
        DepthLaneComboCount = typeCounts != null && typeCounts.Length > 4 ? typeCounts[4] : 0;
        ChallengeNormalBeats = challengeNormalBeats;
        ChallengePressureBeats = challengePressureBeats;
        ChallengeRecoveryBeats = challengeRecoveryBeats;
        FallbackSuccessCount = fallbackSuccessCount;
        UniqueTransitionCount = uniqueTransitionCount;
        UniqueTripleSequenceCount = uniqueTripleSequenceCount;
        ImmediateRepeatCount = immediateRepeatCount;
    }

    /// <summary>A run is valid only when no contract failure occurred and work was done.</summary>
    public bool IsValid => Beats > 0 && Failures == 0;

    /// <summary>Number of distinct non-baseline families that actually appeared.</summary>
    public int DistinctPatternFamilies
    {
        get
        {
            int count = 0;
            if (AlternatingCount > 0) count++;
            if (DoubleLaneBlockCount > 0) count++;
            if (StaggeredCount > 0) count++;
            if (DepthLaneComboCount > 0) count++;
            return count;
        }
    }
}
