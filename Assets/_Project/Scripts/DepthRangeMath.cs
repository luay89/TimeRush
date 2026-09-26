using UnityEngine;

/// <summary>
/// Pure, deterministic math for scaling the player's reachable depth range in lockstep with
/// GameController.GetDepthVariation() -- the same curve ObstacleSpawner already uses to compress
/// its own depth-spawn offsets early in a run. Keeping both scaled by the identical signal keeps
/// a constant safety margin between how far the player can retreat and how far obstacles can
/// spawn throughout the whole run, instead of only at the late-game (variation==1) steady state.
/// </summary>
public static class DepthRangeMath
{
    public static float ComputeEffectiveSafeDepthRange(float baseSafeDepthRange, float depthVariation)
    {
        return Mathf.Max(0f, baseSafeDepthRange) * Mathf.Clamp01(depthVariation);
    }
}
