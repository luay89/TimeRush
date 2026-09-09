using UnityEngine;

/// <summary>
/// Pure, deterministic math for the ambient speed-perception camera field-of-view cue.
/// Never reads or writes gameplay state itself -- CameraFollow supplies already-resolved
/// inputs (raw pace multiplier, gameplay-active flag, camera-shake accessibility preference).
/// </summary>
public static class SpeedPerceptionMath
{
    /// <summary>
    /// Resolves the pace value actually used for the FOV cue: forced back to the neutral
    /// baseline (1) whenever gameplay isn't active or the player has disabled Camera Shake,
    /// so the cue never lingers boosted during menus/results/pause and never bypasses
    /// accessibility preferences.
    /// </summary>
    public static float ResolveEffectivePace(float rawPaceMultiplier, bool gameplayActive, bool cameraShakeEnabled)
    {
        return (gameplayActive && cameraShakeEnabled) ? rawPaceMultiplier : 1f;
    }

    /// <summary>
    /// Maps an effective pace multiplier to a target field of view: baseFieldOfView at
    /// pace&lt;=1, ramping to baseFieldOfView+maxBoost once pace reaches
    /// referencePaceForMaxBoost, clamped beyond so the cue always stays bounded and readable.
    /// </summary>
    public static float ComputeTargetFieldOfView(float baseFieldOfView, float effectivePace, float referencePaceForMaxBoost, float maxBoost)
    {
        float denominator = Mathf.Max(0.01f, referencePaceForMaxBoost - 1f);
        float t = Mathf.Clamp01((effectivePace - 1f) / denominator);
        return baseFieldOfView + maxBoost * t;
    }
}
