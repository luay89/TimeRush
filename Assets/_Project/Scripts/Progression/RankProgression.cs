using UnityEngine;

/// <summary>
/// Pure helper that turns the existing rank ladder into next-rank progression facts. It never changes
/// rank thresholds; it only reads <see cref="ProgressionConfig.ResolveRank(int)"/> and derives how far
/// the player is from the next rank. The top rank is reported cleanly as MAX RANK.
/// </summary>
public readonly struct RankProgressInfo
{
    public readonly int RankIndex;
    public readonly string CurrentRankName;
    public readonly string NextRankName;
    public readonly bool IsMaxRank;
    public readonly int CurrentThreshold;
    public readonly int NextThreshold;
    public readonly int PointsToNext;

    /// <summary>Fraction (0..1) of the way from the current threshold to the next; 1 at MAX RANK.</summary>
    public readonly float Progress01;

    public RankProgressInfo(
        int rankIndex,
        string currentRankName,
        string nextRankName,
        bool isMaxRank,
        int currentThreshold,
        int nextThreshold,
        int pointsToNext,
        float progress01)
    {
        RankIndex = rankIndex;
        CurrentRankName = currentRankName;
        NextRankName = nextRankName;
        IsMaxRank = isMaxRank;
        CurrentThreshold = currentThreshold;
        NextThreshold = nextThreshold;
        PointsToNext = pointsToNext;
        Progress01 = progress01;
    }
}

/// <summary>Derives <see cref="RankProgressInfo"/> from a <see cref="ProgressionConfig"/> and a best score.</summary>
public static class RankProgression
{
    public static RankProgressInfo Evaluate(ProgressionConfig config, int bestScore)
    {
        int safeBest = bestScore < 0 ? 0 : bestScore;

        if (config == null)
        {
            return new RankProgressInfo(0, "—", string.Empty, true, 0, 0, 0, 1f);
        }

        ProgressionConfig.RankResult rank = config.ResolveRank(safeBest);

        if (!rank.hasNext)
        {
            return new RankProgressInfo(
                rank.index,
                Name(rank.name),
                string.Empty,
                true,
                rank.currentThreshold,
                rank.currentThreshold,
                0,
                1f);
        }

        // The next tier's name is the rank resolved exactly at the next threshold.
        string nextName = Name(config.ResolveRank(rank.nextThreshold).name);
        int span = rank.nextThreshold - rank.currentThreshold;
        int done = safeBest - rank.currentThreshold;
        float progress = span <= 0 ? 1f : Mathf.Clamp01((float)done / span);
        int pointsToNext = Mathf.Max(0, rank.nextThreshold - safeBest);

        return new RankProgressInfo(
            rank.index,
            Name(rank.name),
            nextName,
            false,
            rank.currentThreshold,
            rank.nextThreshold,
            pointsToNext,
            progress);
    }

    private static string Name(string value)
    {
        return string.IsNullOrEmpty(value) ? "—" : value;
    }
}
