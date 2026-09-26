using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Builds the lightweight TimeRush menu at runtime while preserving the existing
/// MenuHub -> Game scene contract.
/// </summary>
public class MenuHubUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private FeedbackConfig feedbackConfig;
    private const string BestScoreKey = "BEST_SCORE";

    private static readonly Color Ink = new Color(0.015f, 0.025f, 0.075f, 1f);
    private static readonly Color Panel = new Color(0.035f, 0.055f, 0.12f, 0.96f);
    private static readonly Color Cyan = new Color(0.12f, 0.95f, 1f, 1f);
    private static readonly Color Orange = new Color(1f, 0.36f, 0.12f, 1f);
    private static readonly Color Violet = new Color(0.56f, 0.34f, 1f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.7f, 0.86f, 1f);
    private bool startRequestInProgress;

    private bool cameraShakeEnabled;
    private bool reduceFlashingEnabled;
    private bool audioEnabled;
    private ProgressionConfig progressionConfig;

    private void Awake()
    {
        cameraShakeEnabled = FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig);
        reduceFlashingEnabled = FeedbackPreferences.IsReduceFlashingEnabled(feedbackConfig);
        audioEnabled = FeedbackPreferences.IsAudioEnabled(feedbackConfig);

        progressionConfig = Resources.Load<ProgressionConfig>("ProgressionConfig");
        if (!progressionConfig)
        {
            progressionConfig = ProgressionConfig.CreateDefault();
        }

        var document = GetComponent<UIDocument>();
        if (!document)
        {
            document = gameObject.AddComponent<UIDocument>();
        }

        if (!document.panelSettings)
        {
            document.panelSettings = Resources.Load<PanelSettings>("DefaultPanelSettings");
        }

        BuildUI(document);
    }

    // Layout below is written portrait-first: the shared DefaultPanelSettings asset now scales
    // by matching screen HEIGHT (see its m_Match/m_ReferenceResolution), so the panel's virtual
    // width tracks the device's real aspect ratio instead of assuming a fixed landscape canvas.
    // Every block here therefore sizes itself in percentages / flex, with only a single portrait
    // -oriented maxWidth cap on the frame -- so it fits phone screens narrow or wide without a
    // separate "compact" branch, and still looks fine in the wider editor Game view.
    private const float FrameMaxWidth = 640f;

    private void BuildUI(UIDocument document)
    {
        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.style.backgroundColor = Ink;
        root.style.paddingLeft = 24f;
        root.style.paddingRight = 24f;
        root.style.paddingTop = 36f;
        root.style.paddingBottom = 28f;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        var frame = new VisualElement();
        frame.style.width = new Length(100f, LengthUnit.Percent);
        frame.style.maxWidth = FrameMaxWidth;
        // Fills the available portrait height (instead of a fixed landscape-tuned minHeight),
        // so the menu reads as a proper full-screen mobile page rather than a small floating box.
        frame.style.flexGrow = 1f;
        frame.style.backgroundColor = Panel;
        frame.style.borderLeftWidth = 2f;
        frame.style.borderRightWidth = 2f;
        frame.style.borderTopWidth = 2f;
        frame.style.borderBottomWidth = 2f;
        frame.style.borderLeftColor = Violet;
        frame.style.borderRightColor = Violet;
        frame.style.borderTopColor = Violet;
        frame.style.borderBottomColor = Violet;
        frame.style.borderTopLeftRadius = 22f;
        frame.style.borderTopRightRadius = 22f;
        frame.style.borderBottomLeftRadius = 22f;
        frame.style.borderBottomRightRadius = 22f;
        frame.style.paddingLeft = 28f;
        frame.style.paddingRight = 28f;
        frame.style.paddingTop = 30f;
        frame.style.paddingBottom = 24f;
        frame.style.alignItems = Align.Stretch;
        frame.style.justifyContent = Justify.FlexStart;
        root.Add(frame);

        var header = new VisualElement();
        header.style.width = new Length(100f, LengthUnit.Percent);
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.flexShrink = 0f;
        frame.Add(header);

        // Vertically centers the hero title, accessibility card, and CTA as one group in
        // whatever space is left between the header and footer, instead of clustering them all
        // at the top and leaving a large dead gap above the footer on tall phone screens.
        var contentColumn = new VisualElement();
        contentColumn.style.width = new Length(100f, LengthUnit.Percent);
        contentColumn.style.flexGrow = 1f;
        contentColumn.style.flexDirection = FlexDirection.Column;
        contentColumn.style.justifyContent = Justify.Center;
        contentColumn.style.alignItems = Align.Stretch;
        contentColumn.style.flexShrink = 0f;
        frame.Add(contentColumn);

        var mark = new Label("TR");
        mark.style.color = Cyan;
        mark.style.fontSize = 20f;
        mark.style.unityFontStyleAndWeight = FontStyle.Bold;
        mark.style.letterSpacing = 3f;
        header.Add(mark);

        var headerInfo = new VisualElement();
        headerInfo.style.flexDirection = FlexDirection.Column;
        headerInfo.style.alignItems = Align.FlexEnd;
        headerInfo.style.justifyContent = Justify.Center;
        headerInfo.style.flexShrink = 1f;
        headerInfo.style.maxWidth = new Length(70f, LengthUnit.Percent);
        header.Add(headerInfo);

        int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        var progression = ProgressionProfile.Load();
        var rank = progressionConfig.ResolveRank(bestScore);
        var meta = new Label($"{rank.name}   //   BEST {bestScore}   //   RUNS {progression.TotalRuns}");
        meta.style.color = Muted;
        meta.style.fontSize = 11f;
        meta.style.unityTextAlign = TextAnchor.MiddleRight;
        meta.style.whiteSpace = WhiteSpace.Normal;
        headerInfo.Add(meta);

        // Surface next-rank progress (or MAX RANK) as a single quiet line beneath the meta summary.
        var rankProgress = RankProgression.Evaluate(progressionConfig, bestScore);
        string rankProgressText = rankProgress.IsMaxRank
            ? "MAX RANK"
            : $"NEXT {rankProgress.NextRankName}   //   {rankProgress.PointsToNext} TO GO";
        var rankProgressLabel = new Label(rankProgressText);
        rankProgressLabel.style.color = Muted;
        rankProgressLabel.style.fontSize = 10f;
        rankProgressLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        rankProgressLabel.style.whiteSpace = WhiteSpace.Normal;
        rankProgressLabel.style.marginTop = 2f;
        headerInfo.Add(rankProgressLabel);

        var titleBlock = new VisualElement();
        titleBlock.style.marginTop = 8f;
        titleBlock.style.marginBottom = 0f;
        titleBlock.style.width = new Length(100f, LengthUnit.Percent);
        titleBlock.style.flexShrink = 0f;
        contentColumn.Add(titleBlock);

        var eyebrow = new Label("MOVE WITH THE CLOCK");
        eyebrow.style.color = Orange;
        eyebrow.style.fontSize = 13f;
        eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
        eyebrow.style.letterSpacing = 2f;
        titleBlock.Add(eyebrow);

        var title = new Label("TIME RUSH");
        title.style.color = Color.white;
        title.style.fontSize = 54f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleLeft;
        title.style.letterSpacing = -0.5f;
        title.style.whiteSpace = WhiteSpace.Normal;
        title.style.marginTop = 6f;
        title.style.marginBottom = 4f;
        title.style.flexShrink = 0f;
        titleBlock.Add(title);

        var subtitle = new Label("Three lanes. Read the gap. Shift depth when the line closes.");
        subtitle.style.color = Muted;
        subtitle.style.fontSize = 15f;
        subtitle.style.marginTop = 6f;
        subtitle.style.whiteSpace = WhiteSpace.Normal;
        subtitle.style.flexShrink = 0f;
        titleBlock.Add(subtitle);

        var settingsPanel = new VisualElement();
        settingsPanel.style.width = new Length(100f, LengthUnit.Percent);
        settingsPanel.style.marginTop = 20f;
        settingsPanel.style.marginBottom = 20f;
        settingsPanel.style.paddingLeft = 16f;
        settingsPanel.style.paddingRight = 16f;
        settingsPanel.style.paddingTop = 10f;
        settingsPanel.style.paddingBottom = 8f;
        settingsPanel.style.flexDirection = FlexDirection.Column;
        settingsPanel.style.alignItems = Align.Stretch;
        settingsPanel.style.flexShrink = 0f;
        settingsPanel.style.backgroundColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.05f);
        settingsPanel.style.borderLeftWidth = 1f;
        settingsPanel.style.borderRightWidth = 1f;
        settingsPanel.style.borderTopWidth = 1f;
        settingsPanel.style.borderBottomWidth = 1f;
        settingsPanel.style.borderLeftColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        settingsPanel.style.borderRightColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        settingsPanel.style.borderTopColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        settingsPanel.style.borderBottomColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        settingsPanel.style.borderTopLeftRadius = 14f;
        settingsPanel.style.borderTopRightRadius = 14f;
        settingsPanel.style.borderBottomLeftRadius = 14f;
        settingsPanel.style.borderBottomRightRadius = 14f;
        contentColumn.Add(settingsPanel);

        var settingsHeader = new Label("ACCESSIBILITY");
        settingsHeader.style.color = Cyan;
        settingsHeader.style.fontSize = 12f;
        settingsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        settingsHeader.style.letterSpacing = 1.5f;
        settingsHeader.style.marginBottom = 4f;
        settingsPanel.Add(settingsHeader);

        settingsPanel.Add(CreateSettingRow(
            "Camera Shake",
            () => cameraShakeEnabled,
            enabled =>
            {
                cameraShakeEnabled = enabled;
                FeedbackPreferences.SetCameraShakeEnabled(enabled);
            }));

        settingsPanel.Add(CreateSettingRow(
            "Reduce Flashing",
            () => reduceFlashingEnabled,
            enabled =>
            {
                reduceFlashingEnabled = enabled;
                FeedbackPreferences.SetReduceFlashingEnabled(enabled);
            }));

        settingsPanel.Add(CreateSettingRow(
            "Audio",
            () => audioEnabled,
            enabled =>
            {
                audioEnabled = enabled;
                FeedbackPreferences.SetAudioEnabled(enabled);
            }));

        // A single full-width, thumb-friendly CTA stacked above the control hint -- rather than
        // a side-by-side row -- so it never gets squeezed on narrow phone widths and matches how
        // mobile game menus are normally laid out.
        var actionRow = new VisualElement();
        actionRow.style.width = new Length(100f, LengthUnit.Percent);
        actionRow.style.marginTop = 4f;
        actionRow.style.flexDirection = FlexDirection.Column;
        actionRow.style.alignItems = Align.Stretch;
        actionRow.style.flexShrink = 0f;
        contentColumn.Add(actionRow);

        var startButton = new Button(StartRun)
        {
            text = "START RUN  →"
        };
        startButton.style.width = new Length(100f, LengthUnit.Percent);
        startButton.style.height = 64f;
        startButton.style.backgroundColor = Orange;
        startButton.style.color = Color.white;
        startButton.style.fontSize = 21f;
        startButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        startButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        startButton.style.borderTopWidth = 0f;
        startButton.style.borderBottomWidth = 0f;
        startButton.style.borderLeftWidth = 0f;
        startButton.style.borderRightWidth = 0f;
        startButton.style.borderTopLeftRadius = 16f;
        startButton.style.borderTopRightRadius = 16f;
        startButton.style.borderBottomLeftRadius = 16f;
        startButton.style.borderBottomRightRadius = 16f;
        startButton.style.flexShrink = 0f;
        startButton.RegisterCallback<PointerEnterEvent>(_ => startButton.style.backgroundColor = Cyan);
        startButton.RegisterCallback<PointerLeaveEvent>(_ => startButton.style.backgroundColor = Orange);
        actionRow.Add(startButton);

        var hint = new Label("A / D  or  ← / →  //  lane\nW / S  or  ↑ / ↓  //  depth\nSwipe ← / →  lane   //   ↑ / ↓  depth");
        hint.style.color = Muted;
        hint.style.fontSize = 12f;
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.marginTop = 12f;
        hint.style.flexShrink = 0f;
        hint.style.alignSelf = Align.Center;
        actionRow.Add(hint);

        var footer = new Label("SURVIVE LONGER  •  CHANGE LANES EARLY  •  NEVER STOP MOVING");
        footer.style.color = Violet;
        footer.style.fontSize = 11f;
        footer.style.unityFontStyleAndWeight = FontStyle.Bold;
        footer.style.letterSpacing = 0.6f;
        footer.style.unityTextAlign = TextAnchor.MiddleCenter;
        footer.style.whiteSpace = WhiteSpace.Normal;
        footer.style.width = new Length(100f, LengthUnit.Percent);
        footer.style.marginTop = 16f;
        footer.style.flexShrink = 0f;
        frame.Add(footer);
    }

    private VisualElement CreateSettingRow(string labelText, System.Func<bool> getter, System.Action<bool> setter)
    {
        var row = new VisualElement();
        row.style.width = new Length(100f, LengthUnit.Percent);
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.minHeight = 42f;
        row.style.marginTop = 4f;

        var label = new Label(labelText);
        label.style.color = Color.white;
        label.style.fontSize = 14f;
        label.style.flexGrow = 1f;
        label.style.flexShrink = 1f;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginRight = 16f;
        row.Add(label);

        var button = new Button();
        button.style.width = 96f;
        button.style.height = 38f;
        button.style.flexShrink = 0f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = 13f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.borderTopWidth = 0f;
        button.style.borderBottomWidth = 0f;
        button.style.borderLeftWidth = 0f;
        button.style.borderRightWidth = 0f;
        button.style.borderTopLeftRadius = 10f;
        button.style.borderTopRightRadius = 10f;
        button.style.borderBottomLeftRadius = 10f;
        button.style.borderBottomRightRadius = 10f;

        void Refresh()
        {
            bool value = getter();
            button.text = value ? "ON" : "OFF";
            button.style.backgroundColor = value ? Cyan : Muted;
            button.style.color = value ? Ink : Ink;
        }

        button.clicked += () =>
        {
            setter(!getter());
            Refresh();
        };

        Refresh();
        row.Add(button);
        return row;
    }

    private void StartRun()
    {
        if (startRequestInProgress)
        {
            return;
        }

        if (GameStateMachine.HasInstance)
        {
            startRequestInProgress = true;
            if (!GameStateMachine.Instance.StartRunFromMenu())
            {
                startRequestInProgress = false;
            }
            return;
        }

        startRequestInProgress = true;
        SceneManager.LoadScene(gameSceneName);
    }
}
