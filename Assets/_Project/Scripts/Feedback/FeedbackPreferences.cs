using UnityEngine;

/// <summary>
/// Stores optional feedback accessibility choices without coupling UI controls to feedback consumers.
/// </summary>
public static class FeedbackPreferences
{
    private const string CameraShakeKey = "TIMERUSH_CAMERA_SHAKE";
    private const string ReduceFlashingKey = "TIMERUSH_REDUCE_FLASHING";
    private const string AudioEnabledKey = "TIMERUSH_AUDIO_ENABLED";

    public static bool IsCameraShakeEnabled(FeedbackConfig config)
    {
        return PlayerPrefs.GetInt(CameraShakeKey, config != null && config.cameraShakeEnabledByDefault ? 1 : 0) == 1;
    }

    public static bool IsReduceFlashingEnabled(FeedbackConfig config)
    {
        return PlayerPrefs.GetInt(ReduceFlashingKey, config != null && config.reduceFlashingByDefault ? 1 : 0) == 1;
    }

    public static bool IsAudioEnabled(FeedbackConfig config)
    {
        return PlayerPrefs.GetInt(AudioEnabledKey, config != null && config.audioEnabledByDefault ? 1 : 0) == 1;
    }

    public static void SetCameraShakeEnabled(bool enabled) => Set(CameraShakeKey, enabled);
    public static void SetReduceFlashingEnabled(bool enabled) => Set(ReduceFlashingKey, enabled);
    public static void SetAudioEnabled(bool enabled) => Set(AudioEnabledKey, enabled);

    private static void Set(string key, bool enabled)
    {
        PlayerPrefs.SetInt(key, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
