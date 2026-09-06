using System.Collections.Generic;

/// <summary>
/// A single obstacle the spawner should place this beat. When <see cref="IsBaseline"/>
/// is true the spawner uses its unchanged single-obstacle path (lane heuristics + fairness),
/// otherwise it places the obstacle at <see cref="Lane"/>/<see cref="DepthIndex"/> subject
/// to the fairness authority.
/// </summary>
public readonly struct PatternSpawnRequest
{
    public readonly bool IsBaseline;
    public readonly int Lane;
    public readonly int DepthIndex;

    public PatternSpawnRequest(bool isBaseline, int lane, int depthIndex)
    {
        IsBaseline = isBaseline;
        Lane = lane;
        DepthIndex = depthIndex;
    }

    public static PatternSpawnRequest Baseline => new PatternSpawnRequest(true, -1, -1);
}

/// <summary>
/// Owns pattern cadence: selects a family when idle, emits its beat-0 placements
/// immediately, and schedules later beats so multi-beat families (alternating,
/// staggered) unfold across successive spawn intervals. Pure logic with no Unity
/// dependency, so it is fully unit-testable and deterministic under a seeded source.
/// </summary>
public sealed class PatternDirector
{
    private const int LaneCount = 3;
    private const int RecentHistory = 2;

    private struct ScheduledPlacement
    {
        public int Lane;
        public int DepthIndex;
        public int BeatsUntilSpawn;
    }

    private readonly IReadOnlyList<ObstaclePatternDefinition> patterns;
    private readonly PatternSelector selector;
    private readonly List<ScheduledPlacement> pending = new List<ScheduledPlacement>(6);
    private readonly List<ObstaclePatternType> recent = new List<ObstaclePatternType>(RecentHistory);

    public PatternDirector(ObstaclePatternSet set)
        : this(set != null ? set.Patterns : null, new PatternSelector())
    {
    }

    public PatternDirector(IReadOnlyList<ObstaclePatternDefinition> patterns, PatternSelector selector)
    {
        this.patterns = patterns;
        this.selector = selector ?? new PatternSelector();
    }

    /// <summary>True while a multi-beat pattern is still unfolding.</summary>
    public bool IsMidPattern => pending.Count > 0;

    public ObstaclePatternType LastSelectedType { get; private set; } = ObstaclePatternType.Single;

    /// <summary>
    /// Advances one spawn beat, filling <paramref name="output"/> with the obstacles to place.
    /// While a pattern is unfolding it only drains scheduled placements; when idle it selects
    /// a new family.
    /// </summary>
    public void TickBeat(float difficulty01, IRandomSource random, List<PatternSpawnRequest> output)
    {
        if (output == null)
        {
            return;
        }

        output.Clear();

        bool wasMidPattern = pending.Count > 0;
        DrainPending(output);

        if (wasMidPattern)
        {
            // Never start a new pattern while one is still unfolding.
            return;
        }

        SelectAndSchedule(difficulty01, random, output);
    }

    private void DrainPending(List<PatternSpawnRequest> output)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            ScheduledPlacement placement = pending[i];
            placement.BeatsUntilSpawn -= 1;

            if (placement.BeatsUntilSpawn <= 0)
            {
                output.Add(new PatternSpawnRequest(false, placement.Lane, placement.DepthIndex));
                pending.RemoveAt(i);
            }
            else
            {
                pending[i] = placement;
            }
        }
    }

    private void SelectAndSchedule(float difficulty01, IRandomSource random, List<PatternSpawnRequest> output)
    {
        int index = patterns != null ? selector.Select(patterns, difficulty01, recent, random) : -1;

        if (index < 0)
        {
            EmitBaseline(output);
            return;
        }

        ObstaclePatternDefinition definition = patterns[index];
        RecordRecent(definition.Type);
        LastSelectedType = definition.Type;

        if (definition.Type == ObstaclePatternType.Single || definition.Placements == null || definition.Placements.Length == 0)
        {
            output.Add(PatternSpawnRequest.Baseline);
            return;
        }

        bool mirror = definition.AllowMirror && random != null && random.NextInt(2) == 1;

        for (int i = 0; i < definition.Placements.Length; i++)
        {
            PatternPlacement placement = definition.Placements[i];
            int lane = mirror ? (LaneCount - 1 - placement.Lane) : placement.Lane;
            lane = lane < 0 ? 0 : (lane >= LaneCount ? LaneCount - 1 : lane);

            if (placement.Beat <= 0)
            {
                output.Add(new PatternSpawnRequest(false, lane, placement.DepthIndex));
            }
            else
            {
                pending.Add(new ScheduledPlacement { Lane = lane, DepthIndex = placement.DepthIndex, BeatsUntilSpawn = placement.Beat });
            }
        }

        // A well-formed pattern always begins on beat 0; guard against authored gaps.
        if (output.Count == 0)
        {
            output.Add(PatternSpawnRequest.Baseline);
        }
    }

    private void EmitBaseline(List<PatternSpawnRequest> output)
    {
        RecordRecent(ObstaclePatternType.Single);
        LastSelectedType = ObstaclePatternType.Single;
        output.Add(PatternSpawnRequest.Baseline);
    }

    private void RecordRecent(ObstaclePatternType type)
    {
        recent.Insert(0, type);
        while (recent.Count > RecentHistory)
        {
            recent.RemoveAt(recent.Count - 1);
        }
    }
}
