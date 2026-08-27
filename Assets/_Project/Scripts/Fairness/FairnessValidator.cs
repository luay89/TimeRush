using UnityEngine;

/// <summary>
/// Pure authority for temporal reachability. Presentation and spawning stay outside this class.
/// </summary>
public sealed class FairnessValidator
{
    public FairnessDecision Evaluate(in FairnessValidationContext context)
    {
        if (!HasValidConfiguration(context))
        {
            return new FairnessDecision(false, FairnessRejectionReason.InvalidConfiguration, 0f);
        }

        float candidateTime = TimeToDanger(context.Candidate, context.Player.Height, context.DangerRange);

        if (candidateTime < context.MinimumReactionSeconds)
        {
            return new FairnessDecision(false, FairnessRejectionReason.LateReaction, candidateTime);
        }

        return HasReachableAction(context, candidateTime)
            ? new FairnessDecision(true, FairnessRejectionReason.None, candidateTime)
            : new FairnessDecision(false, FairnessRejectionReason.NoReachableAction, candidateTime);
    }

    public bool IsImmediateLaneRepeat(int candidateLane, int previousLane, bool protectionEnabled)
    {
        return protectionEnabled && previousLane >= 0 && candidateLane == previousLane;
    }

    private static bool HasValidConfiguration(in FairnessValidationContext context)
    {
        return context.LanePositions != null && context.LanePositions.Count == 3 &&
               context.ActiveObstacles != null && context.Candidate.LaneIndex >= 0 &&
               context.Candidate.LaneIndex < context.LanePositions.Count &&
               context.Candidate.Speed > 0f && context.DangerRange > 0f &&
               context.MinimumReactionSeconds > 0f && context.MinimumDepthSeparation > 0f &&
               context.Player.LaneMoveSpeed > 0f && context.Player.DepthMoveSpeed > 0f;
    }

    private static bool HasReachableAction(in FairnessValidationContext context, float candidateTime)
    {
        for (int lane = 0; lane < context.LanePositions.Count; lane++)
        {
            float laneTime = context.Player.LaneSettleSeconds + Mathf.Abs(context.LanePositions[lane] - context.Player.LaneX) / context.Player.LaneMoveSpeed;

            if (IsDepthActionReachable(context, lane, context.Player.MinimumDepth, laneTime, candidateTime) ||
                IsDepthActionReachable(context, lane, context.Player.Depth, laneTime, candidateTime) ||
                IsDepthActionReachable(context, lane, context.Player.MaximumDepth, laneTime, candidateTime))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDepthActionReachable(in FairnessValidationContext context, int lane, float targetDepth, float laneTime, float candidateTime)
    {
        float depthTime = context.Player.DepthSettleSeconds + Mathf.Abs(targetDepth - context.Player.Depth) / context.Player.DepthMoveSpeed;
        float actionTime = Mathf.Max(laneTime, depthTime);
        return actionTime <= candidateTime && !IsThreatened(context, lane, targetDepth, candidateTime);
    }

    private static bool IsThreatened(in FairnessValidationContext context, int lane, float targetDepth, float horizon)
    {
        if (IsObstacleThreatening(context.Candidate, context, lane, targetDepth, horizon))
        {
            return true;
        }

        for (int i = 0; i < context.ActiveObstacles.Count; i++)
        {
            if (IsObstacleThreatening(context.ActiveObstacles[i], context, lane, targetDepth, horizon))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsObstacleThreatening(FairnessObstacleState obstacle, in FairnessValidationContext context, int lane, float targetDepth, float horizon)
    {
        if (obstacle.LaneIndex != lane || obstacle.Height < context.Player.Height - 0.5f || Mathf.Abs(obstacle.Depth - targetDepth) >= context.MinimumDepthSeparation)
        {
            return false;
        }

        float time = TimeToDanger(obstacle, context.Player.Height, context.DangerRange);
        return time >= 0f && time <= horizon;
    }

    private static float TimeToDanger(FairnessObstacleState obstacle, float playerHeight, float dangerRange)
    {
        float dangerTop = playerHeight + dangerRange;
        return obstacle.Height <= dangerTop ? 0f : (obstacle.Height - dangerTop) / obstacle.Speed;
    }
}
