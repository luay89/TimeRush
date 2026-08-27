using UnityEngine;

/// <summary>
/// Immutable payloads that let presentation react without owning gameplay decisions.
/// </summary>
public readonly struct PlayerLaneChangedFeedback
{
    public readonly Vector3 Position;
    public readonly int LaneIndex;

    public PlayerLaneChangedFeedback(Vector3 position, int laneIndex)
    {
        Position = position;
        LaneIndex = laneIndex;
    }
}

public readonly struct PlayerDepthChangedFeedback
{
    public readonly Vector3 Position;
    public readonly float TargetDepth;

    public PlayerDepthChangedFeedback(Vector3 position, float targetDepth)
    {
        Position = position;
        TargetDepth = targetDepth;
    }
}

public readonly struct NearMissFeedback
{
    public readonly Vector3 Position;
    public readonly int Award;
    public readonly int FlowMultiplier;

    public NearMissFeedback(Vector3 position, int award, int flowMultiplier)
    {
        Position = position;
        Award = award;
        FlowMultiplier = flowMultiplier;
    }
}

public readonly struct ObstacleCollisionFeedback
{
    public readonly Vector3 Position;

    public ObstacleCollisionFeedback(Vector3 position)
    {
        Position = position;
    }
}

public readonly struct PaceMilestoneFeedback
{
    public readonly float PaceMultiplier;

    public PaceMilestoneFeedback(float paceMultiplier)
    {
        PaceMultiplier = paceMultiplier;
    }
}
