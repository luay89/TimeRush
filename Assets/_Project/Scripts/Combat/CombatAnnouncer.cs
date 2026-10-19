using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Big arcade call-outs across the upper screen during a run ("DOUBLE KILL", "RAMPAGE" ...) with a
/// chime that climbs a major arpeggio as the streak grows. Visual/audio only.
/// </summary>
public sealed class CombatAnnouncer : MonoBehaviour
{
    private const float Lifetime = 1.15f;
    private static readonly float[] ChimeSemitones = { 0f, 4f, 7f, 12f, 16f, 19f, 24f };

    private static CombatAnnouncer instance;

    private TextMeshProUGUI headline;
    private TextMeshProUGUI subline;
    private Color headlineColor;
    private float age = Lifetime;
    private AudioSource audioSource;
    private AudioClip chimeClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (scene.name != SceneNames.Game || FindObjectOfType<CombatAnnouncer>())
        {
            return;
        }

        var go = new GameObject("CombatAnnouncer");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<CombatAnnouncer>();
    }

    /// <summary>Shows a call-out. <paramref name="chimeStep"/> picks the note (0 = lowest), -1 for silence.</summary>
    public static void Announce(string text, string detail, Color color, int chimeStep)
    {
        if (!instance)
        {
            return;
        }

        instance.Show(text, detail, color, chimeStep);
    }

    private void Awake()
    {
        instance = this;
        BuildUi();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        chimeClip = CreateChime();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (chimeClip)
        {
            Destroy(chimeClip);
        }
    }

    private void Show(string text, string detail, Color color, int chimeStep)
    {
        headline.text = text;
        subline.text = detail ?? string.Empty;
        headlineColor = color;
        subline.color = new Color(1f, 1f, 1f, 0.9f);
        age = 0f;
        headline.gameObject.SetActive(true);
        subline.gameObject.SetActive(true);

        if (chimeStep >= 0 && chimeClip && FeedbackPreferences.IsAudioEnabled(null))
        {
            float semitones = ChimeSemitones[Mathf.Min(chimeStep, ChimeSemitones.Length - 1)];
            audioSource.pitch = Mathf.Pow(2f, semitones / 12f);
            audioSource.PlayOneShot(chimeClip, 0.5f);
        }
    }

    private void Update()
    {
        if (age >= Lifetime)
        {
            return;
        }

        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / Lifetime);
        if (t >= 1f)
        {
            headline.gameObject.SetActive(false);
            subline.gameObject.SetActive(false);
            return;
        }

        // Slam in oversized, settle, hold, then fade upward.
        float scale = t < 0.12f ? Mathf.Lerp(2.2f, 0.95f, t / 0.12f) : t < 0.2f ? Mathf.Lerp(0.95f, 1f, (t - 0.12f) / 0.08f) : 1f;
        float alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
        float rise = t < 0.75f ? 0f : (t - 0.75f) * 120f;
        headline.rectTransform.localScale = Vector3.one * scale;
        headline.rectTransform.anchoredPosition = new Vector2(0f, rise);
        headline.color = new Color(headlineColor.r, headlineColor.g, headlineColor.b, alpha);
        subline.rectTransform.anchoredPosition = new Vector2(0f, -78f + rise);
        subline.alpha = alpha;
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("AnnouncerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        headline = CreateLabel(canvasGo.transform, "Headline", 112f);
        subline = CreateLabel(canvasGo.transform, "Subline", 44f);
        headline.gameObject.SetActive(false);
        subline.gameObject.SetActive(false);
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold | FontStyles.Italic;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(10, 6, 30, 230);
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.72f);
        rect.sizeDelta = new Vector2(1600f, size * 1.5f);
        rect.anchoredPosition = Vector2.zero;
        return label;
    }

    // Bright two-partial chime (fundamental + fifth above the octave) with a fast decay.
    private static AudioClip CreateChime()
    {
        const int rate = 44100;
        const float seconds = 0.5f;
        const float frequency = 659.25f; // E5
        int length = (int)(rate * seconds);
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 7f);
            float sample = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * frequency * 3f * t) * Mathf.Exp(-t * 14f);
            data[i] = sample * envelope * 0.45f;
        }

        var clip = AudioClip.Create("StreakChime", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
