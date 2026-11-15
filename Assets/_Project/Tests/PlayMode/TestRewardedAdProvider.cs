using System;
using UnityEngine;

/// <summary>
/// Test double standing in for a real rewarded-ad SDK provider in EditMode tests only. It never
/// grants rewards; tests only check which provider ResultsController selects.
/// </summary>
public sealed class TestRewardedAdProvider : MonoBehaviour, IRewardedAdService
{
    public bool IsReady => false;

    public void Show(Action onReward, Action onClosed, Action<string> onError)
    {
        onError?.Invoke("Test provider never shows ads.");
        onClosed?.Invoke();
    }
}
