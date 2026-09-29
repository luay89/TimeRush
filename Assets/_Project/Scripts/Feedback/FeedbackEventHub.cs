using System;

/// <summary>
/// Typed, instance-scoped event hub. Consumers subscribe and unsubscribe with their own scene lifetime.
/// </summary>
public sealed class FeedbackEventHub
{
    public event Action<PlayerLaneChangedFeedback> PlayerLaneChanged;
    public event Action<PlayerDepthChangedFeedback> PlayerDepthChanged;
    public event Action<NearMissFeedback> NearMissTriggered;
    public event Action<ObstacleCollisionFeedback> ObstacleCollision;
    public event Action GameOver;
    public event Action RunStarted;
    public event Action RunPaused;
    public event Action RunResumed;
    public event Action<PaceMilestoneFeedback> PaceMilestoneReached;
    public event Action<ChallengeStateChangedFeedback> ChallengeStateChanged;
    // Purely cosmetic "something big just swept past" cue, dispatched on a soft ambient timer by
    // RushTrackEnvironment -- unlike NearMissTriggered, it is never derived from real collision or
    // fairness geometry and never awards score, so it can't be confused with an actual near miss.
    public event Action<float> AmbientCameraJolt;

    public void RaisePlayerLaneChanged(PlayerLaneChangedFeedback payload) => PlayerLaneChanged?.Invoke(payload);
    public void RaisePlayerDepthChanged(PlayerDepthChangedFeedback payload) => PlayerDepthChanged?.Invoke(payload);
    public void RaiseNearMiss(NearMissFeedback payload) => NearMissTriggered?.Invoke(payload);
    public void RaiseObstacleCollision(ObstacleCollisionFeedback payload) => ObstacleCollision?.Invoke(payload);
    public void RaiseGameOver() => GameOver?.Invoke();
    public void RaiseRunStarted() => RunStarted?.Invoke();
    public void RaiseRunPaused() => RunPaused?.Invoke();
    public void RaiseRunResumed() => RunResumed?.Invoke();
    public void RaisePaceMilestone(PaceMilestoneFeedback payload) => PaceMilestoneReached?.Invoke(payload);
    public void RaiseChallengeStateChanged(ChallengeStateChangedFeedback payload) => ChallengeStateChanged?.Invoke(payload);
    public void RaiseAmbientCameraJolt(float strength) => AmbientCameraJolt?.Invoke(strength);
}
