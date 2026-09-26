using UnityEngine;

/// <summary>
/// Deterministic helpers for ambient environment motion cues.
/// These functions are presentation-only and never alter gameplay timings.
/// </summary>
public static class RushTrackPerceptionMath
{
    /// <summary>
    /// Computes the travel speed for non-gameplay environment markers.
    /// Uses obstacle speed as the baseline cue and adds bounded pace amplification.
    /// </summary>
    public static float ResolveMarkerTravelSpeed(float obstacleSpeed, float rawPaceMultiplier, bool gameplayActive, bool cameraShakeEnabled)
    {
        if (obstacleSpeed <= 0f)
        {
            return 0f;
        }

        float effectivePace = SpeedPerceptionMath.ResolveEffectivePace(rawPaceMultiplier, gameplayActive, cameraShakeEnabled);
        float paceT = Mathf.Clamp01((effectivePace - 1f) / 1f);
        // Widened vs the original 1.2-1.65 range: the environment scroll now amplifies the felt
        // acceleration noticeably more strongly as pace rises, while still deriving entirely from
        // the real live obstacle speed/pace and never touching obstacle speed/reaction timing.
        float visualMultiplier = Mathf.Lerp(1.15f, 2.1f, paceT);
        return obstacleSpeed * visualMultiplier;
    }

    /// <summary>
    /// Maps effective pace to a brightness multiplier for the shared ambient glow materials
    /// (lane guidance / energy accents). Bounded to [1, maxBoost] -- a slow continuous
    /// escalation as the run intensifies, never an oscillation/flash.
    /// </summary>
    public static float ComputeAmbientGlowBrightness(float effectivePaceMultiplier, float maxBoost)
    {
        float paceT = Mathf.Clamp01((effectivePaceMultiplier - 1f) / 1f);
        return Mathf.Lerp(1f, Mathf.Max(1f, maxBoost), paceT);
    }

    /// <summary>
    /// Wraps a vertical position into an inclusive [minY, maxY] band.
    /// </summary>
    public static float WrapHeight(float y, float minY, float maxY)
    {
        if (maxY <= minY)
        {
            return minY;
        }

        float span = maxY - minY;
        while (y < minY)
        {
            y += span;
        }

        while (y > maxY)
        {
            y -= span;
        }

        return y;
    }

    /// <summary>
    /// Computes scale multiplier by height in band: larger near the player, smaller far ahead.
    /// </summary>
    public static float ComputeApproachScale(float normalizedHeight, float nearScale, float farScale)
    {
        float t = Mathf.Clamp01(normalizedHeight);
        return Mathf.Lerp(nearScale, farScale, t);
    }
}
