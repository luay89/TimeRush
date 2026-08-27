using UnityEngine;

/// <summary>
/// Observes resolved player movement and emits presentation signals without reading device input or moving the player.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerMotionFeedbackEmitter : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    private PlayerController playerController;
    private int observedLane;
    private float observedTargetDepth;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        observedLane = playerController ? playerController.CurrentLane : 0;
        observedTargetDepth = playerController ? playerController.TargetTrackDepth : 0f;
    }

    private void Update()
    {
        if (!playerController || !GameStateMachine.IsGameplayInputAllowed)
        {
            return;
        }

        if (playerController.CurrentLane != observedLane)
        {
            observedLane = playerController.CurrentLane;
            GameFeedbackSignals.RaisePlayerLaneChanged(new PlayerLaneChangedFeedback(transform.position, observedLane));
        }

        float targetDepth = playerController.TargetTrackDepth;
        float threshold = feedbackConfig ? feedbackConfig.depthFeedbackThreshold : 0.2f;

        if (Mathf.Abs(targetDepth - observedTargetDepth) < threshold)
        {
            return;
        }

        observedTargetDepth = targetDepth;
        GameFeedbackSignals.RaisePlayerDepthChanged(new PlayerDepthChangedFeedback(transform.position, targetDepth));
    }
}
