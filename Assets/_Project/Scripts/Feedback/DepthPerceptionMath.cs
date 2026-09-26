using UnityEngine;

/// <summary>
/// Pure, deterministic math for the depth-position camera readability cue. Never reads or
/// writes gameplay state itself -- CameraFollow supplies already-resolved inputs (the player's
/// current depth normalized against their own reachable range, accessibility gating).
/// </summary>
public static class DepthPerceptionMath
{
    /// <summary>
    /// Normalizes a depth position into [-1, 1] against the player's own (possibly variation-
    /// scaled) reachable range, where 0 is track center. Returns 0 if the range is degenerate.
    /// </summary>
    public static float ComputeNormalizedDepth(float currentDepth, float trackCenterZ, float minimumSafeDepth, float maximumSafeDepth)
    {
        float span = Mathf.Max(0.0001f, maximumSafeDepth - minimumSafeDepth);
        float t = Mathf.Clamp((currentDepth - trackCenterZ) / (span * 0.5f), -1f, 1f);
        return t;
    }

    /// <summary>
    /// A small, symmetric field-of-view widening the further the player sits from depth center
    /// in either direction -- a "pulled back/wider" read that never favors one direction over
    /// the other, bounded so it can never read as a flash/strobe.
    /// </summary>
    public static float ComputeDepthFieldOfViewBoost(float normalizedDepth, float maxBoost)
    {
        return Mathf.Abs(Mathf.Clamp(normalizedDepth, -1f, 1f)) * Mathf.Max(0f, maxBoost);
    }

    /// <summary>
    /// A small, signed camera Z lag: the camera undershoots the player's actual depth by a
    /// bounded fraction, so the player visibly shifts within the frame when they change depth
    /// instead of the camera perfectly re-centering every frame.
    /// </summary>
    public static float ComputeDepthLagOffset(float normalizedDepth, float maxOffset)
    {
        return Mathf.Clamp(normalizedDepth, -1f, 1f) * Mathf.Max(0f, maxOffset);
    }
}
