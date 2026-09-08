using UnityEngine;

/// <summary>
/// Owns tunable TimeRush values. Defaults mirror the proven pre-config values.
/// </summary>
[CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "TimeRush/Game Balance Config")]
public sealed class GameBalanceConfig : ScriptableObject
{
    [Header("Difficulty")]
    [Min(1f)] public float trainingDuration = 25f;
    [Min(2f)] public float maxDifficultySeconds = 120f;
    [Range(0.05f, 0.35f)] public float trainingMaxProgress = 0.18f;
    [Min(0.2f)] public float startSpawnInterval = 1.9f;
    [Min(0.2f)] public float minSpawnInterval = 0.85f;
    [Min(0.1f)] public float startFallSpeed = 4.1f;
    [Min(0.1f)] public float maxFallSpeed = 8.5f;
    [Range(0.1f, 1f)] public float startingDepthVariation = 0.65f;

    [Header("Spawner")]
    [Min(0.2f)] public float fallbackSpawnInterval = 1.5f;
    [Min(0.1f)] public float fallbackFallSpeed = 6f;
    [Min(1f)] public float spawnHeight = 13.5f;
    [Min(1f)] public float obstacleLifetime = 10f;

    [Header("Fairness")]
    [Min(2f)] public float minimumSameLaneGap = 7f;
    [Min(0.5f)] public float minimumDepthSeparation = 1.25f;
    [Min(0.2f)] public float lockWindowSeconds = 1.8f;
    [Min(0.2f)] public float minimumReactionSeconds = 1f;
    [Min(0.5f)] public float dangerRange = 4f;

    [Header("Input")]
    [Range(0.05f, 0.25f)] public float laneInputBufferSeconds = 0.12f;

    [Header("Challenge Sequencing")]
    [Tooltip("Raw difficulty01 (see GetDifficultyProgress) must be at or above this before a PRESSURE window may start.")]
    [Range(0f, 1f)] public float challengeMinDifficultyForPressure = 0.3f;
    [Tooltip("Per-beat probability of entering PRESSURE once eligible and off cooldown.")]
    [Range(0f, 1f)] public float challengePressureChance = 0.35f;
    [Tooltip("Added to the difficulty01 signal fed to pattern selection while in PRESSURE. Never affects spawn speed or interval.")]
    [Range(0f, 0.5f)] public float challengePressureDifficultyBoost = 0.18f;
    [Tooltip("Subtracted from the difficulty01 signal fed to pattern selection while in RECOVERY. Never affects spawn speed or interval.")]
    [Range(0f, 0.5f)] public float challengeRecoveryDifficultyCut = 0.3f;
    [Tooltip("Hard cap on consecutive PRESSURE beats before RECOVERY is forced.")]
    [Min(1)] public int challengeMaxConsecutivePressureBeats = 4;
    [Tooltip("Number of beats RECOVERY lasts once entered.")]
    [Min(1)] public int challengeRecoveryBeats = 2;
    [Tooltip("Minimum NORMAL beats required after RECOVERY before another PRESSURE window can start.")]
    [Min(0)] public int challengePressureCooldownBeats = 3;

    /// <summary>Builds the pure, Unity-independent tuning struct consumed by <see cref="ChallengeDirector"/>.</summary>
    public ChallengeConfig GetChallengeConfig()
    {
        return new ChallengeConfig(
            challengeMinDifficultyForPressure,
            challengePressureChance,
            challengePressureDifficultyBoost,
            challengeRecoveryDifficultyCut,
            challengeMaxConsecutivePressureBeats,
            challengeRecoveryBeats,
            challengePressureCooldownBeats);
    }

    public float GetDifficultyProgress(float effectiveAliveTime)
    {
        float cappedTime = Mathf.Min(Mathf.Max(0f, effectiveAliveTime), maxDifficultySeconds);

        if (cappedTime <= trainingDuration)
        {
            return Mathf.Lerp(0f, trainingMaxProgress, cappedTime / trainingDuration);
        }

        float arcadeProgress = Mathf.InverseLerp(trainingDuration, maxDifficultySeconds, cappedTime);
        return Mathf.Lerp(trainingMaxProgress, 1f, Mathf.SmoothStep(0f, 1f, arcadeProgress));
    }

    public float GetSpawnInterval(float effectiveAliveTime)
    {
        return Mathf.Lerp(startSpawnInterval, minSpawnInterval, GetDifficultyProgress(effectiveAliveTime));
    }

    public float GetFallSpeed(float effectiveAliveTime)
    {
        return Mathf.Lerp(startFallSpeed, maxFallSpeed, GetDifficultyProgress(effectiveAliveTime));
    }

    private void OnValidate()
    {
        trainingDuration = Mathf.Max(1f, trainingDuration);
        maxDifficultySeconds = Mathf.Max(trainingDuration + 1f, maxDifficultySeconds);
        minSpawnInterval = Mathf.Max(0.2f, minSpawnInterval);
        startSpawnInterval = Mathf.Max(minSpawnInterval, startSpawnInterval);
        startFallSpeed = Mathf.Max(0.1f, startFallSpeed);
        maxFallSpeed = Mathf.Max(startFallSpeed, maxFallSpeed);
        laneInputBufferSeconds = Mathf.Clamp(laneInputBufferSeconds, 0.05f, 0.25f);
        challengeMinDifficultyForPressure = Mathf.Clamp01(challengeMinDifficultyForPressure);
        challengePressureChance = Mathf.Clamp01(challengePressureChance);
        challengePressureDifficultyBoost = Mathf.Clamp(challengePressureDifficultyBoost, 0f, 0.5f);
        challengeRecoveryDifficultyCut = Mathf.Clamp(challengeRecoveryDifficultyCut, 0f, 0.5f);
        challengeMaxConsecutivePressureBeats = Mathf.Max(1, challengeMaxConsecutivePressureBeats);
        challengeRecoveryBeats = Mathf.Max(1, challengeRecoveryBeats);
        challengePressureCooldownBeats = Mathf.Max(0, challengePressureCooldownBeats);
    }
}
