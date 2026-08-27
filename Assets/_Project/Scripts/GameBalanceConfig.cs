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
    }
}
