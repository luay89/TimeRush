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

    // Layout below is written portrait-first: the shared DefaultPanelSettings asset scales by
    // matching screen HEIGHT, so the panel's virtual width tracks the device's real aspect ratio.
    // The root is transparent: CosmosBackdrop renders the living universe (and the hovering ship)
    // behind it, so the page is laid out as title on top, an open window in the middle where the
    // ship floats, and the controls in a glass panel at the bottom.
    private const float FrameMaxWidth = 640f;

    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
    private static readonly Color Glass = new Color(0.03f, 0.04f, 0.1f, 0.62f);

    private Label titleLabel;
    private Label eyebrowLabel;
    private Button launchButton;
    private Label hangarName;
    private Label hangarStatus;
    private VisualElement hangarSwatch;
    private Button hangarBuyButton;
    private Label coinsLabel;
    private int hangarIndex;

    private void BuildUI(UIDocument document)
    {
        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.style.backgroundColor = Color.clear;
        root.style.paddingLeft = 18f;
        root.style.paddingRight = 18f;
        root.style.paddingTop = 26f;
        root.style.paddingBottom = 20f;
        root.style.alignItems = Align.Center;

        var frame = new VisualElement();
        frame.style.width = new Length(100f, LengthUnit.Percent);
        frame.style.maxWidth = FrameMaxWidth;
        frame.style.flexGrow = 1f;
        frame.style.flexDirection = FlexDirection.Column;
        root.Add(frame);

        frame.Add(BuildHeader());
        frame.Add(BuildTitle());

        // The open window the hovering ship shows through.
        var window = new VisualElement();
        window.style.flexGrow = 1f;
        window.style.minHeight = 120f;
        frame.Add(window);

        frame.Add(BuildControlDeck());
        StartAnimations();
    }

    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.flexShrink = 0f;

        int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        var progression = ProgressionProfile.Load();
        var rank = progressionConfig.ResolveRank(bestScore);

        var rankChip = new Label($"RANK  {rank.name}");
        rankChip.style.color = Cyan;
        rankChip.style.fontSize = 12f;
        rankChip.style.unityFontStyleAndWeight = FontStyle.Bold;
        rankChip.style.letterSpacing = 1.5f;
        rankChip.style.backgroundColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.12f);
        StyleBorder(rankChip, 1f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
        StyleRadius(rankChip, 12f);
        StylePadding(rankChip, 12f, 6f);
        header.Add(rankChip);

        var stats = new VisualElement();
        stats.style.alignItems = Align.FlexEnd;
        header.Add(stats);

        var best = new Label($"BEST  {bestScore}   •   RUNS  {progression.TotalRuns}");
        best.style.color = Color.white;
        best.style.fontSize = 12f;
        best.style.unityFontStyleAndWeight = FontStyle.Bold;
        stats.Add(best);

        var rankProgress = RankProgression.Evaluate(progressionConfig, bestScore);
        var next = new Label(rankProgress.IsMaxRank ? "MAX RANK" : $"NEXT  {rankProgress.NextRankName}  IN  {rankProgress.PointsToNext}");
        next.style.color = Muted;
        next.style.fontSize = 10f;
        next.style.marginTop = 2f;
        stats.Add(next);

        coinsLabel = new Label();
        coinsLabel.style.color = Gold;
        coinsLabel.style.fontSize = 11f;
        coinsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        coinsLabel.style.marginTop = 2f;
        stats.Add(coinsLabel);
        return header;
    }

    private VisualElement BuildTitle()
    {
        var block = new VisualElement();
        block.style.alignItems = Align.Center;
        block.style.marginTop = 28f;
        block.style.flexShrink = 0f;

        eyebrowLabel = new Label("•  A RACE AGAINST TIME ACROSS THE GALAXY  •");
        eyebrowLabel.style.color = Cyan;
        eyebrowLabel.style.fontSize = 12f;
        eyebrowLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        eyebrowLabel.style.letterSpacing = 2f;
        eyebrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        eyebrowLabel.style.whiteSpace = WhiteSpace.Normal;
        block.Add(eyebrowLabel);

        titleLabel = new Label("TIME RUSH");
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 68f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.letterSpacing = 4f;
        titleLabel.style.marginTop = 4f;
        titleLabel.style.unityTextOutlineWidth = 0.6f;
        titleLabel.style.unityTextOutlineColor = new Color(Violet.r, Violet.g, Violet.b, 0.9f);
        titleLabel.style.textShadow = new TextShadow { offset = new Vector2(0f, 3f), blurRadius = 14f, color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.85f) };
        block.Add(titleLabel);

        var edition = new Label("N E B U L A   E D I T I O N");
        edition.style.color = Gold;
        edition.style.fontSize = 14f;
        edition.style.unityFontStyleAndWeight = FontStyle.Bold;
        edition.style.unityTextAlign = TextAnchor.MiddleCenter;
        edition.style.marginTop = 2f;
        edition.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 8f, color = new Color(Orange.r, Orange.g, Orange.b, 0.8f) };
        block.Add(edition);

        var tagline = new Label("Dodge the warships, blast them with your laser, grab shields. Red ring under a ship = it will hit you.");
        tagline.style.color = Muted;
        tagline.style.fontSize = 13f;
        tagline.style.unityTextAlign = TextAnchor.MiddleCenter;
        tagline.style.whiteSpace = WhiteSpace.Normal;
        tagline.style.marginTop = 12f;
        tagline.style.maxWidth = 460f;
        block.Add(tagline);
        return block;
    }

    private VisualElement BuildControlDeck()
    {
        var deck = new VisualElement();
        deck.style.flexShrink = 0f;
        deck.style.backgroundColor = Glass;
        StyleBorder(deck, 1.5f, new Color(Violet.r, Violet.g, Violet.b, 0.7f));
        StyleRadius(deck, 22f);
        deck.style.paddingLeft = 18f;
        deck.style.paddingRight = 18f;
        deck.style.paddingTop = 14f;
        deck.style.paddingBottom = 14f;

        deck.Add(BuildHangar());

        launchButton = new Button(StartRun) { text = "LAUNCH" };
        launchButton.style.height = 70f;
        launchButton.style.marginTop = 14f;
        launchButton.style.backgroundColor = Orange;
        launchButton.style.color = Color.white;
        launchButton.style.fontSize = 26f;
        launchButton.style.letterSpacing = 6f;
        launchButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        launchButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        StyleBorder(launchButton, 2f, Gold);
        StyleRadius(launchButton, 18f);
        launchButton.style.textShadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 6f, color = new Color(0.4f, 0.05f, 0f, 0.8f) };
        deck.Add(launchButton);

        var toggles = new VisualElement();
        toggles.style.flexDirection = FlexDirection.Row;
        toggles.style.justifyContent = Justify.SpaceBetween;
        toggles.style.marginTop = 12f;
        deck.Add(toggles);

        toggles.Add(CreateToggleChip("SOUND", () => audioEnabled, enabled =>
        {
            audioEnabled = enabled;
            FeedbackPreferences.SetAudioEnabled(enabled);
        }));
        toggles.Add(CreateToggleChip("SHAKE", () => cameraShakeEnabled, enabled =>
        {
            cameraShakeEnabled = enabled;
            FeedbackPreferences.SetCameraShakeEnabled(enabled);
        }));
        toggles.Add(CreateToggleChip("CALM FX", () => reduceFlashingEnabled, enabled =>
        {
            reduceFlashingEnabled = enabled;
            FeedbackPreferences.SetReduceFlashingEnabled(enabled);
        }));

        var hint = new Label("SWIPE  left / right  lane   •   up / down  depth   •   TAP  fire\nKEYS  A D  /  W S   •   SPACE  fire");
        hint.style.color = Muted;
        hint.style.fontSize = 11f;
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.marginTop = 10f;
        deck.Add(hint);
        return deck;
    }

    // ---------------------------------------------------------------- hangar (ship colours)

    private VisualElement BuildHangar()
    {
        hangarIndex = ShipSkinCatalog.IndexOf(ShipSkinManager.SelectedSkinId);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        row.Add(CreateArrowButton("<", -1));

        var center = new VisualElement();
        center.style.flexGrow = 1f;
        center.style.alignItems = Align.Center;
        row.Add(center);

        var caption = new Label("HANGAR");
        caption.style.color = Muted;
        caption.style.fontSize = 10f;
        caption.style.letterSpacing = 3f;
        center.Add(caption);

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        nameRow.style.marginTop = 2f;
        center.Add(nameRow);

        hangarSwatch = new VisualElement();
        hangarSwatch.style.width = 16f;
        hangarSwatch.style.height = 16f;
        hangarSwatch.style.marginRight = 8f;
        StyleRadius(hangarSwatch, 8f);
        StyleBorder(hangarSwatch, 1.5f, Color.white);
        nameRow.Add(hangarSwatch);

        hangarName = new Label();
        hangarName.style.color = Color.white;
        hangarName.style.fontSize = 18f;
        hangarName.style.unityFontStyleAndWeight = FontStyle.Bold;
        hangarName.style.letterSpacing = 1.5f;
        nameRow.Add(hangarName);

        hangarStatus = new Label();
        hangarStatus.style.fontSize = 11f;
        hangarStatus.style.marginTop = 2f;
        center.Add(hangarStatus);

        hangarBuyButton = new Button(BuyPreviewedSkin) { text = "BUY" };
        hangarBuyButton.style.height = 30f;
        hangarBuyButton.style.marginTop = 6f;
        hangarBuyButton.style.paddingLeft = 18f;
        hangarBuyButton.style.paddingRight = 18f;
        hangarBuyButton.style.backgroundColor = Gold;
        hangarBuyButton.style.color = Ink;
        hangarBuyButton.style.fontSize = 13f;
        hangarBuyButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        StyleBorder(hangarBuyButton, 0f, Color.clear);
        StyleRadius(hangarBuyButton, 10f);
        center.Add(hangarBuyButton);

        row.Add(CreateArrowButton(">", 1));
        RefreshHangar();
        return row;
    }

    private Button CreateArrowButton(string glyph, int direction)
    {
        var button = new Button(() => BrowseHangar(direction)) { text = glyph };
        button.style.width = 52f;
        button.style.height = 52f;
        button.style.fontSize = 24f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.color = Cyan;
        button.style.backgroundColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.1f);
        StyleBorder(button, 1.5f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f));
        StyleRadius(button, 26f);
        return button;
    }

    private void BrowseHangar(int direction)
    {
        int count = ShipSkinCatalog.Skins.Length;
        hangarIndex = ((hangarIndex + direction) % count + count) % count;
        string id = ShipSkinCatalog.Skins[hangarIndex].Id;
        if (ShipSkinManager.IsUnlocked(id) && ShipSkinManager.SelectedSkinId != id)
        {
            ShipSkinManager.Select(id);
            if (CosmosBackdrop.Active)
            {
                CosmosBackdrop.Active.RefreshHeroShip();
            }
        }

        RefreshHangar();
    }

    private void BuyPreviewedSkin()
    {
        string id = ShipSkinCatalog.Skins[hangarIndex].Id;
        if (ShipSkinManager.TryPurchase(id))
        {
            ShipSkinManager.Select(id);
            if (CosmosBackdrop.Active)
            {
                CosmosBackdrop.Active.RefreshHeroShip();
            }
        }

        RefreshHangar();
    }

    private void RefreshHangar()
    {
        ShipSkinCatalog.Skin skin = ShipSkinCatalog.Skins[hangarIndex];
        bool unlocked = ShipSkinManager.IsUnlocked(skin.Id);
        int balance = PlayerWallet.Balance;
        hangarName.text = skin.DisplayName;
        hangarSwatch.style.backgroundColor = skin.HullColor;
        coinsLabel.text = $"COINS  {balance}";

        if (unlocked)
        {
            hangarStatus.text = "EQUIPPED";
            hangarStatus.style.color = Cyan;
            hangarBuyButton.style.display = DisplayStyle.None;
            return;
        }

        bool affordable = balance >= skin.Price;
        hangarStatus.text = affordable ? $"LOCKED  •  {skin.Price} COINS" : $"LOCKED  •  {skin.Price} COINS  (you have {balance})";
        hangarStatus.style.color = affordable ? Gold : Muted;
        hangarBuyButton.style.display = affordable ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ---------------------------------------------------------------- toggles + animation

    private VisualElement CreateToggleChip(string labelText, System.Func<bool> getter, System.Action<bool> setter)
    {
        var button = new Button();
        button.style.flexGrow = 1f;
        button.style.flexBasis = 0f;
        button.style.height = 38f;
        button.style.marginLeft = 3f;
        button.style.marginRight = 3f;
        button.style.fontSize = 12f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        StyleRadius(button, 12f);

        System.Action refresh = () =>
        {
            bool value = getter();
            button.text = labelText + (value ? "  ON" : "  OFF");
            button.style.backgroundColor = value ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f) : new Color(1f, 1f, 1f, 0.05f);
            button.style.color = value ? Cyan : Muted;
            StyleBorder(button, 1f, value ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f) : new Color(Muted.r, Muted.g, Muted.b, 0.35f));
        };

        button.clicked += () =>
        {
            setter(!getter());
            refresh();
        };

        refresh();
        return button;
    }

    // Title colour breathing between cyan and violet glow, eyebrow shimmer, and a heartbeat pulse
    // on the launch button. UI Toolkit scheduler, so no Update loop and nothing touches gameplay.
    private void StartAnimations()
    {
        float startTime = Time.unscaledTime;
        titleLabel.schedule.Execute(() =>
        {
            float t = Time.unscaledTime - startTime;
            float wave = Mathf.Sin(t * 1.6f) * 0.5f + 0.5f;
            Color glow = Color.Lerp(Cyan, new Color(0.95f, 0.35f, 1f, 1f), wave);
            titleLabel.style.textShadow = new TextShadow { offset = new Vector2(0f, 3f), blurRadius = 10f + wave * 10f, color = new Color(glow.r, glow.g, glow.b, 0.9f) };
            titleLabel.style.unityTextOutlineColor = Color.Lerp(Violet, Cyan, wave);
            eyebrowLabel.style.opacity = 0.65f + 0.35f * Mathf.Sin(t * 2.3f + 1f);

            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 3.1f)), 8f);
            launchButton.style.scale = new Scale(Vector3.one * (1f + beat * 0.035f));
            launchButton.style.backgroundColor = Color.Lerp(Orange, new Color(1f, 0.55f, 0.15f, 1f), beat);
        }).Every(16);
    }

    private static void StyleBorder(VisualElement element, float width, Color color)
    {
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
    }

    private static void StyleRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }

    private static void StylePadding(VisualElement element, float horizontal, float vertical)
    {
        element.style.paddingLeft = horizontal;
        element.style.paddingRight = horizontal;
        element.style.paddingTop = vertical;
        element.style.paddingBottom = vertical;
    }

    private void StartRun()
    {
        if (startRequestInProgress)
        {
            return;
        }

        startRequestInProgress = true;
        if (launchButton != null)
        {
            launchButton.text = "ENGAGE";
            launchButton.SetEnabled(false);
        }

        // The ship blasts off into warp first; the run loads once it has left the frame.
        if (CosmosBackdrop.Active)
        {
            CosmosBackdrop.Active.PlayLaunch();
            StartCoroutine(LoadRunAfter(0.75f));
            return;
        }

        LoadRun();
    }

    private System.Collections.IEnumerator LoadRunAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        LoadRun();
    }

    private void LoadRun()
    {
        if (GameStateMachine.HasInstance)
        {
            if (!GameStateMachine.Instance.StartRunFromMenu())
            {
                startRequestInProgress = false;
                if (launchButton != null)
                {
                    launchButton.text = "LAUNCH";
                    launchButton.SetEnabled(true);
                }
            }

            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }
}
