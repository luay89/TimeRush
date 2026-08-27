using UnityEngine;

/// <summary>
/// Plays optional clips from feedback signals only; missing clips intentionally act as silent future hooks.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class FeedbackAudioPresenter : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    private AudioSource audioSource;
    private float nextMovementSoundTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (audioSource)
        {
            audioSource.Stop();
        }
    }

    private void Subscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.RunStarted += HandleRunStarted;
        events.PlayerLaneChanged += HandleLaneChanged;
        events.PlayerDepthChanged += HandleDepthChanged;
        events.NearMissTriggered += HandleNearMiss;
        events.ObstacleCollision += HandleCollision;
        events.GameOver += HandleGameOver;
        events.RunPaused += HandlePause;
        events.RunResumed += HandleResume;
        events.PaceMilestoneReached += HandlePaceMilestone;
    }

    private void Unsubscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.RunStarted -= HandleRunStarted;
        events.PlayerLaneChanged -= HandleLaneChanged;
        events.PlayerDepthChanged -= HandleDepthChanged;
        events.NearMissTriggered -= HandleNearMiss;
        events.ObstacleCollision -= HandleCollision;
        events.GameOver -= HandleGameOver;
        events.RunPaused -= HandlePause;
        events.RunResumed -= HandleResume;
        events.PaceMilestoneReached -= HandlePaceMilestone;
    }

    private void HandleRunStarted() => Play(feedbackConfig ? feedbackConfig.runStartClip : null, feedbackConfig ? feedbackConfig.runVolume : 0f);
    private void HandleLaneChanged(PlayerLaneChangedFeedback payload) => PlayMovement(feedbackConfig ? feedbackConfig.laneChangeClip : null, feedbackConfig ? feedbackConfig.laneVolume : 0f);
    private void HandleDepthChanged(PlayerDepthChangedFeedback payload) => PlayMovement(feedbackConfig ? feedbackConfig.depthMoveClip : null, feedbackConfig ? feedbackConfig.depthVolume : 0f);
    private void HandleNearMiss(NearMissFeedback payload) => Play(feedbackConfig ? feedbackConfig.nearMissClip : null, feedbackConfig ? feedbackConfig.nearMissVolume : 0f);
    private void HandleCollision(ObstacleCollisionFeedback payload) => Play(feedbackConfig ? feedbackConfig.collisionClip : null, feedbackConfig ? feedbackConfig.collisionVolume : 0f);
    private void HandleGameOver() => Play(feedbackConfig ? feedbackConfig.gameOverClip : null, feedbackConfig ? feedbackConfig.collisionVolume : 0f);
    private void HandlePause() => Play(feedbackConfig ? feedbackConfig.pauseClip : null, feedbackConfig ? feedbackConfig.pauseVolume : 0f);
    private void HandleResume() => Play(feedbackConfig ? feedbackConfig.resumeClip : null, feedbackConfig ? feedbackConfig.pauseVolume : 0f);
    private void HandlePaceMilestone(PaceMilestoneFeedback payload) => Play(feedbackConfig ? feedbackConfig.paceMilestoneClip : null, feedbackConfig ? feedbackConfig.runVolume : 0f);

    private void PlayMovement(AudioClip clip, float volume)
    {
        if (Time.unscaledTime < nextMovementSoundTime)
        {
            return;
        }

        nextMovementSoundTime = Time.unscaledTime + (feedbackConfig ? feedbackConfig.movementAudioCooldown : 0f);
        Play(clip, volume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (!audioSource || !clip || !FeedbackPreferences.IsAudioEnabled(feedbackConfig))
        {
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }
}
