using UnityEngine;

/// <summary>
/// Device-independent command consumed by PlayerController; input sources never move the transform directly.
/// </summary>
public readonly struct PlayerIntent
{
    public readonly int LaneStep;
    public readonly float DepthAxis;
    public readonly int DepthStep;

    public bool HasLaneStep => LaneStep != 0;
    public bool HasDepthAxis => Mathf.Abs(DepthAxis) > 0.01f;
    public bool HasDepthStep => DepthStep != 0;
    public bool IsEmpty => !HasLaneStep && !HasDepthAxis && !HasDepthStep;

    public PlayerIntent(int laneStep, float depthAxis, int depthStep = 0)
    {
        LaneStep = Mathf.Clamp(laneStep, -1, 1);
        DepthAxis = Mathf.Clamp(depthAxis, -1f, 1f);
        DepthStep = Mathf.Clamp(depthStep, -1, 1);
    }

    public static PlayerIntent FromKeyboard(bool leftPressed, bool rightPressed, bool forwardHeld, bool backwardHeld)
    {
        int laneStep = leftPressed ? -1 : rightPressed ? 1 : 0;
        float depthAxis = (forwardHeld ? 1f : 0f) - (backwardHeld ? 1f : 0f);
        return new PlayerIntent(laneStep, depthAxis);
    }

    public static PlayerIntent FromSwipe(Vector2 delta, float threshold)
    {
        if (Mathf.Abs(delta.x) >= threshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return new PlayerIntent(delta.x > 0f ? 1 : -1, 0f);
        }

        return Mathf.Abs(delta.y) >= threshold
            ? new PlayerIntent(0, 0f, delta.y > 0f ? 1 : -1)
            : default;
    }

    public static int ClampLaneIndex(int currentLane, int laneStep, int laneCount)
    {
        return Mathf.Clamp(currentLane + Mathf.Clamp(laneStep, -1, 1), 0, Mathf.Max(0, laneCount - 1));
    }
}
