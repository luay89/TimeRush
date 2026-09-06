using System;
using UnityEngine;

/// <summary>
/// ScriptableObject configuration for the cross-run rank ladder. Rank is a
/// read-only reflection of the player's best score; it never affects gameplay,
/// difficulty, scoring, or fairness. Pure resolution keeps it testable.
/// </summary>
[CreateAssetMenu(fileName = "ProgressionConfig", menuName = "TimeRush/Progression Config")]
public sealed class ProgressionConfig : ScriptableObject
{
    [Serializable]
    public struct RankTier
    {
        public string name;

        [Min(0)]
        public int bestScoreThreshold;
    }

    public readonly struct RankResult
    {
        public readonly int index;
        public readonly string name;
        public readonly int currentThreshold;
        public readonly bool hasNext;
        public readonly int nextThreshold;

        public RankResult(int index, string name, int currentThreshold, bool hasNext, int nextThreshold)
        {
            this.index = index;
            this.name = name;
            this.currentThreshold = currentThreshold;
            this.hasNext = hasNext;
            this.nextThreshold = nextThreshold;
        }
    }

    [Tooltip("Rank tiers ordered ascending by threshold. The first tier should use threshold 0.")]
    [SerializeField]
    private RankTier[] tiers = DefaultTiers();

    /// <summary>
    /// Resolves the highest tier whose threshold is at or below the supplied best score.
    /// Falls back to a neutral placeholder when no tiers are configured.
    /// </summary>
    public RankResult ResolveRank(int bestScore)
    {
        int clampedBest = bestScore < 0 ? 0 : bestScore;

        if (tiers == null || tiers.Length == 0)
        {
            return new RankResult(0, "—", 0, false, 0);
        }

        int resolvedIndex = 0;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (clampedBest >= tiers[i].bestScoreThreshold)
            {
                resolvedIndex = i;
            }
        }

        bool hasNext = resolvedIndex < tiers.Length - 1;
        int nextThreshold = hasNext ? tiers[resolvedIndex + 1].bestScoreThreshold : 0;
        string name = string.IsNullOrEmpty(tiers[resolvedIndex].name) ? "—" : tiers[resolvedIndex].name;

        return new RankResult(resolvedIndex, name, tiers[resolvedIndex].bestScoreThreshold, hasNext, nextThreshold);
    }

    /// <summary>Provides a runtime fallback when no configured asset is available.</summary>
    public static ProgressionConfig CreateDefault()
    {
        var config = CreateInstance<ProgressionConfig>();
        config.tiers = DefaultTiers();
        return config;
    }

    private static RankTier[] DefaultTiers()
    {
        return new[]
        {
            new RankTier { name = "ROOKIE", bestScoreThreshold = 0 },
            new RankTier { name = "RUNNER", bestScoreThreshold = 500 },
            new RankTier { name = "DODGER", bestScoreThreshold = 1500 },
            new RankTier { name = "VETERAN", bestScoreThreshold = 3500 },
            new RankTier { name = "ACE", bestScoreThreshold = 7000 },
            new RankTier { name = "MASTER", bestScoreThreshold = 12000 },
        };
    }
}
