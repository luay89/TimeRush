using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MockRewardedAdService : MonoBehaviour, IRewardedAdService
{
    private enum SimulatedOutcome
    {
        RewardGranted,
        ClosedWithoutReward,
        Failed
    }

    [Tooltip("Controls the mock ad result for editor/runtime testing.")]
    [SerializeField] private SimulatedOutcome simulatedOutcome = SimulatedOutcome.RewardGranted;
    [Tooltip("Delay (in seconds) before the mock ad reports completion.")]
    [SerializeField] private float simulatedDelay = 0.5f;

    private bool isShowing;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool IsReady => !isShowing;
#else
    // Release builds must never hand out a free continue: the mock reports "no ad available"
    // until a real rewarded-ad provider replaces it.
    public bool IsReady => false;
#endif

    // Dev/test safety net: this component normally lives only on the persistent Boot-scene
    // GameObject, so it's never instantiated at all when Play Mode (or a build) starts directly
    // from Game/Results and skips Boot. Without this, ResultsController's scene-graph lookup for
    // an IRewardedAdService finds nothing and reports "Rewarded ad unavailable" even though the
    // mock is meant to always simulate an available ad in development. Runs once at process start,
    // before the first scene's own Awake/Start, and only creates a fallback if no
    // IRewardedAdService already exists (e.g. Boot's own instance is left untouched/unduplicated).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureFallbackInstanceExists()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IRewardedAdService)
            {
                return;
            }
        }

        var fallback = new GameObject("MockRewardedAdService (Fallback)");
        fallback.AddComponent<MockRewardedAdService>();
        DontDestroyOnLoad(fallback);
    }
#endif

    public void Show(System.Action onReward, System.Action onClosed, System.Action<string> onError)
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        onError?.Invoke("No rewarded-ad provider is configured for release builds.");
        onClosed?.Invoke();
        return;
#else
        if (isShowing)
        {
            onError?.Invoke("MockRewardedAdService is already showing an ad.");
            return;
        }

        if (!isActiveAndEnabled)
        {
            onError?.Invoke("MockRewardedAdService is not active in the scene.");
            onClosed?.Invoke();
            return;
        }

        StartCoroutine(SimulateRoutine(onReward, onClosed, onError));
#endif
    }

    private IEnumerator SimulateRoutine(System.Action onReward, System.Action onClosed, System.Action<string> onError)
    {
        isShowing = true;
        yield return new WaitForSeconds(simulatedDelay);

        if (simulatedOutcome == SimulatedOutcome.Failed)
        {
            onError?.Invoke("Mock rewarded ad simulated a failure.");
        }
        else if (simulatedOutcome == SimulatedOutcome.RewardGranted)
        {
            onReward?.Invoke();
        }

        onClosed?.Invoke();
        isShowing = false;
    }
}
