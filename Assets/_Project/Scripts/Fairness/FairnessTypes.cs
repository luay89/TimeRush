using System.Collections.Generic;

public enum FairnessRejectionReason
{
    None,
    InvalidConfiguration,
    LateReaction,
    NoReachableAction
}

public readonly struct FairnessObstacleState
{
    public readonly int LaneIndex;
    public readonly float Height;
    public readonly float Depth;
    public readonly float Speed;

    public FairnessObstacleState(int laneIndex, float height, float depth, float speed)
    {
        LaneIndex = laneIndex;
        Height = height;
        Depth = depth;
        Speed = speed;
    }
}

public readonly struct FairnessPlayerState
{
    public readonly float LaneX;
    public readonly float Depth;
    public readonly float Height;
    public readonly float MinimumDepth;
    public readonly float MaximumDepth;
    public readonly float LaneMoveSpeed;
    public readonly float LaneSettleSeconds;
    public readonly float DepthMoveSpeed;
    public readonly float DepthSettleSeconds;

    public FairnessPlayerState(float laneX, float depth, float height, float minimumDepth, float maximumDepth, float laneMoveSpeed, float laneSettleSeconds, float depthMoveSpeed, float depthSettleSeconds)
    {
        LaneX = laneX;
        Depth = depth;
        Height = height;
        MinimumDepth = minimumDepth;
        MaximumDepth = maximumDepth;
        LaneMoveSpeed = laneMoveSpeed;
        LaneSettleSeconds = laneSettleSeconds;
        DepthMoveSpeed = depthMoveSpeed;
        DepthSettleSeconds = depthSettleSeconds;
    }
}

public readonly struct FairnessValidationContext
{
    public readonly IReadOnlyList<float> LanePositions;
    public readonly IReadOnlyList<FairnessObstacleState> ActiveObstacles;
    public readonly FairnessPlayerState Player;
    public readonly FairnessObstacleState Candidate;
    public readonly float DangerRange;
    public readonly float MinimumReactionSeconds;
    public readonly float MinimumDepthSeparation;

    public FairnessValidationContext(IReadOnlyList<float> lanePositions, IReadOnlyList<FairnessObstacleState> activeObstacles, FairnessPlayerState player, FairnessObstacleState candidate, float dangerRange, float minimumReactionSeconds, float minimumDepthSeparation)
    {
        LanePositions = lanePositions;
        ActiveObstacles = activeObstacles;
        Player = player;
        Candidate = candidate;
        DangerRange = dangerRange;
        MinimumReactionSeconds = minimumReactionSeconds;
        MinimumDepthSeparation = minimumDepthSeparation;
    }
}

public readonly struct FairnessDecision
{
    public readonly bool IsAllowed;
    public readonly FairnessRejectionReason Reason;
    public readonly float TimeToDanger;

    public FairnessDecision(bool isAllowed, FairnessRejectionReason reason, float timeToDanger)
    {
        IsAllowed = isAllowed;
        Reason = reason;
        TimeToDanger = timeToDanger;
    }
}
