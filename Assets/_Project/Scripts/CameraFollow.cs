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
    [SerializeField, Range(0f, 15f)] private float maxSpeedFieldOfViewBoost = 5f;
    [Tooltip("Pace multiplier (GameController.GetPaceMultiplier) at which the FOV boost reaches its maximum.")]
    [SerializeField, Range(1.2f, 4f)] private float referencePaceForMaxBoost = 2f;
    [SerializeField] private float fieldOfViewLerpSpeed = 2f;

    private Vector3 feedbackOffset;
    private Vector3 appliedFeedbackOffset;
    private Camera trackedCamera;
    private float baseFieldOfView;

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

        Vector3 desired = target.position + offset;
        Vector3 basePosition = transform.position - appliedFeedbackOffset;
        transform.position = Vector3.Lerp(basePosition, desired, followSpeed * Time.deltaTime) + feedbackOffset;
        appliedFeedbackOffset = feedbackOffset;
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

    // Ambient speed-perception cue: widens FOV slightly as pace rises, eases back to baseline
    // outside active gameplay, and never bypasses the Camera Shake accessibility preference.
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
        trackedCamera.fieldOfView = Mathf.Lerp(trackedCamera.fieldOfView, targetFieldOfView, fieldOfViewLerpSpeed * Time.deltaTime);
    }
}
