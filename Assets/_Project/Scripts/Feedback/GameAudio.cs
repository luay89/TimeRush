using UnityEngine;

/// <summary>
/// Single gate for sound effects: honours the Audio switch and the SFX volume slider, so every
/// one-shot in the game follows the Settings screen. Also owns the small synthesised interface
/// sounds (button click, confirm, pickup chime) and the impact vibration.
/// </summary>
public static class GameAudio
{
    private const int SampleRate = 32000;

    private static AudioSource uiSource;
    private static AudioClip clickClip;
    private static AudioClip confirmClip;
    private static AudioClip pickupClip;

    public static bool CanPlay(FeedbackConfig config = null)
    {
        return FeedbackPreferences.IsAudioEnabled(config) && FeedbackPreferences.SfxVolume > 0.001f;
    }

    /// <summary>PlayOneShot scaled by the SFX volume; does nothing while audio is off.</summary>
    public static void PlaySfx(AudioSource source, AudioClip clip, float volume, FeedbackConfig config = null)
    {
        if (!source || !clip || !CanPlay(config))
        {
            return;
        }

        source.PlayOneShot(clip, volume * FeedbackPreferences.SfxVolume);
    }

    public static void Click() => PlayUi(ref clickClip, () => Blip("UiClick", 1250f, 1650f, 0.045f), 0.35f);

    public static void Confirm() => PlayUi(ref confirmClip, () => Blip("UiConfirm", 700f, 1400f, 0.16f), 0.45f);

    public static void Pickup() => PlayUi(ref pickupClip, CreatePickupChime, 0.55f);

    /// <summary>A short buzz on phones, when the player has vibration enabled.</summary>
    public static void Vibrate()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (FeedbackPreferences.IsVibrationEnabled)
        {
            Handheld.Vibrate();
        }
#endif
    }

    private static void PlayUi(ref AudioClip clip, System.Func<AudioClip> create, float volume)
    {
        if (!CanPlay())
        {
            return;
        }

        if (!uiSource)
        {
            var go = new GameObject("GameAudioUi");
            Object.DontDestroyOnLoad(go);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            uiSource.ignoreListenerPause = true;
        }

        if (!clip)
        {
            clip = create();
        }

        uiSource.PlayOneShot(clip, volume * FeedbackPreferences.SfxVolume);
    }

    // A soft sine chirp sliding from one pitch to another with a quick fade -- reads as a "tick".
    private static AudioClip Blip(string name, float fromHz, float toHz, float seconds)
    {
        int count = Mathf.CeilToInt(SampleRate * seconds);
        var data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float hz = Mathf.Lerp(fromHz, toHz, t);
            phase += 2f * Mathf.PI * hz / SampleRate;
            float envelope = Mathf.Clamp01(i / (SampleRate * 0.003f)) * (1f - t) * (1f - t);
            data[i] = Mathf.Sin(phase) * envelope * 0.6f;
        }

        return MakeClip(name, data);
    }

    // Three rising bell notes (a major arpeggio) for grabbing a shield.
    private static AudioClip CreatePickupChime()
    {
        float[] notes = { 880f, 1108.73f, 1318.51f };
        const float noteGap = 0.06f;
        const float ring = 0.32f;
        int count = Mathf.CeilToInt(SampleRate * (noteGap * notes.Length + ring));
        var data = new float[count];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = Mathf.RoundToInt(SampleRate * noteGap * n);
            for (int i = start; i < count; i++)
            {
                float t = (i - start) / (float)SampleRate;
                float envelope = Mathf.Exp(-t * 9f) * Mathf.Clamp01(t / 0.004f);
                float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[n] * t);
                data[i] += tone * envelope * 0.28f;
            }
        }

        return MakeClip("UiPickup", data);
    }

    private static AudioClip MakeClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
