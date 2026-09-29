using UnityEngine;

/// <summary>
/// Plays optional clips from feedback signals only; missing clips intentionally act as silent future hooks.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class FeedbackAudioPresenter : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    private AudioSource audioSource;
    // Dedicated source for the Flow-pitched near-miss cue. PlayOneShot voices keep following their
    // AudioSource's pitch while they play, so setting and restoring pitch on the shared source in
    // the same frame never reaches the audio thread; a separate source can keep its pitch.
    private AudioSource pitchedSource;
    private float nextMovementSoundTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        // These are UI/game-feel cues (lane change, near miss, game over, etc.), not sounds coming
        // from a place in the 3D world, so force fully 2D playback -- the scene's AudioSource was
        // left at its component default (3D spatial blend, doppler on), which applies distance
        // attenuation/stereo panning/pitch-shifting on every PlayOneShot and can make short cues
        // sound thin, panned, or muddy depending on camera motion. 2D + no doppler plays every
        // cue at a flat, consistent volume and pitch regardless of where this GameObject sits.
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;

        pitchedSource = gameObject.AddComponent<AudioSource>();
        pitchedSource.playOnAwake = false;
        pitchedSource.spatialBlend = 0f;
        pitchedSource.dopplerLevel = 0f;
        pitchedSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        pitchedSource.volume = audioSource.volume;
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

        if (pitchedSource)
        {
            pitchedSource.Stop();
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
    // Signature audio touch: the near-miss cue itself rises in pitch as the Flow chain builds, so
    // the player *hears* mastery accumulating (and hears it fall back with DecayFlow's tier drop)
    // instead of only reading a number. Same clip, no new audio assets needed.
    private void HandleNearMiss(NearMissFeedback payload) => PlayPitched(feedbackConfig ? feedbackConfig.nearMissClip : null, feedbackConfig ? feedbackConfig.nearMissVolume : 0f, FlowPitch(payload.FlowMultiplier));
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

    /// <summary>
    /// Same as Play, but on the dedicated pitched source so the Flow pitch actually applies for
    /// the whole cue without affecting any other one-shot.
    /// </summary>
    private void PlayPitched(AudioClip clip, float volume, float pitch)
    {
        if (!pitchedSource || !clip || !FeedbackPreferences.IsAudioEnabled(feedbackConfig))
        {
            return;
        }

        pitchedSource.pitch = pitch;
        pitchedSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Maps Flow multiplier (1..maxFlowMultiplier, typically 1-4) to a gentle upward pitch curve.
    /// Multiplier 1 (no chain yet) plays at natural pitch; each Flow level adds a small step so a
    /// maxed-out chain sounds noticeably brighter/faster without becoming a cartoonish chipmunk cue.
    /// </summary>
    private static float FlowPitch(int flowMultiplier)
    {
        int level = Mathf.Max(0, flowMultiplier - 1);
        return Mathf.Clamp(1f + level * 0.08f, 1f, 1.35f);
    }
}
