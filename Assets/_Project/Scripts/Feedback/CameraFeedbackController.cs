using UnityEngine;

/// <summary>
/// Applies a short additive shake after CameraFollow has resolved its gameplay framing.
/// </summary>
[RequireComponent(typeof(CameraFollow))]
public sealed class CameraFeedbackController : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

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
    }

    private void HandleNearMiss(NearMissFeedback payload)
    {
        BeginShake(feedbackConfig ? feedbackConfig.nearMissShakeStrength : 0f, feedbackConfig ? feedbackConfig.nearMissShakeDuration : 0f);
    }

    private void HandleCollision(ObstacleCollisionFeedback payload)
    {
        BeginShake(feedbackConfig ? feedbackConfig.collisionShakeStrength : 0f, feedbackConfig ? feedbackConfig.collisionShakeDuration : 0f);
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
