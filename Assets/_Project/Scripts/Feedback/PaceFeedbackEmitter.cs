using UnityEngine;

/// <summary>
/// Emits a compact pace milestone while a run is active, leaving difficulty calculation untouched.
/// </summary>
public sealed class PaceFeedbackEmitter : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    private float nextMilestone;

    private void Awake()
    {
        ResetNextMilestone();
    }

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

    private void Update()
    {
        if (!GameStateMachine.IsGameplayInputAllowed || !GameController.Instance || !feedbackConfig)
        {
            return;
        }

        float pace = GameController.Instance.GetPaceMultiplier();

        if (pace < nextMilestone)
        {
            return;
        }

        GameFeedbackSignals.RaisePaceMilestone(new PaceMilestoneFeedback(pace));
        nextMilestone += feedbackConfig.paceMilestoneStep;
    }

    private void HandleStateChanged(GameStateKind previous, GameStateKind current)
    {
        if (current == GameStateKind.Playing && previous == GameStateKind.Loading)
        {
            ResetNextMilestone();
        }
    }

    private void ResetNextMilestone()
    {
        float step = feedbackConfig ? feedbackConfig.paceMilestoneStep : 0.25f;
        nextMilestone = 1f + step;
    }
}
