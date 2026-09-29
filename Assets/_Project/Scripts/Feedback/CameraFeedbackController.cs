using UnityEngine;

/// <summary>
/// Applies a short additive shake after CameraFollow has resolved its gameplay framing.
/// </summary>
[RequireComponent(typeof(CameraFollow))]
public sealed class CameraFeedbackController : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    [Header("Ambient Asteroid Flyby")]
    [Tooltip("Max shake strength for the ambient 'something big just swept past' cue -- purely cosmetic flavor, never derived from real collision or near-miss geometry.")]
    [SerializeField, Range(0f, 0.3f)] private float ambientJoltMaxStrength = 0.12f;
    [SerializeField, Range(0.1f, 0.6f)] private float ambientJoltDuration = 0.28f;

    private CameraFollow cameraFollow;
    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeStrength;

    private void Awake()
    {
        cameraFollow = GetComponent<CameraFollow>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearShake();
    }

    private void Update()
    {
        if (shakeTimeRemaining <= 0f || !cameraFollow)
        {
            return;
        }

        shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - Time.deltaTime);

        if (shakeTimeRemaining <= 0f)
        {
            ClearShake();
            return;
        }

        float fade = shakeDuration > 0f ? shakeTimeRemaining / shakeDuration : 0f;
        Vector2 sample = Random.insideUnitCircle * (shakeStrength * fade);
        cameraFollow.SetFeedbackOffset(new Vector3(sample.x, sample.y, 0f));
    }

    private void Subscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.NearMissTriggered += HandleNearMiss;
        events.ObstacleCollision += HandleCollision;
        events.RunPaused += ClearShake;
        events.AmbientCameraJolt += HandleAmbientJolt;
    }

    private void Unsubscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.NearMissTriggered -= HandleNearMiss;
        events.ObstacleCollision -= HandleCollision;
        events.RunPaused -= ClearShake;
        events.AmbientCameraJolt -= HandleAmbientJolt;
    }

    private void HandleNearMiss(NearMissFeedback payload)
    {
        float boost = feedbackConfig ? FlowFeedbackScaling.ComputeBoost(payload.FlowMultiplier, feedbackConfig.flowFeedbackReferenceMultiplier, feedbackConfig.flowFeedbackMaxBoost) : 1f;
        float strength = (feedbackConfig ? feedbackConfig.nearMissShakeStrength : 0f) * boost;
        BeginShake(strength, feedbackConfig ? feedbackConfig.nearMissShakeDuration : 0f);
    }

    private void HandleCollision(ObstacleCollisionFeedback payload)
    {
        BeginShake(feedbackConfig ? feedbackConfig.collisionShakeStrength : 0f, feedbackConfig ? feedbackConfig.collisionShakeDuration : 0f);
    }

    // Ambient flavor cue only -- deliberately does not go through FlowFeedbackScaling/near-miss
    // math, since it carries no gameplay meaning (score, precision, danger). Still fully gated by
    // the Camera Shake accessibility preference via BeginShake below.
    private void HandleAmbientJolt(float strength)
    {
        BeginShake(Mathf.Clamp01(strength) * ambientJoltMaxStrength, ambientJoltDuration);
    }

    private void BeginShake(float strength, float duration)
    {
        if (!FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig) || strength <= 0f || duration <= 0f)
        {
            return;
        }

        shakeStrength = strength;
        shakeDuration = duration;
        shakeTimeRemaining = duration;
    }

    private void ClearShake()
    {
        shakeTimeRemaining = 0f;
        shakeDuration = 0f;
        shakeStrength = 0f;

        if (cameraFollow)
        {
            cameraFollow.SetFeedbackOffset(Vector3.zero);
        }
    }
}
