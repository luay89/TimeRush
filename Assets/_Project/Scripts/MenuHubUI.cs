using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Builds the lightweight TimeRush menu at runtime while preserving the existing
/// MenuHub -> Game scene contract. Besides the hangar and LAUNCH (endless run) it hosts the
/// Stages, Settings, How to Play and Credits panels, and asks before quitting on Back/Escape.
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
    private static readonly Color Mint = new Color(0.3f, 1f, 0.72f, 1f);
    private bool startRequestInProgress;

    private ProgressionConfig progressionConfig;

    private void Awake()
    {
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
    private static readonly Color PanelGlass = new Color(0.03f, 0.045f, 0.11f, 0.97f);

    private VisualElement root;
    private Label titleLabel;
    private Label eyebrowLabel;
    private Button launchButton;
    private Button stagesButton;
    private Label hangarName;
    private Label hangarStatus;
    private VisualElement hangarSwatch;
    private Button hangarBuyButton;
    private Label coinsLabel;
    private int hangarIndex;
    private VisualElement openPanel;

    private void BuildUI(UIDocument document)
    {
        root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.style.backgroundColor = Color.clear;
        root.style.alignItems = Align.Center;

        // Padding lives on the frame (not the root) so full-screen panels can cover edge to edge.
        var frame = new VisualElement();
        frame.style.width = new Length(100f, LengthUnit.Percent);
        frame.style.maxWidth = FrameMaxWidth + 36f;
        frame.style.flexGrow = 1f;
        frame.style.flexDirection = FlexDirection.Column;
        frame.style.paddingLeft = 18f;
        frame.style.paddingRight = 18f;
        frame.style.paddingTop = 26f;
        frame.style.paddingBottom = 20f;
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

    private void Update()
    {
        // Android's Back button arrives as Escape: close the open panel, else ask before quitting.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (openPanel != null)
            {
                ClosePanels();
            }
            else
            {
                ShowQuitConfirm();
            }
        }
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

        launchButton = new Button(() => StartRun(-1)) { text = "LAUNCH" };
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

        var modeHint = new Label("ENDLESS RUN  •  HOW FAR CAN YOU GO?");
        modeHint.style.color = Muted;
        modeHint.style.fontSize = 10f;
        modeHint.style.letterSpacing = 1.5f;
        modeHint.style.unityTextAlign = TextAnchor.MiddleCenter;
        modeHint.style.marginTop = 5f;
        deck.Add(modeHint);

        var nav = new VisualElement();
        nav.style.flexDirection = FlexDirection.Row;
        nav.style.marginTop = 12f;
        deck.Add(nav);

        StageProgressModel stages = StageProgress.Load();
        stagesButton = CreateNavButton($"STAGES  {stages.TotalStars}/{stages.MaxTotalStars}", Mint, ShowStages);
        nav.Add(stagesButton);
        nav.Add(CreateNavButton("SETTINGS", Cyan, ShowSettings));
        nav.Add(CreateNavButton("HOW TO PLAY", Cyan, ShowHelp));
        return deck;
    }

    private Button CreateNavButton(string text, Color accent, System.Action onClick)
    {
        var button = new Button(() =>
        {
            GameAudio.Click();
            onClick();
        })
        { text = text };
        button.style.flexGrow = 1f;
        button.style.flexBasis = 0f;
        button.style.height = 44f;
        button.style.marginLeft = 3f;
        button.style.marginRight = 3f;
        button.style.fontSize = 12f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.letterSpacing = 1f;
        button.style.color = accent;
        button.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.12f);
        StyleBorder(button, 1f, new Color(accent.r, accent.g, accent.b, 0.6f));
        StyleRadius(button, 12f);
        return button;
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
        GameAudio.Click();
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
            GameAudio.Confirm();
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

    // ---------------------------------------------------------------- panels

    /// <summary>Opens a menu panel by name (stages, settings, help, credits, quit). Used by the screenshot validator.</summary>
    public void OpenPanelForCapture(string panel)
    {
        switch (panel)
        {
            case "stages": ShowStages(); break;
            case "settings": ShowSettings(); break;
            case "help": ShowHelp(); break;
            case "credits": ShowCredits(); break;
            case "quit": ShowQuitConfirm(); break;
        }
    }

    public void ClosePanels()
    {
        if (openPanel != null)
        {
            openPanel.RemoveFromHierarchy();
            openPanel = null;
        }
    }

    // A dimmed full-screen layer with a glass card: title, close button and a scrolling body.
    private VisualElement OpenPanel(string title, Color accent)
    {
        ClosePanels();

        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.backgroundColor = new Color(0.005f, 0.01f, 0.04f, 0.78f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.paddingLeft = 16f;
        overlay.style.paddingRight = 16f;
        overlay.style.paddingTop = 24f;
        overlay.style.paddingBottom = 24f;
        // Tapping the dimmed area outside the card closes it.
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.target == overlay)
            {
                ClosePanels();
            }
        });
        root.Add(overlay);
        openPanel = overlay;

        var card = new VisualElement();
        card.style.width = new Length(100f, LengthUnit.Percent);
        card.style.maxWidth = 600f;
        card.style.maxHeight = new Length(100f, LengthUnit.Percent);
        card.style.backgroundColor = PanelGlass;
        StyleBorder(card, 1.5f, new Color(accent.r, accent.g, accent.b, 0.75f));
        StyleRadius(card, 22f);
        card.style.paddingLeft = 20f;
        card.style.paddingRight = 20f;
        card.style.paddingTop = 16f;
        card.style.paddingBottom = 18f;
        overlay.Add(card);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.flexShrink = 0f;
        header.style.marginBottom = 10f;
        card.Add(header);

        var heading = new Label(title);
        heading.style.color = accent;
        heading.style.fontSize = 24f;
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        heading.style.letterSpacing = 3f;
        header.Add(heading);

        var close = new Button(() =>
        {
            GameAudio.Click();
            ClosePanels();
        })
        { text = "X" };
        close.style.width = 44f;
        close.style.height = 44f;
        close.style.fontSize = 18f;
        close.style.unityFontStyleAndWeight = FontStyle.Bold;
        close.style.color = Color.white;
        close.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
        StyleBorder(close, 1f, new Color(1f, 1f, 1f, 0.3f));
        StyleRadius(close, 22f);
        header.Add(close);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexShrink = 1f;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        card.Add(scroll);
        return scroll;
    }

    private void ShowStages()
    {
        VisualElement body = OpenPanel("STAGES", Mint);
        StageProgressModel progress = StageProgress.Load();

        var intro = Paragraph($"Survive each stage to clear it and unlock the next. Score more for extra stars.   {progress.TotalStars} / {progress.MaxTotalStars} stars", Muted, 12f);
        intro.style.marginBottom = 10f;
        body.Add(intro);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.Center;
        body.Add(grid);

        for (int i = 0; i < StageCatalog.Count; i++)
        {
            grid.Add(CreateStageTile(i, progress));
        }
    }

    private VisualElement CreateStageTile(int index, StageProgressModel progress)
    {
        StageCatalog.StageDefinition stage = StageCatalog.Stages[index];
        bool unlocked = progress.IsUnlocked(index);
        int stars = progress.GetStars(index);

        var tile = new Button(() =>
        {
            if (unlocked)
            {
                GameAudio.Confirm();
                ClosePanels();
                StartRun(index);
            }
        });
        tile.style.width = 104f;
        tile.style.height = 124f;
        tile.style.marginLeft = 4f;
        tile.style.marginRight = 4f;
        tile.style.marginTop = 4f;
        tile.style.marginBottom = 4f;
        tile.style.paddingLeft = 4f;
        tile.style.paddingRight = 4f;
        tile.style.flexDirection = FlexDirection.Column;
        tile.style.alignItems = Align.Center;
        tile.style.justifyContent = Justify.Center;
        Color edge = !unlocked ? new Color(1f, 1f, 1f, 0.18f) : stars > 0 ? Mint : Gold;
        tile.style.backgroundColor = unlocked ? new Color(edge.r, edge.g, edge.b, 0.1f) : new Color(1f, 1f, 1f, 0.03f);
        StyleBorder(tile, 1.5f, new Color(edge.r, edge.g, edge.b, unlocked ? 0.75f : 1f));
        StyleRadius(tile, 16f);
        tile.SetEnabled(unlocked);

        var number = new Label(unlocked ? stage.Number.ToString() : "LOCKED");
        number.style.color = unlocked ? Color.white : Muted;
        number.style.fontSize = unlocked ? 30f : 13f;
        number.style.unityFontStyleAndWeight = FontStyle.Bold;
        number.pickingMode = PickingMode.Ignore;
        tile.Add(number);

        var name = new Label(stage.Name);
        name.style.color = unlocked ? Muted : new Color(Muted.r, Muted.g, Muted.b, 0.6f);
        name.style.fontSize = 9f;
        name.style.unityTextAlign = TextAnchor.MiddleCenter;
        name.style.whiteSpace = WhiteSpace.Normal;
        name.style.marginTop = 2f;
        name.pickingMode = PickingMode.Ignore;
        tile.Add(name);

        var starRow = new VisualElement();
        starRow.style.flexDirection = FlexDirection.Row;
        starRow.style.marginTop = 6f;
        starRow.pickingMode = PickingMode.Ignore;
        for (int s = 0; s < StageCatalog.MaxStars; s++)
        {
            starRow.Add(new StarIcon(s < stars, 16f));
        }

        tile.Add(starRow);
        starRow.style.flexShrink = 0f;
        starRow.style.height = 18f;

        var goal = new Label($"{stage.GoalSeconds:0}s");
        goal.style.color = new Color(Muted.r, Muted.g, Muted.b, 0.8f);
        goal.style.fontSize = 9f;
        goal.style.marginTop = 3f;
        goal.pickingMode = PickingMode.Ignore;
        tile.Add(goal);

        // A fixed-height tile must not squash its rows into each other.
        foreach (VisualElement child in tile.Children())
        {
            child.style.flexShrink = 0f;
        }

        return tile;
    }

    private void ShowSettings()
    {
        VisualElement body = OpenPanel("SETTINGS", Cyan);

        body.Add(SectionLabel("AUDIO"));
        body.Add(CreateToggleRow("Sound", () => FeedbackPreferences.IsAudioEnabled(feedbackConfig), FeedbackPreferences.SetAudioEnabled));
        body.Add(CreateLevelRow("Music", () => FeedbackPreferences.MusicVolume, v => FeedbackPreferences.MusicVolume = v));
        body.Add(CreateLevelRow("Effects", () => FeedbackPreferences.SfxVolume, v => FeedbackPreferences.SfxVolume = v));

        body.Add(SectionLabel("COMFORT"));
        body.Add(CreateToggleRow("Camera shake", () => FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig), FeedbackPreferences.SetCameraShakeEnabled));
        body.Add(CreateToggleRow("Calm effects (less flashing)", () => FeedbackPreferences.IsReduceFlashingEnabled(feedbackConfig), FeedbackPreferences.SetReduceFlashingEnabled));
        body.Add(CreateToggleRow("Vibration", () => FeedbackPreferences.IsVibrationEnabled, FeedbackPreferences.SetVibrationEnabled));

        var links = new VisualElement();
        links.style.flexDirection = FlexDirection.Row;
        links.style.marginTop = 16f;
        body.Add(links);
        links.Add(CreateNavButton("SHOW TUTORIAL AGAIN", Gold, () =>
        {
            FeedbackPreferences.SetTutorialSeen(false);
            ShowHelp();
        }));
        links.Add(CreateNavButton("CREDITS", Violet, ShowCredits));
    }

    private void ShowHelp()
    {
        VisualElement body = OpenPanel("HOW TO PLAY", Cyan);
        body.Add(HelpRow("MOVE", "Swipe left / right to change lane.\nKeys: A / D or arrow keys."));
        body.Add(HelpRow("DEPTH", "Swipe up / down to fly further back or closer.\nKeys: W / S."));
        body.Add(HelpRow("FIRE", "Tap the screen to shoot your laser.\nKey: Space."));
        body.Add(HelpRow("DANGER", "A red ring under a warship means it is on your line and will hit you. Move or shoot it."));
        body.Add(HelpRow("SHIELD", "Fly through a blue shield to become untouchable for a few seconds."));
        body.Add(HelpRow("FLOW", "Near misses build a score multiplier. Keep the chain going."));
        body.Add(HelpRow("PAUSE", "Tap the pause button, or press Esc / P."));
    }

    private void ShowCredits()
    {
        VisualElement body = OpenPanel("CREDITS", Violet);
        body.Add(HelpRow("GAME", "Time Rush: Nebula Edition\nDesign & development: luay89"));
        body.Add(HelpRow("MUSIC", "Composed and synthesised in code at runtime (no audio files)."));
        body.Add(HelpRow("SOUND FX", "Kenney (kenney.nl), CC0 1.0. Interface sounds synthesised in code."));
        body.Add(HelpRow("ENGINE", "Made with Unity. Text: TextMesh Pro."));
        var version = Paragraph($"Version {Application.version}", Muted, 11f);
        version.style.marginTop = 10f;
        version.style.unityTextAlign = TextAnchor.MiddleCenter;
        body.Add(version);
    }

    private void ShowQuitConfirm()
    {
        VisualElement body = OpenPanel("LEAVE?", Orange);
        body.Add(Paragraph("Close Time Rush? Your progress is saved.", Color.white, 15f));

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginTop = 16f;
        body.Add(row);
        row.Add(CreateNavButton("STAY", Cyan, ClosePanels));
        row.Add(CreateNavButton("QUIT GAME", Orange, () =>
        {
            ClosePanels();
            Application.Quit();
        }));
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label(text);
        label.style.color = Gold;
        label.style.fontSize = 11f;
        label.style.letterSpacing = 3f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 10f;
        label.style.marginBottom = 4f;
        return label;
    }

    private static Label Paragraph(string text, Color color, float size)
    {
        var label = new Label(text);
        label.style.color = color;
        label.style.fontSize = size;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static VisualElement HelpRow(string title, string text)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.FlexStart;
        row.style.marginTop = 6f;
        row.style.marginBottom = 6f;

        var key = new Label(title);
        key.style.width = 92f;
        key.style.flexShrink = 0f;
        key.style.color = Cyan;
        key.style.fontSize = 13f;
        key.style.unityFontStyleAndWeight = FontStyle.Bold;
        key.style.letterSpacing = 1.5f;
        row.Add(key);

        var body = Paragraph(text, Color.white, 13f);
        body.style.flexShrink = 1f;
        body.style.flexGrow = 1f;
        row.Add(body);
        return row;
    }

    private VisualElement CreateToggleRow(string labelText, System.Func<bool> getter, System.Action<bool> setter)
    {
        var row = SettingRow(labelText);
        var button = new Button();
        button.style.width = 84f;
        button.style.height = 38f;
        button.style.fontSize = 13f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        StyleRadius(button, 12f);

        System.Action refresh = () =>
        {
            bool value = getter();
            button.text = value ? "ON" : "OFF";
            button.style.backgroundColor = value ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f) : new Color(1f, 1f, 1f, 0.05f);
            button.style.color = value ? Cyan : Muted;
            StyleBorder(button, 1f, value ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f) : new Color(Muted.r, Muted.g, Muted.b, 0.35f));
        };

        button.clicked += () =>
        {
            setter(!getter());
            refresh();
            GameAudio.Click();
        };

        refresh();
        row.Add(button);
        return row;
    }

    // A 10-step level control: [-] segmented bar [+]. Big touch targets, no tiny slider thumb.
    private VisualElement CreateLevelRow(string labelText, System.Func<float> getter, System.Action<float> setter)
    {
        const int Steps = 10;
        var row = SettingRow(labelText);

        var control = new VisualElement();
        control.style.flexDirection = FlexDirection.Row;
        control.style.alignItems = Align.Center;
        row.Add(control);

        var segments = new VisualElement[Steps];
        System.Action refresh = null;
        System.Action<int> apply = level =>
        {
            setter(Mathf.Clamp(level, 0, Steps) / (float)Steps);
            refresh();
            GameAudio.Click();
        };

        control.Add(SmallButton("-", () => apply(Mathf.RoundToInt(getter() * Steps) - 1)));

        var bar = new VisualElement();
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.marginLeft = 6f;
        bar.style.marginRight = 6f;
        control.Add(bar);
        for (int i = 0; i < Steps; i++)
        {
            int level = i + 1;
            var segment = new VisualElement();
            segment.style.width = 13f;
            segment.style.height = 26f;
            segment.style.marginLeft = 1.5f;
            segment.style.marginRight = 1.5f;
            StyleRadius(segment, 3f);
            segment.RegisterCallback<PointerDownEvent>(_ => apply(level));
            segments[i] = segment;
            bar.Add(segment);
        }

        control.Add(SmallButton("+", () => apply(Mathf.RoundToInt(getter() * Steps) + 1)));

        refresh = () =>
        {
            int level = Mathf.RoundToInt(getter() * Steps);
            for (int i = 0; i < Steps; i++)
            {
                segments[i].style.backgroundColor = i < level ? Cyan : new Color(1f, 1f, 1f, 0.1f);
            }
        };

        refresh();
        return row;
    }

    private static Button SmallButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.style.width = 38f;
        button.style.height = 38f;
        button.style.fontSize = 18f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.color = Cyan;
        button.style.backgroundColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.1f);
        StyleBorder(button, 1f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f));
        StyleRadius(button, 19f);
        return button;
    }

    private static VisualElement SettingRow(string labelText)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.minHeight = 48f;
        row.style.borderBottomWidth = 1f;
        row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.06f);

        var label = new Label(labelText);
        label.style.color = Color.white;
        label.style.fontSize = 14f;
        label.style.flexShrink = 1f;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginRight = 8f;
        row.Add(label);
        return row;
    }

    // ---------------------------------------------------------------- animation

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
            float pulse = 1f + beat * 0.035f;
            launchButton.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
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

    // ---------------------------------------------------------------- launching

    /// <param name="stageIndex">-1 for the endless run, otherwise the stage to play.</param>
    private void StartRun(int stageIndex)
    {
        if (startRequestInProgress)
        {
            return;
        }

        if (stageIndex >= 0)
        {
            StageSession.SelectStage(stageIndex);
        }
        else
        {
            StageSession.SelectEndless();
        }

        GameAudio.Confirm();
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

    /// <summary>A five-pointed star drawn with the vector API (no font glyph needed).</summary>
    private sealed class StarIcon : VisualElement
    {
        private readonly bool filled;

        public StarIcon(bool filled, float size)
        {
            this.filled = filled;
            style.width = size;
            style.height = size;
            style.marginLeft = 1f;
            style.marginRight = 1f;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            var painter = context.painter2D;
            Vector2 center = rect.center;
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = outer * 0.45f;

            painter.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.Deg2Rad * (-90f + i * 36f);
                float radius = i % 2 == 0 ? outer : inner;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0)
                {
                    painter.MoveTo(point);
                }
                else
                {
                    painter.LineTo(point);
                }
            }

            painter.ClosePath();
            if (filled)
            {
                painter.fillColor = Gold;
                painter.Fill();
            }
            else
            {
                painter.strokeColor = new Color(1f, 1f, 1f, 0.35f);
                painter.lineWidth = 1.2f;
                painter.Stroke();
            }
        }
    }
}
