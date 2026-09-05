using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ResultsController : MonoBehaviour
{
    private enum ContinueAdState
    {
        Idle,
        ContinueRequested,
        WaitingForRewardedAd,
        RewardGranted,
        AdUnavailable,
        AdFailed,
        AdClosedWithoutReward
    }

    private const string RestartButtonName = "RestartButton";
    private const string MenuButtonName = "MenuButton";
    private const string ContinueButtonName = "ContinueButton";
    private const string FinalScoreLabelName = "FinalScoreText";
    private const string BestScoreLabelName = "BestScoreText";
    private const string ResultStatusLabelName = "ResultStatusText";
    private const string GameOverTitleName = "GameOverTitle";
    private const string BestScoreKey = "BEST_SCORE";

    [Header("UI Auto-Build Settings")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("UI References (Optional)")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;
    [SerializeField] private TextMeshProUGUI resultStatusText;
    [SerializeField] private TextMeshProUGUI gameOverTitle;
    [SerializeField, Tooltip("Optional rewarded-ad provider. If omitted, ResultsController searches active/persistent objects for an IRewardedAdService implementation.")]
    private MonoBehaviour rewardedAdServiceSource;

    [Header("Testing (Optional)")]
    [SerializeField, Tooltip("Force show Continue button for UI testing only.")]
    private bool forceShowContinueForTesting = false;

    private bool uiInitialized;
    private bool buttonsBound;
    private bool continueButtonBound;

    private Canvas cachedCanvas;
    private bool continueRequestInProgress;
    private bool navigationRequestInProgress;
    private bool continueAvailable;
    private ContinueAdState continueAdState;
    private int continueAttemptId;
    private IRewardedAdService rewardedAdService;

    private bool missingFinalScoreLabelLogged;
    private bool missingBestScoreLabelLogged;

    private void Awake()
    {
        Time.timeScale = 1f;
        ResolveRewardedAdService();
        EnsureUserInterface();
        BindButtons();
    }

    private void OnEnable()
    {
        Time.timeScale = 1f;
        ResolveRewardedAdService();
        BindButtons();
        RefreshContinueState();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        UpdateScoreLabels();
        RefreshContinueState();
        LogUiDiagnostics();
    }

    public void RestartGame()
    {
        if (!TryBeginNavigationRequest())
        {
            return;
        }

        Time.timeScale = 1f;

        if (GameStateMachine.HasInstance)
        {
            if (!GameStateMachine.Instance.RestartFromResults())
            {
                navigationRequestInProgress = false;
            }
            return;
        }

        SceneManager.LoadScene(SceneNames.Game);
    }

    public void GoToMenu()
    {
        if (!TryBeginNavigationRequest())
        {
            return;
        }

        Time.timeScale = 1f;

        if (GameStateMachine.HasInstance)
        {
            if (!GameStateMachine.Instance.ReturnToMenu())
            {
                navigationRequestInProgress = false;
            }
            return;
        }

        SceneManager.LoadScene(SceneNames.MenuHub);
    }

    // =========================
    // Continue (Rewarded Ad Gate)
    // =========================
    private void OnContinuePressed()
    {
        Debug.Log("[CONTINUE] Clicked", this);

        if (continueRequestInProgress)
        {
            Debug.Log("[CONTINUE] Already processing", this);
            return;
        }

        continueRequestInProgress = true;
        continueAdState = ContinueAdState.ContinueRequested;
        ApplyButtonStates();

        if (!continueAvailable)
        {
            Debug.LogWarning("[CONTINUE] Not available", this);
            continueRequestInProgress = false;
            continueAdState = ContinueAdState.Idle;
            RefreshContinueState();
            return;
        }

        IRewardedAdService adService = ResolveRewardedAdService();
        if (adService == null || !adService.IsReady)
        {
            Debug.LogWarning("[CONTINUE] Rewarded ad unavailable", this);
            HandleAdFailure(ContinueAdState.AdUnavailable, "CONTINUE UNAVAILABLE");
            return;
        }

        continueAdState = ContinueAdState.WaitingForRewardedAd;
        int attemptId = ++continueAttemptId;
        ApplyButtonStates();
        SetStatusMessage("WAITING FOR REWARD...");

        try
        {
            adService.Show(
                () => HandleRewardGranted(attemptId),
                () => HandleAdClosed(attemptId),
                error => HandleAdError(attemptId, error));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CONTINUE] Exception while showing rewarded ad: {ex}", this);
            HandleAdFailure(ContinueAdState.AdFailed, "AD FAILED");
        }
    }

    // =========================
    // UI Setup
    // =========================
    private void EnsureUserInterface()
    {
        if (uiInitialized) return;

        Canvas canvas = ResolveCanvasReference();

        if (!canvas)
        {
            canvas = CreateCanvasWithButtons();
        }
        else
        {
            ConfigureCanvas(canvas);
            ConfigureCanvasScaler(canvas.GetComponent<CanvasScaler>());
            EnsureGraphicRaycaster(canvas);
            DisableCanvasBackgroundRaycast(canvas);
            AttemptButtonLookup(canvas.transform);
            AttemptScoreLabelLookup(canvas.transform);
            EnsureResultControls(canvas.transform);
            ConfigureButtonLayout();
        }

        if (!canvas)
        {
            Debug.LogError("ResultsController: Failed to create or resolve a Canvas instance.");
            return;
        }

        cachedCanvas = canvas;
        EnsureResultDetails(canvas.transform);
        DisableAllTextRaycasts(canvas.transform);
        EnsureEventSystem();
        CleanupDuplicateGameOverTitles(canvas.transform);
        CleanupExternalGameOverTitles();

        uiInitialized = true;
    }

    private Canvas ResolveCanvasReference()
    {
        if (restartButton) return restartButton.GetComponentInParent<Canvas>();
        if (menuButton) return menuButton.GetComponentInParent<Canvas>();
        return GetComponentInChildren<Canvas>();
    }

    private Canvas CreateCanvasWithButtons()
    {
        var canvasGO = new GameObject("ResultsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        ConfigureCanvas(canvas);

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        ConfigureCanvasScaler(scaler);
        EnsureGraphicRaycaster(canvas);
        DisableCanvasBackgroundRaycast(canvas);

        var contentRoot = CreateContentRoot(canvas.transform);

        gameOverTitle = CreateGameOverTitle(contentRoot);

        var scorePanel = CreateScorePanel(contentRoot);
        finalScoreText = CreateLabel(scorePanel, FinalScoreLabelName, Vector2.zero, 60f, "Score: 0", true);
        bestScoreText = CreateLabel(scorePanel, BestScoreLabelName, Vector2.zero, 50f, "Best: 0", true);

        var buttonsRoot = CreateButtonPanel(contentRoot);
        continueButton = CreateButton(buttonsRoot, ContinueButtonName, "Continue");
        restartButton = CreateButton(buttonsRoot, RestartButtonName, "Restart");
        menuButton = CreateButton(buttonsRoot, MenuButtonName, "Menu");

        return canvas;
    }

    private void ConfigureCanvas(Canvas canvas)
    {
        if (!canvas) return;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 0;

        // Legacy scenes may retain a zero-scale Canvas. Presentation should recover safely,
        // rather than leave Results invisible after a valid Game Over transition.
        if (canvas.transform is RectTransform rectTransform && rectTransform.localScale.sqrMagnitude < 0.0001f)
        {
            rectTransform.localScale = Vector3.one;
        }
    }

    private void ConfigureCanvasScaler(CanvasScaler scaler)
    {
        if (!scaler) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureGraphicRaycaster(Canvas canvas)
    {
        if (!canvas) return;
        if (!canvas.TryGetComponent<GraphicRaycaster>(out _))
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void DisableCanvasBackgroundRaycast(Canvas canvas)
    {
        if (!canvas) return;
        if (canvas.TryGetComponent<Image>(out var image))
            image.raycastTarget = false;
    }

    private void DisableAllTextRaycasts(Transform root)
    {
        if (!root) return;
        var labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var label in labels)
            label.raycastTarget = false;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // =========================
    // Binding
    // =========================
    private void BindButtons()
    {
        if (buttonsBound) return;

        var restartBound = BindButton(restartButton, RestartGame, nameof(RestartGame));
        var menuBound = BindButton(menuButton, GoToMenu, nameof(GoToMenu));
        BindContinueButton();

        buttonsBound = restartBound && menuBound;
    }

    private void BindContinueButton()
    {
        if (!continueButton)
        {
            continueButtonBound = false;
            return;
        }

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(OnContinuePressed);
        continueButtonBound = true;
    }

    private bool BindButton(Button button, UnityAction action, string methodName, bool required = true)
    {
        if (!button)
        {
            if (required)
            {
                Debug.LogError($"ResultsController: Missing button for {methodName}.", this);
                return false;
            }
            return true;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
        return true;
    }

    private void UnbindButtons()
    {
        if (!buttonsBound) return;

        restartButton?.onClick.RemoveListener(RestartGame);
        menuButton?.onClick.RemoveListener(GoToMenu);

        if (continueButton && continueButtonBound)
            continueButton.onClick.RemoveAllListeners();

        buttonsBound = false;
        continueButtonBound = false;
    }

    // =========================
    // Lookup / Layout
    // =========================
    private void AttemptButtonLookup(Transform canvasTransform)
    {
        if (!canvasTransform) return;

        if (!continueButton) continueButton = FindButton(canvasTransform, ContinueButtonName);
        if (!restartButton) restartButton = FindButton(canvasTransform, RestartButtonName);
        if (!menuButton) menuButton = FindButton(canvasTransform, MenuButtonName);

        AttemptScoreLabelLookup(canvasTransform);
        ConfigureButtonLayout();
    }

    private Button FindButton(Transform parent, string buttonName)
    {
        var target = parent.Find(buttonName);
        return target ? target.GetComponent<Button>() : null;
    }

    private Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 90f);

        var img = go.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = true;

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.preferredHeight = 90f;
        layout.minHeight = 90f;
        layout.flexibleWidth = 1f;

        var btn = go.GetComponent<Button>();
        CreateButtonLabel(go.transform, label, 36f);
        return btn;
    }

    private void CreateButtonLabel(Transform parent, string textValue, float fontSize)
    {
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(parent, false);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = textValue;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.fontWeight = FontWeight.Bold;
        tmp.color = new Color(0.015f, 0.025f, 0.075f, 1f);
        tmp.raycastTarget = false;

        var rect = tmp.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string labelName, Vector2 anchoredPos, float fontSize, string defaultText, bool addOutline)
    {
        var go = new GameObject(labelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.raycastTarget = false;

        ApplyScoreLabelStyle(tmp, fontSize, addOutline);
        ConfigureScoreLabel(tmp, anchoredPos);
        return tmp;
    }

    private void ApplyScoreLabelStyle(TextMeshProUGUI label, float fontSize, bool addOutline)
    {
        if (!label) return;

        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.fontWeight = FontWeight.Bold;
        label.color = new Color(0.95f, 0.98f, 1f, 1f);
        label.enableWordWrapping = false;
        label.characterSpacing = 2f;

        if (addOutline)
        {
            label.outlineColor = Color.black;
            label.outlineWidth = 0.25f;
        }
        else
        {
            label.outlineWidth = 0f;
        }
    }

    private void ConfigureScoreLabel(TextMeshProUGUI label, Vector2 anchoredPos)
    {
        if (!label) return;

        bool parentHasLayout = label.transform.parent && label.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();

        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        if (parentHasLayout)
        {
            rect.sizeDelta = new Vector2(0f, 120f);

            var layout = label.GetComponent<LayoutElement>();
            if (!layout)
                layout = label.gameObject.AddComponent<LayoutElement>();

            layout.minHeight = 110f;
            layout.preferredHeight = 120f;
        }
        else
        {
            rect.sizeDelta = new Vector2(820f, 140f);
            rect.anchoredPosition = anchoredPos;
        }
    }

    private RectTransform CreateContentRoot(Transform parent)
    {
        var rootGO = new GameObject("ResultsContent", typeof(RectTransform));
        rootGO.transform.SetParent(parent, false);

        var rect = rootGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 1400f);

        var layout = rootGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 28f;
        layout.padding = new RectOffset(0, 0, 96, 64);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = rootGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rect;
    }

    private RectTransform CreateScorePanel(Transform parent)
    {
        var panelGO = new GameObject("ScorePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(parent, false);

        var rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(820f, 260f);

        var bg = panelGO.GetComponent<Image>();
        bg.color = new Color(0.035f, 0.055f, 0.12f, 0.96f);
        bg.raycastTarget = false;

        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 32f;
        layout.padding = new RectOffset(20, 20, 30, 30);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var layoutElement = panelGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 820f;
        layoutElement.minHeight = 300f;

        return rect;
    }

    private TextMeshProUGUI CreateGameOverTitle(Transform parent)
    {
        var go = new GameObject(GameOverTitleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = "RUN ENDED";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 116f;
        label.fontWeight = FontWeight.Bold;
        label.color = new Color(1f, 0.36f, 0.12f, 1f);
        label.outlineColor = new Color(0.01f, 0.02f, 0.07f, 0.92f);
        label.outlineWidth = 0.2f;
        label.raycastTarget = false;

        ConfigureGameOverTitle(label);
        return label;
    }

    private void ConfigureGameOverTitle(TextMeshProUGUI titleLabel)
    {
        if (!titleLabel) return;

        titleLabel.enableWordWrapping = false;

        bool parentHasLayout = titleLabel.transform.parent && titleLabel.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        var rect = titleLabel.rectTransform;

        if (parentHasLayout)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 150f);

            var layout = titleLabel.GetComponent<LayoutElement>();
            if (!layout)
                layout = titleLabel.gameObject.AddComponent<LayoutElement>();

            layout.preferredHeight = 150f;
            layout.minHeight = 140f;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -160f);
            rect.sizeDelta = new Vector2(900f, 170f);
        }
    }

    private RectTransform CreateButtonPanel(Transform parent)
    {
        var panelGO = new GameObject("ButtonPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(parent, false);

        var rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 360f);

        var image = panelGO.GetComponent<Image>();
        image.color = new Color(0.015f, 0.025f, 0.075f, 0.22f);
        image.raycastTarget = false;

        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 28f;
        layout.padding = new RectOffset(30, 30, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var layoutElement = panelGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 420f;
        layoutElement.minHeight = 360f;

        return rect;
    }

    private void ConfigureButtonLayout()
    {
        ApplyButtonStyle(continueButton);
        ApplyButtonStyle(restartButton);
        ApplyButtonStyle(menuButton);
    }

    private static void ApplyButtonStyle(Button button)
    {
        if (!button) return;

        var rect = button.GetComponent<RectTransform>();
        if (rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 90f);
        }

        Color accent = button.name == ContinueButtonName
            ? new Color(0.12f, 0.95f, 1f, 1f)
            : button.name == MenuButtonName
                ? new Color(0.56f, 0.34f, 1f, 1f)
                : new Color(1f, 0.36f, 0.12f, 1f);

        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label)
        {
            label.fontSize = 32f;
            label.fontWeight = FontWeight.Bold;
            label.color = new Color(0.015f, 0.025f, 0.075f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        var image = button.GetComponent<Image>();
        if (image)
        {
            image.color = accent;
        }

        var colors = button.colors;
        colors.normalColor = accent;
        colors.highlightedColor = Color.Lerp(accent, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(accent, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(accent.r, accent.g, accent.b, 0.3f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    // =========================
    // Score + Continue State
    // =========================
    private void AttemptScoreLabelLookup(Transform canvasTransform)
    {
        if (!canvasTransform) return;

        if (!finalScoreText)
        {
            finalScoreText = FindLabel(canvasTransform, FinalScoreLabelName);
        }

        if (!bestScoreText)
        {
            bestScoreText = FindLabel(canvasTransform, BestScoreLabelName);
        }

        if (!resultStatusText)
        {
            resultStatusText = FindLabel(canvasTransform, ResultStatusLabelName);
        }

        if (!gameOverTitle)
        {
            var foundTitle = FindLabel(canvasTransform, GameOverTitleName);
            if (foundTitle)
            {
                ConfigureGameOverTitle(foundTitle);
                gameOverTitle = foundTitle;
            }
        }

        if (finalScoreText)
        {
            ApplyScoreLabelStyle(finalScoreText, 60f, true);
            ConfigureScoreLabel(finalScoreText, new Vector2(0f, -360f));
        }

        if (bestScoreText)
        {
            ApplyScoreLabelStyle(bestScoreText, 50f, true);
            ConfigureScoreLabel(bestScoreText, new Vector2(0f, -440f));
        }

        CleanupDuplicateGameOverTitles(canvasTransform);
        CleanupExternalGameOverTitles();
    }

    private void EnsureResultDetails(Transform canvasTransform)
    {
        if (!canvasTransform)
        {
            return;
        }

        AttemptScoreLabelLookup(canvasTransform);
        resultStatusText = resultStatusText ? resultStatusText : CreateResultStatusLabel(canvasTransform);
        ConfigureResultStatusLabel(resultStatusText);
    }

    private void EnsureResultControls(Transform canvasTransform)
    {
        if (!canvasTransform)
        {
            return;
        }

        var contentRoot = canvasTransform.Find("ResultsContent") as RectTransform;
        if (!contentRoot)
        {
            contentRoot = CreateContentRoot(canvasTransform);
            contentRoot.anchoredPosition = new Vector2(0f, -96f);
        }

        var scorePanel = contentRoot.Find("ScorePanel");
        if (!scorePanel)
        {
            scorePanel = CreateScorePanel(contentRoot);
        }

        if (!finalScoreText)
        {
            finalScoreText = CreateLabel(scorePanel, FinalScoreLabelName, Vector2.zero, 60f, "Score: 0", true);
        }

        if (!bestScoreText)
        {
            bestScoreText = CreateLabel(scorePanel, BestScoreLabelName, Vector2.zero, 50f, "Best: 0", true);
        }

        var buttonPanel = contentRoot.Find("ButtonPanel");
        if (!buttonPanel)
        {
            buttonPanel = CreateButtonPanel(contentRoot);
        }

        if (!continueButton)
        {
            continueButton = CreateButton(buttonPanel, ContinueButtonName, "Continue");
        }

        if (!restartButton)
        {
            restartButton = CreateButton(buttonPanel, RestartButtonName, "Restart");
        }

        if (!menuButton)
        {
            menuButton = CreateButton(buttonPanel, MenuButtonName, "Menu");
        }
    }

    private TextMeshProUGUI CreateResultStatusLabel(Transform parent)
    {
        var labelObject = new GameObject(ResultStatusLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        return label;
    }

    private static void ConfigureResultStatusLabel(TextMeshProUGUI label)
    {
        if (!label)
        {
            return;
        }

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 30f;
        label.fontWeight = FontWeight.Bold;
        label.color = new Color(0.62f, 0.82f, 1f, 1f);
        label.characterSpacing = 1.5f;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -105f);
        rect.sizeDelta = new Vector2(900f, 72f);
    }

    private void CleanupDuplicateGameOverTitles(Transform root)
    {
        if (!root) return;

        var titles = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var label in titles)
        {
            if (label == gameOverTitle) continue;

            string text = label.text;
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (!IsGameOverText(text))
                continue;

            if (!gameOverTitle)
            {
                gameOverTitle = label;
                ConfigureGameOverTitle(gameOverTitle);
                continue;
            }

            Destroy(label.gameObject);
        }
    }

    private void CleanupExternalGameOverTitles()
    {
        var titles = FindObjectsOfType<TextMeshProUGUI>(true);
        var allowedRoot = cachedCanvas ? cachedCanvas.transform : null;

        foreach (var label in titles)
        {
            if (label == gameOverTitle) continue;
            if (!IsGameOverText(label.text)) continue;

            if (allowedRoot && label.transform.IsChildOf(allowedRoot))
                continue;

            Destroy(label.gameObject);
        }
    }

    private bool IsGameOverText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = string.Join(
            " ",
            text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
        );

        return string.Equals(normalized, "GAME OVER", StringComparison.OrdinalIgnoreCase);
    }

    private TextMeshProUGUI FindLabel(Transform parent, string labelName)
    {
        var target = parent.Find(labelName);
        return target ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private void UpdateScoreLabels()
    {
        int finalScore = 0;
        int storedBest = PlayerPrefs.GetInt(BestScoreKey, 0);
        int snapshotBest = storedBest;

        if (ScoreSnapshot.HasValue)
        {
            finalScore = ScoreSnapshot.LastScore;
            snapshotBest = ScoreSnapshot.LastBest;
        }

        int bestScore = Mathf.Max(storedBest, snapshotBest, finalScore);
        ResultsPresentation.DisplayData display = ResultsPresentation.Build(
            finalScore,
            bestScore,
            ScoreSnapshot.HasValue && ScoreSnapshot.LastRunSetNewBest,
            ScoreSnapshot.LastLossReason);

        // ✅ حفظ مضمون
        if (bestScore != storedBest)
        {
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.Save();
        }

        if (finalScoreText)
            finalScoreText.text = display.FinalScoreText;
        else if (!missingFinalScoreLabelLogged)
        {
            Debug.LogError("ResultsController: FinalScoreText missing.", this);
            missingFinalScoreLabelLogged = true;
        }

        if (bestScoreText)
            bestScoreText.text = display.BestScoreText;
        else if (!missingBestScoreLabelLogged)
        {
            Debug.LogError("ResultsController: BestScoreText missing.", this);
            missingBestScoreLabelLogged = true;
        }

        if (resultStatusText)
        {
            resultStatusText.text = display.StatusText;
        }
    }

    private bool TryBeginNavigationRequest()
    {
        if (continueRequestInProgress)
        {
            return false;
        }

        if (navigationRequestInProgress)
        {
            return false;
        }

        navigationRequestInProgress = true;
        return true;
    }

    private void RefreshContinueState()
    {
        // ✅ يعتمد على منطقك الحالي: مرة وحدة لكل Run
        continueAvailable = forceShowContinueForTesting || ScoreSnapshot.CanContinue;

        if (!continueRequestInProgress)
        {
            continueAdState = ContinueAdState.Idle;
        }

        ApplyButtonStates();

        if (continueRequestInProgress)
        {
            return;
        }

        if (!continueButton)
            return;

        continueButton.gameObject.SetActive(continueAvailable);
    }

    private void ApplyButtonStates()
    {
        bool adActive = continueRequestInProgress;

        if (continueButton)
        {
            continueButton.gameObject.SetActive(continueAvailable);
            continueButton.interactable = continueAvailable && !adActive;
        }

        if (restartButton)
        {
            restartButton.interactable = !adActive && !navigationRequestInProgress;
        }

        if (menuButton)
        {
            menuButton.interactable = !adActive && !navigationRequestInProgress;
        }
    }

    private IRewardedAdService ResolveRewardedAdService()
    {
        if (rewardedAdServiceSource is IRewardedAdService serializedService)
        {
            rewardedAdService = serializedService;
            return rewardedAdService;
        }

        if (rewardedAdService is MonoBehaviour cachedBehaviour && cachedBehaviour)
        {
            return rewardedAdService;
        }

        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IRewardedAdService service)
            {
                rewardedAdService = service;
                return rewardedAdService;
            }
        }

        rewardedAdService = null;
        return null;
    }

    private void HandleRewardGranted(int attemptId)
    {
        if (!IsActiveAttempt(attemptId) || continueAdState == ContinueAdState.RewardGranted)
        {
            return;
        }

        continueAdState = ContinueAdState.RewardGranted;
        SetStatusMessage("REWARD GRANTED");

        bool ok = false;
        try
        {
            ok = GameController.ContinueRun();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CONTINUE] Exception in ContinueRun(): {ex}", this);
            ok = false;
        }

        Debug.Log($"[CONTINUE] ContinueRun() => {ok}", this);

        if (ok)
        {
            continueAvailable = false;
            return;
        }

        HandleAdFailure(ContinueAdState.AdFailed, "CONTINUE FAILED");
    }

    private void HandleAdClosed(int attemptId)
    {
        if (!IsActiveAttempt(attemptId) || continueAdState == ContinueAdState.RewardGranted)
        {
            return;
        }

        Debug.LogWarning("[CONTINUE] Rewarded ad closed without reward", this);
        HandleAdFailure(ContinueAdState.AdClosedWithoutReward, "NO REWARD GRANTED");
    }

    private void HandleAdError(int attemptId, string error)
    {
        if (!IsActiveAttempt(attemptId) || continueAdState == ContinueAdState.RewardGranted)
        {
            return;
        }

        Debug.LogWarning($"[CONTINUE] Rewarded ad failed: {error}", this);
        HandleAdFailure(ContinueAdState.AdFailed, "AD FAILED");
    }

    private bool IsActiveAttempt(int attemptId)
    {
        return continueRequestInProgress && continueAttemptId == attemptId;
    }

    private void HandleAdFailure(ContinueAdState failureState, string statusMessage)
    {
        continueAdState = failureState;
        continueRequestInProgress = false;
        SetStatusMessage(statusMessage);
        RefreshContinueState();
    }

    private void SetStatusMessage(string message)
    {
        if (resultStatusText)
        {
            resultStatusText.text = message;
        }
    }

    private void LogUiDiagnostics()
    {
        var canvas = cachedCanvas ? cachedCanvas : ResolveCanvasReference();
        bool hasEventSystem = FindObjectOfType<EventSystem>() != null;
        bool hasRaycaster = canvas && canvas.GetComponent<GraphicRaycaster>() != null;

        Debug.Log($"[RESULTS] EventSystem={hasEventSystem} Raycaster={hasRaycaster}", this);
    }
}
