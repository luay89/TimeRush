using UnityEngine;

/// <summary>
/// Pure, deterministic scaling helper: turns the player's current Flow (near-miss combo)
/// multiplier into a bounded multiplicative feedback-intensity boost, so escalating Flow reads
/// as visibly stronger near-miss feedback (shake/flash/VFX) without touching gameplay or
/// fairness. Always returns 1 (no change) when flowMultiplier is at its baseline of 1.
/// </summary>
public static class FlowFeedbackScaling
{
    public static float ComputeBoost(int flowMultiplier, int referenceMultiplier, float maxBoost)
    {
        float denominator = Mathf.Max(1, referenceMultiplier - 1);
        float t = Mathf.Clamp01((flowMultiplier - 1) / denominator);
        return Mathf.Lerp(1f, Mathf.Max(1f, maxBoost), t);
    }
}
