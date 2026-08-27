using System;
using NUnit.Framework;
using UnityEngine;

public sealed class FeedbackEventHubTests
{
    [Test]
    public void TypedGameplayEvents_DeliverEachRequiredPayload()
    {
        var hub = new FeedbackEventHub();
        int lane = -1;
        float depth = 0f;
        int award = 0;
        int multiplier = 0;
        Vector3 collisionPosition = Vector3.zero;
        float pace = 0f;
        int simpleEvents = 0;

        hub.PlayerLaneChanged += payload => lane = payload.LaneIndex;
        hub.PlayerDepthChanged += payload => depth = payload.TargetDepth;
        hub.NearMissTriggered += payload => { award = payload.Award; multiplier = payload.FlowMultiplier; };
        hub.ObstacleCollision += payload => collisionPosition = payload.Position;
        hub.PaceMilestoneReached += payload => pace = payload.PaceMultiplier;
        hub.RunStarted += () => simpleEvents++;
        hub.RunPaused += () => simpleEvents++;
        hub.RunResumed += () => simpleEvents++;
        hub.GameOver += () => simpleEvents++;

        hub.RaisePlayerLaneChanged(new PlayerLaneChangedFeedback(Vector3.one, 2));
        hub.RaisePlayerDepthChanged(new PlayerDepthChangedFeedback(Vector3.zero, -1.5f));
        hub.RaiseNearMiss(new NearMissFeedback(Vector3.up, 15, 2));
        hub.RaiseObstacleCollision(new ObstacleCollisionFeedback(Vector3.forward));
        hub.RaisePaceMilestone(new PaceMilestoneFeedback(1.25f));
        hub.RaiseRunStarted();
        hub.RaiseRunPaused();
        hub.RaiseRunResumed();
        hub.RaiseGameOver();

        Assert.AreEqual(2, lane);
        Assert.AreEqual(-1.5f, depth);
        Assert.AreEqual(15, award);
        Assert.AreEqual(2, multiplier);
        Assert.AreEqual(Vector3.forward, collisionPosition);
        Assert.AreEqual(1.25f, pace);
        Assert.AreEqual(4, simpleEvents);
    }

    [Test]
    public void UnsubscribedListener_IsNotCalledAgain()
    {
        var hub = new FeedbackEventHub();
        int calls = 0;
        Action<NearMissFeedback> listener = payload => calls++;

        hub.NearMissTriggered += listener;
        hub.RaiseNearMiss(new NearMissFeedback(Vector3.zero, 5, 1));
        hub.NearMissTriggered -= listener;
        hub.RaiseNearMiss(new NearMissFeedback(Vector3.zero, 5, 1));

        Assert.AreEqual(1, calls);
    }
}
