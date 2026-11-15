using UnityEngine;

/// <summary>
/// Stores optional feedback accessibility choices without coupling UI controls to feedback consumers.
/// </summary>
public static class FeedbackPreferences
{
    private const string CameraShakeKey = "TIMERUSH_CAMERA_SHAKE";
    private const string ReduceFlashingKey = "TIMERUSH_REDUCE_FLASHING";
    private const string AudioEnabledKey = "TIMERUSH_AUDIO_ENABLED";
    private const string MusicVolumeKey = "TIMERUSH_MUSIC_VOLUME";
    private const string SfxVolumeKey = "TIMERUSH_SFX_VOLUME";
    private const string VibrationKey = "TIMERUSH_VIBRATION";
    private const string TutorialSeenKey = "TIMERUSH_TUTORIAL_SEEN";

    public const float DefaultMusicVolume = 0.8f;
    public const float DefaultSfxVolume = 1f;

    // Fallbacks used when a component has no FeedbackConfig assigned (e.g. added via AddComponent).
    // Keep in sync with the field initializers in FeedbackConfig.
    private const bool DefaultCameraShakeEnabled = true;
    private const bool DefaultReduceFlashing = false;
    private const bool DefaultAudioEnabled = true;

    public static bool IsCameraShakeEnabled(FeedbackConfig config)
    {
        return PlayerPrefs.GetInt(CameraShakeKey, (config != null ? config.cameraShakeEnabledByDefault : DefaultCameraShakeEnabled) ? 1 : 0) == 1;
    }

    public static bool IsReduceFlashingEnabled(FeedbackConfig config)
    {
        return PlayerPrefs.GetInt(ReduceFlashingKey, (config != null ? config.reduceFlashingByDefault : DefaultReduceFlashing) ? 1 : 0) == 1;
    }

    public static bool IsAudioEnabled(FeedbackConfig config)
    {
        return PlayerPrefs.GetInt(AudioEnabledKey, (config != null ? config.audioEnabledByDefault : DefaultAudioEnabled) ? 1 : 0) == 1;
    }

    /// <summary>Music loudness, 0..1 (scaled on top of the soundtrack's own mix level).</summary>
    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        set => SetFloat(MusicVolumeKey, value);
    }

    /// <summary>Sound-effect loudness, 0..1, applied to every one-shot via GameAudio.</summary>
    public static float SfxVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        set => SetFloat(SfxVolumeKey, value);
    }

    /// <summary>Short haptic pulse on impacts (phones only).</summary>
    public static bool IsVibrationEnabled => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
    public static void SetVibrationEnabled(bool enabled) => Set(VibrationKey, enabled);

    /// <summary>Whether the first-run "how to play" card has been dismissed.</summary>
    public static bool HasSeenTutorial => PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
    public static void SetTutorialSeen(bool seen) => Set(TutorialSeenKey, seen);

    public static void SetCameraShakeEnabled(bool enabled) => Set(CameraShakeKey, enabled);
    public static void SetReduceFlashingEnabled(bool enabled) => Set(ReduceFlashingKey, enabled);
    public static void SetAudioEnabled(bool enabled) => Set(AudioEnabledKey, enabled);

    private static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    private static void Set(string key, bool enabled)
    {
        PlayerPrefs.SetInt(key, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
