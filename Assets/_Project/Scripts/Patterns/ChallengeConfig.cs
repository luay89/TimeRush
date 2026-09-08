/// <summary>
/// Pure, immutable tuning for the challenge sequencing layer (<see cref="ChallengeDirector"/>).
/// Every field is clamped in the constructor to a range that can never produce invalid
/// director behavior (no negative durations, no out-of-range probabilities/difficulty
/// deltas), so a misconfigured asset always degrades to a safe, valid configuration instead
/// of throwing or producing NaN/negative results.
/// </summary>
public readonly struct ChallengeConfig
{
    /// <summary>Raw difficulty01 must be at or above this before a PRESSURE window may start.</summary>
    public readonly float MinDifficultyForPressure;

    /// <summary>Per-beat probability of entering PRESSURE once eligible and off cooldown.</summary>
    public readonly float PressureChance;

    /// <summary>Added to the difficulty signal fed to the pattern selector while in PRESSURE.</summary>
    public readonly float PressureDifficultyBoost;

    /// <summary>Subtracted from the difficulty signal fed to the pattern selector while in RECOVERY.</summary>
    public readonly float RecoveryDifficultyCut;

    /// <summary>Hard cap on consecutive PRESSURE beats before RECOVERY is forced.</summary>
    public readonly int MaxConsecutivePressureBeats;

    /// <summary>Number of beats RECOVERY lasts once entered.</summary>
    public readonly int RecoveryBeats;

    /// <summary>Minimum NORMAL beats required after RECOVERY before another PRESSURE window can start.</summary>
    public readonly int PressureCooldownBeats;

    public ChallengeConfig(
        float minDifficultyForPressure,
        float pressureChance,
        float pressureDifficultyBoost,
        float recoveryDifficultyCut,
        int maxConsecutivePressureBeats,
        int recoveryBeats,
        int pressureCooldownBeats)
    {
        MinDifficultyForPressure = Clamp01(minDifficultyForPressure);
        PressureChance = Clamp01(pressureChance);
        PressureDifficultyBoost = ClampRange(pressureDifficultyBoost, 0f, 0.5f);
        RecoveryDifficultyCut = ClampRange(recoveryDifficultyCut, 0f, 0.5f);
        MaxConsecutivePressureBeats = maxConsecutivePressureBeats < 1 ? 1 : maxConsecutivePressureBeats;
        RecoveryBeats = recoveryBeats < 1 ? 1 : recoveryBeats;
        PressureCooldownBeats = pressureCooldownBeats < 0 ? 0 : pressureCooldownBeats;
    }

    /// <summary>Balanced defaults, used when no <c>GameBalanceConfig</c> asset is available.</summary>
    public static ChallengeConfig Default => new ChallengeConfig(0.3f, 0.35f, 0.18f, 0.3f, 4, 2, 3);

    private static float Clamp01(float value)
    {
        return value < 0f ? 0f : (value > 1f ? 1f : value);
    }

    private static float ClampRange(float value, float min, float max)
    {
        return value < min ? min : (value > max ? max : value);
    }
}
