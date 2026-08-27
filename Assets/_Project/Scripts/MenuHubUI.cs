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

    private static readonly Color Ink = new Color(0.015f, 0.025f, 0.075f, 1f);
    private static readonly Color Panel = new Color(0.035f, 0.055f, 0.12f, 0.96f);
    private static readonly Color Cyan = new Color(0.12f, 0.95f, 1f, 1f);
    private static readonly Color Orange = new Color(1f, 0.36f, 0.12f, 1f);
    private static readonly Color Violet = new Color(0.56f, 0.34f, 1f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.7f, 0.86f, 1f);

    private void Awake()
    {
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
        frame.style.justifyContent = Justify.SpaceBetween;
        root.Add(frame);

        var header = new VisualElement();
        header.style.width = new Length(100f, LengthUnit.Percent);
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        frame.Add(header);

        var mark = new Label("TR");
        mark.style.color = Cyan;
        mark.style.fontSize = 26f;
        mark.style.unityFontStyleAndWeight = FontStyle.Bold;
        mark.style.letterSpacing = 4f;
        header.Add(mark);

        var meta = new Label("ENDLESS DODGE // BUILD 01");
        meta.style.color = Muted;
        meta.style.fontSize = 14f;
        meta.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(meta);

        var titleBlock = new VisualElement();
        titleBlock.style.marginTop = 56f;
        titleBlock.style.marginBottom = 48f;
        frame.Add(titleBlock);

        var eyebrow = new Label("MOVE WITH THE CLOCK");
        eyebrow.style.color = Orange;
        eyebrow.style.fontSize = 16f;
        eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
        eyebrow.style.letterSpacing = 2f;
        titleBlock.Add(eyebrow);

        var title = new Label("TIME\nRUSH");
        title.style.color = Color.white;
        title.style.fontSize = 108f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleLeft;
        title.style.letterSpacing = -2f;
        title.style.marginTop = 8f;
        titleBlock.Add(title);

        var subtitle = new Label("Three lanes. One clean line through the chaos.");
        subtitle.style.color = Muted;
        subtitle.style.fontSize = 23f;
        subtitle.style.marginTop = 18f;
        titleBlock.Add(subtitle);

        var actionRow = new VisualElement();
        actionRow.style.width = new Length(100f, LengthUnit.Percent);
        actionRow.style.flexDirection = FlexDirection.Row;
        actionRow.style.alignItems = Align.Center;
        actionRow.style.justifyContent = Justify.SpaceBetween;
        frame.Add(actionRow);

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
        startButton.RegisterCallback<PointerEnterEvent>(_ => startButton.style.backgroundColor = Cyan);
        startButton.RegisterCallback<PointerLeaveEvent>(_ => startButton.style.backgroundColor = Orange);
        actionRow.Add(startButton);

        var hint = new Label("A / D  or  ← / →\nSwipe left or right on touch");
        hint.style.color = Muted;
        hint.style.fontSize = 16f;
        hint.style.unityTextAlign = TextAnchor.MiddleRight;
        actionRow.Add(hint);

        var footer = new Label("SURVIVE LONGER  •  CHANGE LANES EARLY  •  NEVER STOP MOVING");
        footer.style.color = Violet;
        footer.style.fontSize = 13f;
        footer.style.unityFontStyleAndWeight = FontStyle.Bold;
        footer.style.letterSpacing = 1f;
        footer.style.marginTop = 56f;
        frame.Add(footer);
    }

    private void StartRun()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.StartRunFromMenu();
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }
}
