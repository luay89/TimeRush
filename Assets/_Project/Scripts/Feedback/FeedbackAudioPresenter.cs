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
        events.ChallengeStateChanged += HandleChallengeStateChanged;
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
        events.ChallengeStateChanged -= HandleChallengeStateChanged;
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

    // Subtle anticipation/relief cue only: fires once per NORMAL->PRESSURE or ->RECOVERY
    // transition (ObstacleSpawner only raises this on an actual state change, never per-beat).
    // A missing clip stays silent, matching every other optional-clip hook in this presenter.
    private void HandleChallengeStateChanged(ChallengeStateChangedFeedback payload)
    {
        AudioClip clip = null;

        if (payload.State == ChallengeState.Pressure)
        {
            clip = feedbackConfig ? feedbackConfig.pressureBeginClip : null;
        }
        else if (payload.State == ChallengeState.Recovery)
        {
            clip = feedbackConfig ? feedbackConfig.recoveryBeginClip : null;
        }

        Play(clip, feedbackConfig ? feedbackConfig.runVolume : 0f);
    }

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
