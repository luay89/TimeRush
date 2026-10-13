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

    private TextMeshProUGUI label;
    private Canvas canvas;
    private float elapsed;
    private bool finished;
    private AudioSource audioSource;
    private AudioClip tickClip;
    private AudioClip goClip;
    private int lastStep = -1;

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
        if (clip && FeedbackPreferences.IsAudioEnabled(null))
        {
            audioSource.PlayOneShot(clip, 0.5f);
        }
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
