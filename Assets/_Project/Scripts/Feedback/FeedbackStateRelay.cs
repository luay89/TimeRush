using UnityEngine;

/// <summary>
/// Converts only FSM lifecycle changes into feedback signals; it never changes FSM state itself.
/// </summary>
public sealed class FeedbackStateRelay : MonoBehaviour
{
    private void OnEnable()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.StateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.StateChanged -= HandleStateChanged;
        }
    }

    private static void HandleStateChanged(GameStateKind previous, GameStateKind current)
    {
        if (current == GameStateKind.Paused)
        {
            GameFeedbackSignals.RaiseRunPaused();
            return;
        }

        if (current != GameStateKind.Playing)
        {
            return;
        }

        if (previous == GameStateKind.Paused)
        {
            GameFeedbackSignals.RaiseRunResumed();
            return;
        }

        if (previous == GameStateKind.Loading)
        {
            GameFeedbackSignals.RaiseRunStarted();
        }
    }
}
