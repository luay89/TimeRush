using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "3, 2, 1, GO!" at the start of every run (fresh, restart or continue) so the player is ready
/// before anything moves. The world is held with Time.timeScale = 0 -- every gameplay system
/// already runs on scaled delta time, so spawning, falling, score and difficulty all simply wait.
/// The countdown itself runs on unscaled time and only advances while the game is in the
/// Playing state, so opening the pause menu mid-count pauses the count too.
/// </summary>
public sealed class RunCountdown : MonoBehaviour
{
    private const float StepSeconds = 0.75f;
    private const float GoSeconds = 0.55f;
    private static readonly string[] Steps = { "3", "2", "1" };

    public static bool IsCounting { get; private set; }

    /// <summary>Forces the first-run tutorial card on (true) or off (false); null follows the saved flag.</summary>
    public static bool? TutorialOverride;

    private const float TutorialMinSeconds = 0.5f;

    private TextMeshProUGUI label;
    private Canvas canvas;
    private float elapsed;
    private bool finished;
    private AudioSource audioSource;
    private AudioClip tickClip;
    private AudioClip goClip;
    private int lastStep = -1;
    private GameObject tutorialCard;
    private TextMeshProUGUI tutorialPrompt;
    private float tutorialShownFor;

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
        IsCounting = false;
        if (scene.name != SceneNames.Game || FindObjectOfType<RunCountdown>())
        {
            return;
        }

        var go = new GameObject("RunCountdown");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<RunCountdown>();
    }

    private void Awake()
    {
        BuildUi();
        if (TutorialOverride ?? !FeedbackPreferences.HasSeenTutorial)
        {
            BuildTutorialCard();
        }
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        tickClip = CreateBeep("CountdownTick", 660f, 0.12f);
        goClip = CreateBeep("CountdownGo", 990f, 0.3f);
        IsCounting = true;
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (IsCounting)
        {
            IsCounting = false;
            if (IsPlayingState())
            {
                Time.timeScale = 1f;
            }
        }

        if (tickClip)
        {
            Destroy(tickClip);
        }

        if (goClip)
        {
            Destroy(goClip);
        }
    }

    private void Update()
    {
        if (finished)
        {
            return;
        }

        if (!IsPlayingState())
        {
            // Paused (or anything else): hold the count where it is.
            return;
        }

        if (IsCounting)
        {
            // Resuming from pause sets timeScale back to 1; keep the world held until GO.
            Time.timeScale = 0f;
        }

        if (tutorialCard)
        {
            TickTutorial();
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        int step = Mathf.FloorToInt(elapsed / StepSeconds);

        if (step < Steps.Length)
        {
            float local = (elapsed - step * StepSeconds) / StepSeconds;
            ShowStep(Steps[step], local, new Color(0.45f, 0.95f, 1f, 1f));
            if (step != lastStep)
            {
                lastStep = step;
                PlayClip(tickClip);
            }

            return;
        }

        if (IsCounting)
        {
            IsCounting = false;
            Time.timeScale = 1f;
            PlayClip(goClip);
        }

        float goLocal = (elapsed - Steps.Length * StepSeconds) / GoSeconds;
        if (goLocal >= 1f)
        {
            finished = true;
            canvas.gameObject.SetActive(false);
            return;
        }

        ShowStep("GO!", goLocal, new Color(1f, 0.6f, 0.2f, 1f));
    }

    // The world stays frozen behind the card until the player taps (or presses a key); only then
    // does "3, 2, 1" start, so the dismissing tap can never fire or steer the ship.
    private void TickTutorial()
    {
        tutorialShownFor += Time.unscaledDeltaTime;
        if (tutorialPrompt)
        {
            float blink = 0.55f + 0.45f * Mathf.Sin(tutorialShownFor * 4f);
            tutorialPrompt.alpha = blink;
        }

        bool tapped = Input.anyKeyDown || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (!tapped || tutorialShownFor < TutorialMinSeconds)
        {
            return;
        }

        if (TutorialOverride == null)
        {
            FeedbackPreferences.SetTutorialSeen(true);
        }

        GameAudio.Click();
        Destroy(tutorialCard);
        tutorialCard = null;
    }

    private void BuildTutorialCard()
    {
        tutorialCard = new GameObject("TutorialCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tutorialCard.transform.SetParent(canvas.transform, false);
        var dim = tutorialCard.GetComponent<Image>();
        dim.color = new Color(0.005f, 0.01f, 0.045f, 0.8f);
        var dimRect = (RectTransform)tutorialCard.transform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(tutorialCard.transform, false);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.13f, 0.96f);
        var panelRect = (RectTransform)panel.transform;
        // Stretches with the screen width so it fits portrait phones as well as landscape.
        panelRect.anchorMin = new Vector2(0.04f, 0.5f);
        panelRect.anchorMax = new Vector2(0.96f, 0.5f);
        panelRect.sizeDelta = new Vector2(0f, 640f);

        AddText(panel.transform, "HOW TO FLY", 72f, new Color(0.12f, 0.95f, 1f, 1f), 230f, 100f);
        AddText(panel.transform, "<color=#FFFFFF>SWIPE LEFT / RIGHT</color>   change lane   (A / D)", 34f, Muted, 120f, 56f);
        AddText(panel.transform, "<color=#FFFFFF>SWIPE UP / DOWN</color>   fly deeper or closer   (W / S)", 34f, Muted, 50f, 56f);
        AddText(panel.transform, "<color=#FFFFFF>TAP</color>   fire your laser   (Space)", 34f, Muted, -20f, 56f);
        AddText(panel.transform, "Red ring under a warship = it will hit you.   Blue orb = shield.", 28f, new Color(1f, 0.82f, 0.3f, 1f), -110f, 56f);
        tutorialPrompt = AddText(panel.transform, "TAP ANYWHERE TO START", 40f, new Color(1f, 0.55f, 0.2f, 1f), -225f, 70f);
    }

    private static readonly Color Muted = new Color(0.62f, 0.7f, 0.86f, 1f);

    private static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, float y, float height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontWeight = FontWeight.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.4f;
        tmp.fontSizeMax = size;
        var rect = tmp.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-60f, height);
        return tmp;
    }

    private static bool IsPlayingState()
    {
        return !GameStateMachine.HasInstance || GameStateMachine.Instance.CurrentState == GameStateKind.Playing;
    }

    private void ShowStep(string text, float t, Color color)
    {
        label.text = text;
        // Punch in big, settle, then fade toward the end of the step.
        float scale = t < 0.2f ? Mathf.Lerp(1.8f, 1f, t / 0.2f) : 1f;
        float alpha = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
        label.rectTransform.localScale = Vector3.one * scale;
        label.color = new Color(color.r, color.g, color.b, alpha);
    }

    private void PlayClip(AudioClip clip)
    {
        GameAudio.PlaySfx(audioSource, clip, 0.5f);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("CountdownCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var labelGo = new GameObject("CountdownLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(canvasGo.transform, false);
        label = labelGo.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 220f;
        label.fontWeight = FontWeight.Bold;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color(0.01f, 0.02f, 0.07f, 0.9f);
        label.text = string.Empty;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.55f);
        rect.sizeDelta = new Vector2(900f, 400f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static AudioClip CreateBeep(string name, float frequency, float seconds)
    {
        const int rate = 44100;
        int length = (int)(rate * seconds);
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float envelope = Mathf.Min(1f, t * 40f) * Mathf.Exp(-t * 4f);
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / rate) * envelope * 0.5f;
        }

        var clip = AudioClip.Create(name, length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
