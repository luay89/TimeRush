using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Presents pause state through a resolution-independent uGUI canvas.
/// It surfaces a touch-friendly pause button while playing and a settings overlay while paused,
/// but never stops gameplay or changes player state itself; all transitions route through
/// the existing <see cref="GameStateMachine"/>.
/// </summary>
public sealed class PauseOverlayPresenter : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    private static readonly Color Ink = new Color(0.015f, 0.025f, 0.075f, 1f);
    private static readonly Color Panel = new Color(0.035f, 0.055f, 0.12f, 0.98f);
    private static readonly Color Dim = new Color(0.01f, 0.02f, 0.06f, 0.72f);
    private static readonly Color Cyan = new Color(0.12f, 0.95f, 1f, 1f);
    private static readonly Color Violet = new Color(0.56f, 0.34f, 1f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.7f, 0.86f, 1f);

    private readonly List<Action> toggleRefreshers = new List<Action>();

    private GameObject pauseButton;
    private GameObject overlayRoot;
    private bool uiBuilt;
    private GameStateKind lastAppliedState = (GameStateKind)(-1);

    private void Awake()
    {
        EnsureUserInterface();
        ApplyState(ResolveState(), force: true);
    }

    private void Update()
    {
        GameStateKind state = ResolveState();
        if (state != lastAppliedState)
        {
            ApplyState(state, force: false);
        }
    }

    private static GameStateKind ResolveState()
    {
        return GameStateMachine.HasInstance ? GameStateMachine.Instance.CurrentState : GameStateKind.Boot;
    }

    private void ApplyState(GameStateKind state, bool force)
    {
        bool playing = state == GameStateKind.Playing;
        bool paused = state == GameStateKind.Paused;

        if (playing || paused)
        {
            EnsureEventSystem();
        }

        if (paused)
        {
            RefreshToggles();
        }

        if (pauseButton)
        {
            pauseButton.SetActive(playing);
        }

        if (overlayRoot)
        {
            overlayRoot.SetActive(paused);
        }

        lastAppliedState = state;
    }

    // =========================
    // UI construction
    // =========================
    private void EnsureUserInterface()
    {
        if (uiBuilt)
        {
            return;
        }

        var canvasGO = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 500;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var safeArea = CreateStretchElement(canvasGO.transform, "SafeArea");
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        BuildPauseButton(safeArea);
        BuildOverlay(safeArea);

        pauseButton.SetActive(false);
        overlayRoot.SetActive(false);
        uiBuilt = true;
    }

    private void BuildPauseButton(RectTransform parent)
    {
        var go = new GameObject("PauseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -36f);
        rect.sizeDelta = new Vector2(120f, 120f);

        var image = go.GetComponent<Image>();
        image.color = new Color(Panel.r, Panel.g, Panel.b, 0.9f);
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
        colors.pressedColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f);
        button.colors = colors;
        button.onClick.AddListener(OnPauseButtonPressed);

        var label = CreateLabel(go.transform, "Glyph", "II", 56f, Cyan, TextAlignmentOptions.Center);
        label.characterSpacing = 8f;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        pauseButton = go;
    }

    private void BuildOverlay(RectTransform parent)
    {
        var root = CreateStretchElement(parent, "PauseOverlay");
        var dim = root.gameObject.AddComponent<Image>();
        dim.color = Dim;
        dim.raycastTarget = true;

        var panelGO = new GameObject("PausePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGO.transform.SetParent(root, false);

        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 0f);

        var panelImage = panelGO.GetComponent<Image>();
        panelImage.color = Panel;
        panelImage.raycastTarget = true;

        var layout = panelGO.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(56, 56, 48, 48);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var sizeFitter = panelGO.GetComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = CreateLabel(panelGO.transform, "PausedTitle", "PAUSED", 88f, Color.white, TextAlignmentOptions.Center);
        title.fontWeight = FontWeight.Bold;
        AddLayoutHeight(title.gameObject, 108f);

        var settingsHeader = CreateLabel(panelGO.transform, "SettingsHeader", "ACCESSIBILITY", 24f, Cyan, TextAlignmentOptions.Center);
        settingsHeader.characterSpacing = 4f;
        settingsHeader.fontWeight = FontWeight.Bold;
        AddLayoutHeight(settingsHeader.gameObject, 40f);

        CreateToggleRow(
            panelGO.transform,
            "Camera Shake",
            () => FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig),
            FeedbackPreferences.SetCameraShakeEnabled);

        CreateToggleRow(
            panelGO.transform,
            "Reduce Flashing",
            () => FeedbackPreferences.IsReduceFlashingEnabled(feedbackConfig),
            FeedbackPreferences.SetReduceFlashingEnabled);

        CreateToggleRow(
            panelGO.transform,
            "Audio",
            () => FeedbackPreferences.IsAudioEnabled(feedbackConfig),
            FeedbackPreferences.SetAudioEnabled);

        var resume = CreateActionButton(panelGO.transform, "ResumeButton", "RESUME", OnResumePressed);
        AddLayoutHeight(resume, 96f);

        overlayRoot = root.gameObject;
    }

    private void CreateToggleRow(Transform parent, string labelText, Func<bool> getter, Action<bool> setter)
    {
        var rowGO = new GameObject(labelText + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGO.transform.SetParent(parent, false);

        var rowLayout = rowGO.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = 16f;
        AddLayoutHeight(rowGO, 68f);

        var label = CreateLabel(rowGO.transform, "Label", labelText, 30f, Color.white, TextAlignmentOptions.Left);
        var labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minHeight = 60f;

        var buttonGO = new GameObject("Toggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(rowGO.transform, false);

        var buttonImage = buttonGO.GetComponent<Image>();
        buttonImage.raycastTarget = true;

        var buttonLayout = buttonGO.AddComponent<LayoutElement>();
        buttonLayout.minWidth = 160f;
        buttonLayout.preferredWidth = 160f;
        buttonLayout.minHeight = 60f;
        buttonLayout.preferredHeight = 60f;

        var buttonLabel = CreateLabel(buttonGO.transform, "State", "OFF", 26f, Ink, TextAlignmentOptions.Center);
        buttonLabel.fontWeight = FontWeight.Bold;
        var buttonLabelRect = buttonLabel.rectTransform;
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;

        void Refresh()
        {
            bool value = getter();
            buttonLabel.text = value ? "ON" : "OFF";
            buttonImage.color = value ? Cyan : Muted;
        }

        var button = buttonGO.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            setter(!getter());
            Refresh();
        });

        Refresh();
        toggleRefreshers.Add(Refresh);
    }

    private GameObject CreateActionButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = Violet;
        image.raycastTarget = true;

        var label = CreateLabel(go.transform, "Label", text, 34f, Color.white, TextAlignmentOptions.Center);
        label.fontWeight = FontWeight.Bold;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        return go;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateStretchElement(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void AddLayoutHeight(GameObject go, float height)
    {
        var layout = go.GetComponent<LayoutElement>();
        if (!layout)
        {
            layout = go.AddComponent<LayoutElement>();
        }

        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private void RefreshToggles()
    {
        for (int i = 0; i < toggleRefreshers.Count; i++)
        {
            toggleRefreshers[i]?.Invoke();
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // =========================
    // Actions
    // =========================
    private void OnPauseButtonPressed()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.TogglePause();
        }
    }

    private void OnResumePressed()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.Resume();
        }
    }
}
