using System.Collections.Generic;

/// <summary>
/// Validates a group of simultaneous pattern placements against the single fairness
/// authority (<see cref="FairnessValidator"/>) without duplicating any fairness rule.
///
/// Each candidate is evaluated in turn against the real active obstacles plus every
/// earlier candidate already accepted in the group. Because the final candidate is
/// evaluated against all the others, a passing group guarantees that, once every
/// obstacle in it exists, at least one reachable survival action still avoids them all.
/// A group is placed only when this method returns true, so no impossible pattern can
/// ever enter active gameplay.
/// </summary>
public static class PatternFairnessProbe
{
    public static bool CanPlaceGroup(
        FairnessValidator validator,
        float[] lanePositions,
        IReadOnlyList<FairnessObstacleState> activeObstacles,
        in FairnessPlayerState player,
        IReadOnlyList<FairnessObstacleState> candidates,
        float dangerRange,
        float reactionSeconds,
        float depthSeparation,
        List<FairnessObstacleState> scratch = null)
    {
        if (validator == null || lanePositions == null || candidates == null || candidates.Count == 0)
        {
            return false;
        }

        List<FairnessObstacleState> working = scratch ?? new List<FairnessObstacleState>((activeObstacles?.Count ?? 0) + candidates.Count);
        working.Clear();

        if (activeObstacles != null)
        {
            for (int i = 0; i < activeObstacles.Count; i++)
            {
                working.Add(activeObstacles[i]);
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            FairnessObstacleState candidate = candidates[i];
            var context = new FairnessValidationContext(lanePositions, working, player, candidate, dangerRange, reactionSeconds, depthSeparation);

            if (!validator.Evaluate(context).IsAllowed)
            {
                return false;
            }

            working.Add(candidate);
        }

        return true;
    }
}
