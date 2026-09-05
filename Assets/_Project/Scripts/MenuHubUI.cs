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

    private void Awake()
    {
        cameraShakeEnabled = FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig);
        reduceFlashingEnabled = FeedbackPreferences.IsReduceFlashingEnabled(feedbackConfig);
        audioEnabled = FeedbackPreferences.IsAudioEnabled(feedbackConfig);

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

    private void BuildUI(UIDocument document)
    {
        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.style.backgroundColor = Ink;
        root.style.paddingLeft = 64f;
        root.style.paddingRight = 64f;
        root.style.paddingTop = 56f;
        root.style.paddingBottom = 56f;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        var frame = new VisualElement();
        frame.style.width = new Length(100f, LengthUnit.Percent);
        frame.style.maxWidth = 960f;
        frame.style.minHeight = 620f;
        frame.style.backgroundColor = Panel;
        frame.style.borderLeftWidth = 2f;
        frame.style.borderRightWidth = 2f;
        frame.style.borderTopWidth = 2f;
        frame.style.borderBottomWidth = 2f;
        frame.style.borderLeftColor = Violet;
        frame.style.borderRightColor = Violet;
        frame.style.borderTopColor = Violet;
        frame.style.borderBottomColor = Violet;
        frame.style.paddingLeft = 72f;
        frame.style.paddingRight = 72f;
        frame.style.paddingTop = 62f;
        frame.style.paddingBottom = 62f;
        frame.style.alignItems = Align.FlexStart;
        frame.style.justifyContent = Justify.FlexStart;
        root.Add(frame);

        var header = new VisualElement();
        header.style.width = new Length(100f, LengthUnit.Percent);
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        frame.Add(header);

        var contentColumn = new VisualElement();
        contentColumn.style.width = new Length(100f, LengthUnit.Percent);
        contentColumn.style.flexGrow = 1f;
        contentColumn.style.flexDirection = FlexDirection.Column;
        contentColumn.style.justifyContent = Justify.FlexStart;
        contentColumn.style.alignItems = Align.FlexStart;
        contentColumn.style.flexShrink = 0f;
        frame.Add(contentColumn);

        var mark = new Label("TR");
        mark.style.color = Cyan;
        mark.style.fontSize = 26f;
        mark.style.unityFontStyleAndWeight = FontStyle.Bold;
        mark.style.letterSpacing = 4f;
        header.Add(mark);

        var meta = new Label($"BEST  {PlayerPrefs.GetInt(BestScoreKey, 0)}   //   ENDLESS DODGE");
        meta.style.color = Muted;
        meta.style.fontSize = 14f;
        meta.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(meta);

        var titleBlock = new VisualElement();
        titleBlock.style.marginTop = 32f;
        titleBlock.style.marginBottom = 0f;
        titleBlock.style.width = new Length(100f, LengthUnit.Percent);
        titleBlock.style.maxWidth = 720f;
        titleBlock.style.flexShrink = 0f;
        contentColumn.Add(titleBlock);

        var eyebrow = new Label("MOVE WITH THE CLOCK");
        eyebrow.style.color = Orange;
        eyebrow.style.fontSize = 16f;
        eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
        eyebrow.style.letterSpacing = 2f;
        titleBlock.Add(eyebrow);

        var title = new Label("TIME RUSH");
        title.style.color = Color.white;
        title.style.fontSize = 92f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleLeft;
        title.style.letterSpacing = -1f;
        title.style.marginTop = 8f;
        title.style.marginBottom = 4f;
        title.style.flexShrink = 0f;
        titleBlock.Add(title);

        var subtitle = new Label("Three lanes. Read the gap. Shift depth when the line closes.");
        subtitle.style.color = Muted;
        subtitle.style.fontSize = 23f;
        subtitle.style.marginTop = 8f;
        subtitle.style.maxWidth = 700f;
        subtitle.style.flexShrink = 0f;
        titleBlock.Add(subtitle);

        var settingsPanel = new VisualElement();
        settingsPanel.style.width = new Length(100f, LengthUnit.Percent);
        settingsPanel.style.maxWidth = 520f;
        settingsPanel.style.marginTop = 18f;
        settingsPanel.style.marginBottom = 20f;
        settingsPanel.style.paddingLeft = 18f;
        settingsPanel.style.paddingRight = 18f;
        settingsPanel.style.paddingTop = 10f;
        settingsPanel.style.paddingBottom = 10f;
        settingsPanel.style.flexDirection = FlexDirection.Column;
        settingsPanel.style.alignItems = Align.Stretch;
        settingsPanel.style.flexShrink = 0f;
        settingsPanel.style.borderLeftWidth = 1f;
        settingsPanel.style.borderRightWidth = 1f;
        settingsPanel.style.borderTopWidth = 1f;
        settingsPanel.style.borderBottomWidth = 1f;
        settingsPanel.style.borderLeftColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        settingsPanel.style.borderRightColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        settingsPanel.style.borderTopColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        settingsPanel.style.borderBottomColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        contentColumn.Add(settingsPanel);

        var settingsHeader = new Label("ACCESSIBILITY");
        settingsHeader.style.color = Cyan;
        settingsHeader.style.fontSize = 14f;
        settingsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        settingsHeader.style.letterSpacing = 1.5f;
        settingsHeader.style.marginBottom = 6f;
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

        var actionRow = new VisualElement();
        actionRow.style.width = new Length(100f, LengthUnit.Percent);
        actionRow.style.marginTop = 0f;
        actionRow.style.minHeight = 74f;
        actionRow.style.flexDirection = FlexDirection.Row;
        actionRow.style.alignItems = Align.Center;
        actionRow.style.justifyContent = Justify.SpaceBetween;
        actionRow.style.flexShrink = 0f;
        contentColumn.Add(actionRow);

        var startButton = new Button(StartRun)
        {
            text = "START RUN  →"
        };
        startButton.style.width = 320f;
        startButton.style.height = 74f;
        startButton.style.backgroundColor = Orange;
        startButton.style.color = Color.white;
        startButton.style.fontSize = 24f;
        startButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        startButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        startButton.style.borderTopWidth = 0f;
        startButton.style.borderBottomWidth = 0f;
        startButton.style.borderLeftWidth = 0f;
        startButton.style.borderRightWidth = 0f;
        startButton.style.flexShrink = 0f;
        startButton.RegisterCallback<PointerEnterEvent>(_ => startButton.style.backgroundColor = Cyan);
        startButton.RegisterCallback<PointerLeaveEvent>(_ => startButton.style.backgroundColor = Orange);
        actionRow.Add(startButton);

        var hint = new Label("A / D  or  ← / →  //  lane\nW / S  or  ↑ / ↓  //  depth\nSwipe left or right on touch");
        hint.style.color = Muted;
        hint.style.fontSize = 16f;
        hint.style.unityTextAlign = TextAnchor.MiddleRight;
        hint.style.marginLeft = 32f;
        hint.style.flexShrink = 1f;
        hint.style.maxWidth = 340f;
        hint.style.alignSelf = Align.Center;
        actionRow.Add(hint);

        var footer = new Label("SURVIVE LONGER  •  CHANGE LANES EARLY  •  NEVER STOP MOVING");
        footer.style.color = Violet;
        footer.style.fontSize = 13f;
        footer.style.unityFontStyleAndWeight = FontStyle.Bold;
        footer.style.letterSpacing = 1f;
        footer.style.marginTop = 24f;
        frame.Add(footer);
    }

    private VisualElement CreateSettingRow(string labelText, System.Func<bool> getter, System.Action<bool> setter)
    {
        var row = new VisualElement();
        row.style.width = new Length(100f, LengthUnit.Percent);
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.minHeight = 36f;
        row.style.marginTop = 6f;

        var label = new Label(labelText);
        label.style.color = Color.white;
        label.style.fontSize = 16f;
        label.style.flexGrow = 1f;
        label.style.flexShrink = 1f;
        label.style.marginRight = 24f;
        row.Add(label);

        var button = new Button();
        button.style.width = 110f;
        button.style.height = 32f;
        button.style.flexShrink = 0f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = 14f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.borderTopWidth = 0f;
        button.style.borderBottomWidth = 0f;
        button.style.borderLeftWidth = 0f;
        button.style.borderRightWidth = 0f;

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
