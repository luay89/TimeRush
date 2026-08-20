using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MockRewardedAdService : MonoBehaviour, IRewardedAdService
{
    [Tooltip("If enabled, the mock ad will report a simulated failure when shown.")]
    [SerializeField] private bool simulateFail;
    [Tooltip("Delay (in seconds) before the mock ad reports completion.")]
    [SerializeField] private float simulatedDelay = 0.5f;

    private bool isShowing;

    public bool IsReady => !isShowing;

    public void Show(System.Action onReward, System.Action onClosed, System.Action<string> onError)
    {
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
    }

    private IEnumerator SimulateRoutine(System.Action onReward, System.Action onClosed, System.Action<string> onError)
    {
        isShowing = true;
        yield return new WaitForSeconds(simulatedDelay);

        if (simulateFail)
        {
            onError?.Invoke("Mock rewarded ad simulated a failure.");
        }
        else
        {
            onReward?.Invoke();
        }

        onClosed?.Invoke();
        isShowing = false;
    }
}
