#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic-only tool for the Phase K Step 5 audit. Reuses the exact
/// production pattern/fairness pipeline (PatternDirector, ChallengeDirector,
/// PatternFairnessProbe, FairnessValidator) to measure whether a player who never
/// moves from lane=center/depth=0 would ever be geometrically hit. Not part of the
/// shipped game; delete after use.
/// </summary>
public static class StationaryCenterAudit
{
    private const string BalancePath = "Assets/_Project/Config/GameBalanceConfig.asset";
    private const string LayoutPath = "Assets/_Project/Config/TrackLayoutConfig.asset";
    private const string PatternSetPath = "Assets/_Project/Resources/ObstaclePatternSet.asset";
    private const uint Seed = 424242u;
    private const float CombinedHalfExtent = 1.0f; // 0.5 (player box) + 0.5 (obstacle box)

    [MenuItem("TimeRush/Validation/Run Stationary Center Audit")]
    public static void Run()
    {
        GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
        TrackLayoutConfig layout = AssetDatabase.LoadAssetAtPath<TrackLayoutConfig>(LayoutPath);
        ObstaclePatternSet set = AssetDatabase.LoadAssetAtPath<ObstaclePatternSet>(PatternSetPath);
        if (!set)
        {
            set = ObstaclePatternSet.CreateDefault();
        }

        if (!balance || !layout)
        {
            Debug.LogError("StationaryCenterAudit: Required TimeRush config assets are missing.");
            return;
        }

        RunSnapshotBand("Early", 0f, set, balance, layout, 5000);
        RunSnapshotBand("Medium", 60f, set, balance, layout, 5000);
        RunSnapshotBand("High", 120f, set, balance, layout, 5000);
        RunSnapshotBand("Maximum", 300f, set, balance, layout, 5000);

        RunTimeline(90f, set, balance, layout);
        RunTimeline(180f, set, balance, layout);
    }

    private static void RunSnapshotBand(string label, float aliveTime, ObstaclePatternSet set, GameBalanceConfig balance, TrackLayoutConfig layout, int beats)
    {
        AuditResult result = Simulate(set, balance, layout, Seed, beats, aliveTime, continuous: false, maxSeconds: 0f);
        Log(label, result);
    }

    private static void RunTimeline(float maxSeconds, ObstaclePatternSet set, GameBalanceConfig balance, TrackLayoutConfig layout)
    {
        AuditResult result = Simulate(set, balance, layout, Seed, int.MaxValue, 0f, continuous: true, maxSeconds: maxSeconds);
        Log($"Timeline0-{maxSeconds}s", result);
    }

    private struct AuditResult
    {
        public int Beats;
        public int Spawned;
        public int Hits;
        public int FirstHitBeat;
        public float FirstHitTime;
        public int MaxSafeStreak;
        public int LaneLeft, LaneCenter, LaneRight;
        public int[] TypeCounts;
        public int GroupsRejected;
        public int FallbackSuccess;
        public float ElapsedSeconds;
    }

    private static AuditResult Simulate(ObstaclePatternSet set, GameBalanceConfig balance, TrackLayoutConfig layout, uint seed, int beats, float fixedAliveTime, bool continuous, float maxSeconds)
    {
        var validator = new FairnessValidator();
        var active = new List<FairnessObstacleState>(16);
        var candidates = new List<FairnessObstacleState>(4);
        var scratch = new List<FairnessObstacleState>(24);
        var requests = new List<PatternSpawnRequest>(4);

        var random = new DeterministicRandom(seed);
        var director = new PatternDirector(set);
        var challenge = new ChallengeDirector(balance.GetChallengeConfig());
        float[] lanes = layout.CopyLanePositions();
        float[] depths = layout.CopyDepthOffsets();
        float spawnHeight = balance.spawnHeight;

        int centerLaneIndex = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < lanes.Length; i++)
        {
            float dist = Mathf.Abs(lanes[i]);
            if (dist < bestDist) { bestDist = dist; centerLaneIndex = i; }
        }

        const float playerDepth = 0f;
        var player = new FairnessPlayerState(lanes[centerLaneIndex], playerDepth, 0.5f, -layout.SafeDepthRange, layout.SafeDepthRange, 18f, 0.12f, 7.5f, 0.14f);

        int spawned = 0, hits = 0, firstHitBeat = -1, groupsRejected = 0, fallbackSuccess = 0;
        float firstHitTime = -1f;
        int laneLeft = 0, laneCenter = 0, laneRight = 0;
        int currentStreak = 0, maxStreak = 0;
        var typeCounts = new int[5];
        float elapsed = 0f;
        int b = 0;

