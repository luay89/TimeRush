using System.Collections.Generic;

/// <summary>
/// Pure, deterministic pattern selection. Given the current difficulty, a short
/// history of recently used families, and a random source, it picks one eligible
/// pattern using difficulty gating, weighted probability, and immediate-repetition
/// avoidance. Eligibility always wins over anti-repetition, and difficulty gating
/// always wins over both, so the selector can never manufacture an unfair choice.
/// </summary>
public sealed class PatternSelector
{
    /// <summary>
    /// Returns the index of the selected pattern, or -1 when nothing is eligible
    /// (the caller then falls back to a baseline single spawn).
    /// </summary>
    public int Select(IReadOnlyList<ObstaclePatternDefinition> patterns, float difficulty01, IReadOnlyList<ObstaclePatternType> recent, IRandomSource random)
    {
        if (patterns == null || patterns.Count == 0 || random == null)
        {
            return -1;
        }

        // First pass excludes recently used families; if that removes every option,
        // the second pass ignores the history so a valid pattern is still chosen.
        int picked = WeightedPick(patterns, difficulty01, recent, random, true);
        if (picked >= 0)
        {
            return picked;
        }

        return WeightedPick(patterns, difficulty01, recent, random, false);
    }

    private static int WeightedPick(IReadOnlyList<ObstaclePatternDefinition> patterns, float difficulty01, IReadOnlyList<ObstaclePatternType> recent, IRandomSource random, bool excludeRecent)
    {
        float total = 0f;

        for (int i = 0; i < patterns.Count; i++)
        {
            if (IsEligible(patterns[i], difficulty01, recent, excludeRecent))
            {
                total += patterns[i].Weight;
            }
        }

        if (total <= 0f)
        {
            return -1;
        }

        float roll = random.NextFloat() * total;
        float cumulative = 0f;

        for (int i = 0; i < patterns.Count; i++)
        {
            if (!IsEligible(patterns[i], difficulty01, recent, excludeRecent))
            {
                continue;
            }

            cumulative += patterns[i].Weight;
            if (roll < cumulative)
            {
                return i;
            }
        }

        // Floating-point guard: return the last eligible index.
        for (int i = patterns.Count - 1; i >= 0; i--)
        {
            if (IsEligible(patterns[i], difficulty01, recent, excludeRecent))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsEligible(in ObstaclePatternDefinition definition, float difficulty01, IReadOnlyList<ObstaclePatternType> recent, bool excludeRecent)
    {
        if (definition.Weight <= 0f || definition.MinDifficulty > difficulty01)
        {
            return false;
        }

        if (excludeRecent && recent != null)
        {
            for (int i = 0; i < recent.Count; i++)
            {
                if (recent[i] == definition.Type)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
