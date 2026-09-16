using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Focused safety checks for rewarded-continue callback ordering and lifecycle invalidation.
/// These tests intentionally exercise private callback handlers via reflection because the
/// production API surface is button-driven.
/// </summary>
public class RewardedContinueSafetyTests
{
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject host;
    private ResultsController controller;

    [SetUp]
    public void SetUp()
    {
        ScoreSnapshot.Clear();
        host = new GameObject("ResultsControllerTestHost");
        controller = host.AddComponent<ResultsController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (host)
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        ScoreSnapshot.Clear();
    }

    [Test]
    public void OnDisable_InvalidatesPendingContinueAttempt()
    {
        SetField("continueRequestInProgress", true);
        SetField("continueAttemptId", 7);
        SetContinueAdState("WaitingForRewardedAd");

        Invoke("OnDisable");

        Assert.IsFalse((bool)GetField("continueRequestInProgress"));
        Assert.AreEqual(8, (int)GetField("continueAttemptId"));

        bool active = (bool)Invoke("IsActiveAttempt", 7);
        Assert.IsFalse(active, "Disabled ResultsController must not accept stale callbacks.");
    }

    [Test]
    public void ClosedWithoutRewardThenReward_DoesNotQueueContinue()
    {
        SetField("continueRequestInProgress", true);
        SetField("continueAttemptId", 3);
        SetContinueAdState("WaitingForRewardedAd");

        Invoke("HandleAdClosed", 3);
        Assert.IsFalse((bool)GetField("continueRequestInProgress"));
        Assert.IsFalse(ScoreSnapshot.ContinueRequested);

        Invoke("HandleRewardGranted", 3);
        Assert.IsFalse(ScoreSnapshot.ContinueRequested, "Late reward after close must be ignored.");
    }

    [Test]
    public void FailedThenReward_DoesNotQueueContinue()
    {
        SetField("continueRequestInProgress", true);
        SetField("continueAttemptId", 5);
        SetContinueAdState("WaitingForRewardedAd");

        Invoke("HandleAdError", 5, "simulated error");
        Assert.IsFalse((bool)GetField("continueRequestInProgress"));
        Assert.IsFalse(ScoreSnapshot.ContinueRequested);

        Invoke("HandleRewardGranted", 5);
        Assert.IsFalse(ScoreSnapshot.ContinueRequested, "Late reward after failure must be ignored.");
    }

    [Test]
    public void RewardGrantedTwice_SecondCallbackIsIgnored()
    {
        SetField("continueRequestInProgress", true);
        SetField("continueAttemptId", 9);
        SetContinueAdState("RewardGranted");

        Invoke("HandleRewardGranted", 9);

        Assert.IsFalse(ScoreSnapshot.ContinueRequested, "Duplicate reward must not queue a second continue.");
        Assert.IsTrue((bool)GetField("continueRequestInProgress"), "Second reward callback should be a no-op.");
    }

    private void SetContinueAdState(string enumName)
    {
        FieldInfo field = typeof(ResultsController).GetField("continueAdState", InstanceFlags);
        Assert.NotNull(field);
        object value = Enum.Parse(field.FieldType, enumName);
        field.SetValue(controller, value);
    }

    private object GetField(string fieldName)
    {
        FieldInfo field = typeof(ResultsController).GetField(fieldName, InstanceFlags);
        Assert.NotNull(field, "Missing field: " + fieldName);
        return field.GetValue(controller);
    }

    private void SetField(string fieldName, object value)
    {
        FieldInfo field = typeof(ResultsController).GetField(fieldName, InstanceFlags);
        Assert.NotNull(field, "Missing field: " + fieldName);
        field.SetValue(controller, value);
    }

    private object Invoke(string methodName, params object[] args)
    {
        MethodInfo method = typeof(ResultsController).GetMethod(methodName, InstanceFlags);
        Assert.NotNull(method, "Missing method: " + methodName);
        return method.Invoke(controller, args);
    }
}