        while (continuous ? elapsed < maxSeconds : b < beats)
        {
            float effectiveAliveTime = continuous ? elapsed : fixedAliveTime;
            float interval = balance.GetSpawnInterval(effectiveAliveTime);
            float speed = balance.GetFallSpeed(effectiveAliveTime);
            float progress = balance.GetDifficultyProgress(effectiveAliveTime);
            float variation = Mathf.Lerp(balance.startingDepthVariation, 1f, progress);

            AdvanceActive(active, interval);

            float effectiveDifficulty = challenge.Advance(progress, random);
            director.TickBeat(effectiveDifficulty, random, requests);

            if (requests.Count > 0)
            {
                bool baseline = requests.Count == 1 && requests[0].IsBaseline;
                BuildCandidates(baseline, requests, lanes, depths, variation, speed, spawnHeight, random, candidates);

                if (candidates.Count > 0)
                {
                    bool valid = PatternFairnessProbe.CanPlaceGroup(validator, lanes, active, player, candidates, balance.dangerRange, balance.minimumReactionSeconds, balance.minimumDepthSeparation, scratch);
                    bool committed = false;
                    int committedFamily = -1;

                    if (!valid)
                    {
                        groupsRejected++;
                        if (TrySingleFallback(validator, lanes, depths, variation, speed, spawnHeight, random, player, balance, active, candidates, scratch, ref spawned))
                        {
                            fallbackSuccess++;
                            committed = true;
                            committedFamily = (int)ObstaclePatternType.Single;
                        }
                    }
                    else
                    {
                        committedFamily = (int)director.LastSelectedType;
                        for (int i = 0; i < candidates.Count; i++)
                        {
                            active.Add(candidates[i]);
                            spawned++;
                        }
                        committed = true;
                    }

                    if (committed)
                    {
                        typeCounts[committedFamily]++;
                        bool anyHitThisEvent = false;

                        for (int i = 0; i < candidates.Count; i++)
                        {
                            FairnessObstacleState c = candidates[i];
                            if (c.LaneIndex == centerLaneIndex)
                            {
                                laneCenter++;
                                if (Mathf.Abs(c.Depth - playerDepth) < CombinedHalfExtent)
                                {
                                    anyHitThisEvent = true;
                                }
                            }
                            else if (lanes[c.LaneIndex] < lanes[centerLaneIndex])
                            {
                                laneLeft++;
                            }
                            else
                            {
                                laneRight++;
                            }
                        }

                        if (anyHitThisEvent)
                        {
                            hits++;
                            if (firstHitBeat < 0) { firstHitBeat = b; firstHitTime = elapsed; }
                            if (currentStreak > maxStreak) maxStreak = currentStreak;
                            currentStreak = 0;
                        }
                        else
                        {
                            currentStreak++;
                        }
                    }
                }
            }

            elapsed += interval;
            b++;
        }

        if (currentStreak > maxStreak) maxStreak = currentStreak;

        return new AuditResult
        {
            Beats = b,
            Spawned = spawned,
            Hits = hits,
            FirstHitBeat = firstHitBeat,
            FirstHitTime = firstHitTime,
            MaxSafeStreak = maxStreak,
            LaneLeft = laneLeft,
            LaneCenter = laneCenter,
            LaneRight = laneRight,
            TypeCounts = typeCounts,
            GroupsRejected = groupsRejected,
            FallbackSuccess = fallbackSuccess,
            ElapsedSeconds = elapsed
        };
    }

    private static void BuildCandidates(bool baseline, List<PatternSpawnRequest> requests, float[] lanes, float[] depths, float variation, float speed, float spawnHeight, DeterministicRandom random, List<FairnessObstacleState> candidates)
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

    private static bool TrySingleFallback(FairnessValidator validator, float[] lanes, float[] depths, float variation, float speed, float spawnHeight, DeterministicRandom random, in FairnessPlayerState player, GameBalanceConfig balance, List<FairnessObstacleState> active, List<FairnessObstacleState> candidates, List<FairnessObstacleState> scratch, ref int spawned)
    {
        int lane = random.NextInt(lanes.Length);
        float depth = depths[random.NextInt(depths.Length)] * variation;
        candidates.Clear();
        candidates.Add(new FairnessObstacleState(lane, spawnHeight, depth, speed));

        if (!PatternFairnessProbe.CanPlaceGroup(validator, lanes, active, player, candidates, balance.dangerRange, balance.minimumReactionSeconds, balance.minimumDepthSeparation, scratch))
        {
            candidates.Clear();
            return false;
        }

        active.Add(candidates[0]);
        spawned++;
        return true;
    }

    private static void AdvanceActive(List<FairnessObstacleState> active, float seconds)
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

    private static void Log(string label, AuditResult r)
    {
        Debug.Log($"[StationaryCenterAudit:{label}] beats={r.Beats} elapsed={r.ElapsedSeconds:F1}s spawned={r.Spawned} HITS={r.Hits} firstHitBeat={r.FirstHitBeat} firstHitTime={r.FirstHitTime:F2}s maxSafeStreak={r.MaxSafeStreak} lanes=L{r.LaneLeft}/C{r.LaneCenter}/R{r.LaneRight} families=Sgl{r.TypeCounts[0]}/Alt{r.TypeCounts[1]}/Dbl{r.TypeCounts[2]}/Stg{r.TypeCounts[3]}/Dpt{r.TypeCounts[4]} rejected={r.GroupsRejected} fallbackOk={r.FallbackSuccess}");
    }
}
#endif
