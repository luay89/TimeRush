using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -10f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 2.5f, 3.5f);
    [SerializeField] private float followSpeed = 10f;

    [Header("Speed Perception")]
    [SerializeField] private FeedbackConfig feedbackConfig;
    [Tooltip("Extra field of view added at maximum pace -- a purely perceptual cue that never changes obstacle speed, spawn interval, reaction time, or difficulty.")]
    [SerializeField, Range(0f, 15f)] private float maxSpeedFieldOfViewBoost = 7f;
    [Tooltip("Pace multiplier (GameController.GetPaceMultiplier) at which the FOV boost reaches its maximum.")]
    [SerializeField, Range(1.2f, 4f)] private float referencePaceForMaxBoost = 1.8f;
    [SerializeField] private float fieldOfViewLerpSpeed = 2.8f;

    [Header("Depth Perception")]
    [Tooltip("Max FOV widening at the player's own depth extremes (symmetric, either direction) -- a purely perceptual cue so forward/back movement is visibly readable.")]
    [SerializeField, Range(0f, 5f)] private float maxDepthFieldOfViewBoost = 1.6f;
    [Tooltip("Max camera Z lag at the player's own depth extremes -- the player visibly shifts within the frame when they change depth instead of the camera perfectly re-centering every frame.")]
    [SerializeField, Range(0f, 1.5f)] private float maxDepthCameraLag = 0.6f;

    private Vector3 feedbackOffset;
    private Vector3 appliedFeedbackOffset;
    private Camera trackedCamera;
    private float baseFieldOfView;
    private PlayerController targetPlayerController;
    private float cachedNormalizedDepth;
    private float appliedDepthLagOffsetZ;

    private void Awake()
    {
        trackedCamera = GetComponent<Camera>();
        if (trackedCamera)
        {
            baseFieldOfView = trackedCamera.fieldOfView;
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        if (!targetPlayerController)
        {
            targetPlayerController = target.GetComponent<PlayerController>();
        }

        UpdateDepthPerceptionState();

        Vector3 desired = target.position + offset;
        Vector3 basePosition = transform.position - appliedFeedbackOffset - new Vector3(0f, 0f, appliedDepthLagOffsetZ);
        float depthLagOffsetZ = DepthPerceptionMath.ComputeDepthLagOffset(cachedNormalizedDepth, maxDepthCameraLag);
        transform.position = Vector3.Lerp(basePosition, desired, followSpeed * Time.deltaTime) + feedbackOffset + new Vector3(0f, 0f, depthLagOffsetZ);
        appliedFeedbackOffset = feedbackOffset;
        appliedDepthLagOffsetZ = depthLagOffsetZ;
        transform.LookAt(target.position + lookAtOffset);

        UpdateSpeedFieldOfView();
    }

    /// <summary>
    /// Lets the feedback layer add a short visual offset without altering the follow target or gameplay transforms.
    /// </summary>
    public void SetFeedbackOffset(Vector3 offset)
    {
        feedbackOffset = offset;
    }

    // Resolves how far off depth-center the player currently is (symmetric [-1,1], gated behind
    // the same Camera Shake preference / active-gameplay guard the speed-perception cue already
    // uses) once per frame, shared by both the FOV boost and the camera Z lag below.
    private void UpdateDepthPerceptionState()
    {
        bool gameplayActive = GameStateMachine.IsGameplayInputAllowed;
        bool shakeEnabled = FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig);

        if (!targetPlayerController || !gameplayActive || !shakeEnabled)
        {
            cachedNormalizedDepth = 0f;
            return;
        }

        float minDepth = targetPlayerController.MinimumSafeDepth;
        float maxDepth = targetPlayerController.MaximumSafeDepth;
        cachedNormalizedDepth = DepthPerceptionMath.ComputeNormalizedDepth(targetPlayerController.CurrentTrackDepth, (minDepth + maxDepth) * 0.5f, minDepth, maxDepth);
    }

    // Ambient speed-perception cue: widens FOV slightly as pace rises, eases back to baseline
    // outside active gameplay, and never bypasses the Camera Shake accessibility preference.
    // Also folds in the depth-position FOV term computed above.
    private void UpdateSpeedFieldOfView()
    {
        if (!trackedCamera)
        {
            return;
        }

        var gc = GameController.Instance;
        float rawPace = gc ? gc.GetPaceMultiplier() : 1f;
        bool gameplayActive = gc && GameStateMachine.IsGameplayInputAllowed;
        bool shakeEnabled = FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig);

        float effectivePace = SpeedPerceptionMath.ResolveEffectivePace(rawPace, gameplayActive, shakeEnabled);
        float targetFieldOfView = SpeedPerceptionMath.ComputeTargetFieldOfView(baseFieldOfView, effectivePace, referencePaceForMaxBoost, maxSpeedFieldOfViewBoost);
        targetFieldOfView += DepthPerceptionMath.ComputeDepthFieldOfViewBoost(cachedNormalizedDepth, maxDepthFieldOfViewBoost);
        trackedCamera.fieldOfView = Mathf.Lerp(trackedCamera.fieldOfView, targetFieldOfView, fieldOfViewLerpSpeed * Time.deltaTime);
    }
}
