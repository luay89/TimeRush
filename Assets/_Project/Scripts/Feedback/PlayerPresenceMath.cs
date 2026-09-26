using UnityEngine;

/// <summary>
/// Pure, deterministic math for the player's ambient presence cues (emission brightness /
/// subtle pulse). Never reads or writes gameplay state itself -- PlayerPresencePresenter
/// supplies already-resolved inputs (effective pace, accessibility preferences).
/// </summary>
public static class PlayerPresenceMath
{
    /// <summary>
    /// Maps pace to an emission brightness multiplier: 1 at pace&lt;=1, ramping to maxBoost at
    /// referencePaceForMaxBoost, clamped beyond so the cue always stays bounded.
    /// </summary>
    public static float ComputeEmissionBrightness(float pace, float referencePaceForMaxBoost, float maxBoost)
    {
        float denominator = Mathf.Max(0.01f, referencePaceForMaxBoost - 1f);
        float t = Mathf.Clamp01((pace - 1f) / denominator);
        return Mathf.Lerp(1f, Mathf.Max(1f, maxBoost), t);
    }

    /// <summary>
    /// A tiny, bounded sine pulse multiplier around 1. Always returns exactly 1 (no oscillation
    /// at all) whenever Reduce Flashing is enabled, so this can never read as a strobe/flash.
    /// </summary>
    public static float ComputePulseMultiplier(float time, float pulseHz, float amplitude, bool reduceFlashingEnabled)
    {
        if (reduceFlashingEnabled || amplitude <= 0f)
        {
            return 1f;
        }

        return 1f + Mathf.Sin(time * pulseHz * Mathf.PI * 2f) * amplitude;
    }
}
