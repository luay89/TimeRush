using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatHudController : MonoBehaviour
{
    private static readonly Color Cyan = new Color(0.2f, 0.94f, 1f, 1f);
    private static readonly Color Amber = new Color(1f, 0.68f, 0.22f, 1f);
    private static readonly Color Red = new Color(1f, 0.2f, 0.22f, 1f);
    private static readonly Color Panel = new Color(0.015f, 0.035f, 0.085f, 0.88f);

    private RectTransform safeRoot;
    private RectTransform targetMarker;
    private TMP_Text missionTitle;
    private TMP_Text objectiveText;
    private TMP_Text waveText;
    private TMP_Text healthText;
    private TMP_Text shieldText;
    private TMP_Text weaponName;
    private TMP_Text weaponSlot;
    private TMP_Text ammoText;
    private TMP_Text targetName;
    private TMP_Text targetDistance;
    private TMP_Text targetStatus;
    private TMP_Text hitText;
    private TMP_Text bossTitle;
    private TMP_Text specialText;
    private Image healthFill;
    private Image shieldFill;
    private Image bossFill;
    private Image specialFill;
    private Image damageFlash;
    private Button specialButton;

    private CombatShipHealth playerHealth;
    private CombatShipHealth targetHealth;
    private CombatEnemy target;
    private CombatWeaponController weapons;
    private CombatSpecialAbilityController specialAbility;
    private CombatWaveDirector waves;
    private CombatPlayerController player;
    private RectTransform bossPanel;
    private Camera worldCamera;
    private float refreshTimer;
    private float hitTimer;
    private float damageTimer;
    private Rect lastSafeArea;

    private void Awake()
    {
        BuildCanvas();
    }

    private void Start()
    {
        player = FindObjectOfType<CombatPlayerController>();
        playerHealth = player ? player.GetComponent<CombatShipHealth>() : null;
        weapons = player ? player.GetComponent<CombatWeaponController>() : null;
        specialAbility = player ? player.GetComponent<CombatSpecialAbilityController>() : null;
        waves = FindObjectOfType<CombatWaveDirector>();
        worldCamera = ResolveWorldCamera();

        if (playerHealth)
        {
            playerHealth.Damaged += HandlePlayerDamaged;
        }

        ApplySafeArea();
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (playerHealth)
        {
            playerHealth.Damaged -= HandlePlayerDamaged;
        }

        if (targetHealth)
        {
            targetHealth.Damaged -= HandleTargetDamaged;
        }
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea)
        {
            ApplySafeArea();
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.12f;
            RefreshHud();
        }

        if (hitTimer > 0f)
        {
            hitTimer -= Time.unscaledDeltaTime;
            hitText.alpha = Mathf.Clamp01(hitTimer * 2f);
        }

        if (damageTimer > 0f)
        {
            damageTimer -= Time.unscaledDeltaTime;
            Color color = damageFlash.color;
            color.a = Mathf.Clamp01(damageTimer / 0.4f) * 0.08f;
            damageFlash.color = color;
        }

        UpdateTargetMarker();
    }

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("CombatHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var safeObject = new GameObject("SafeArea", typeof(RectTransform));
        safeObject.transform.SetParent(canvasObject.transform, false);
        safeRoot = safeObject.GetComponent<RectTransform>();
        safeRoot.anchorMin = Vector2.zero;
        safeRoot.anchorMax = Vector2.one;
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;

        BuildMissionPanel();
        BuildVitalsPanel();
        BuildWeaponPanel();
        BuildSpecialPanel();
        BuildBossPanel();
        BuildTargetMarker();
        BuildTargetStatus();

        var flashObject = new GameObject("DamageFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flashObject.transform.SetParent(canvasObject.transform, false);
        var flashRect = flashObject.GetComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
        damageFlash = flashObject.GetComponent<Image>();
        damageFlash.color = new Color(Red.r, Red.g, Red.b, 0f);
        damageFlash.raycastTarget = false;
        damageFlash.transform.SetAsFirstSibling();
    }

    private void BuildMissionPanel()
    {
        RectTransform panel = CreatePanel("MissionPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(292f, 118f), new Vector2(16f, -18f));
        missionTitle = CreateText(panel, "MissionTitle", 16f, Cyan, FontStyles.Bold, new Vector2(14f, -10f), new Vector2(260f, 26f), new Vector2(0f, 1f));
        objectiveText = CreateText(panel, "Objective", 14f, Color.white, FontStyles.Normal, new Vector2(14f, -40f), new Vector2(264f, 28f), new Vector2(0f, 1f));
        waveText = CreateText(panel, "WaveStatus", 14f, Amber, FontStyles.Bold, new Vector2(14f, -75f), new Vector2(264f, 26f), new Vector2(0f, 1f));
    }

    private void BuildVitalsPanel()
    {
        RectTransform panel = CreatePanel("VitalsPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(278f, 118f), new Vector2(-16f, -96f));
        CreateText(panel, "ShipTitle", 13f, Cyan, FontStyles.Bold, new Vector2(12f, -8f), new Vector2(248f, 20f), new Vector2(0f, 1f)).text = "PILOT // HULL INTEGRITY";
        healthText = CreateText(panel, "HullLabel", 12f, Color.white, FontStyles.Bold, new Vector2(12f, -31f), new Vector2(245f, 18f), new Vector2(0f, 1f));
        healthFill = CreateBar(panel, "HullBar", new Vector2(12f, -52f), new Color(0.98f, 0.28f, 0.28f, 1f));
        shieldText = CreateText(panel, "ShieldLabel", 12f, Color.white, FontStyles.Bold, new Vector2(12f, -70f), new Vector2(245f, 18f), new Vector2(0f, 1f));
        shieldFill = CreateBar(panel, "ShieldBar", new Vector2(12f, -91f), Cyan);
    }

    private void BuildWeaponPanel()
    {
        RectTransform panel = CreatePanel("WeaponPanel", Vector2.zero, Vector2.zero, new Vector2(326f, 106f), new Vector2(16f, 18f));
        weaponSlot = CreateText(panel, "WeaponSlot", 12f, Cyan, FontStyles.Bold, new Vector2(12f, 94f), new Vector2(292f, 18f), Vector2.zero);
        weaponName = CreateText(panel, "WeaponName", 20f, Color.white, FontStyles.Bold, new Vector2(12f, 64f), new Vector2(292f, 28f), Vector2.zero);
        ammoText = CreateText(panel, "Ammo", 13f, Amber, FontStyles.Bold, new Vector2(12f, 37f), new Vector2(292f, 20f), Vector2.zero);
        CreateText(panel, "ControlHint", 11f, new Color(0.66f, 0.77f, 0.92f, 1f), FontStyles.Normal, new Vector2(12f, 12f), new Vector2(300f, 18f), Vector2.zero).text = "DRAG TO PILOT  •  HOLD RIGHT TO FIRE  •  SWIPE TO CYCLE";
    }

    private void BuildBossPanel()
    {
        RectTransform panel = CreatePanel("BossVitals", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(360f, 68f), new Vector2(0f, -252f));
        bossTitle = CreateText(panel, "BossTitle", 13f, Red, FontStyles.Bold, new Vector2(0f, -8f), new Vector2(336f, 20f), new Vector2(0.5f, 1f));
        bossFill = CreateBar(panel, "BossHealth", new Vector2(12f, -36f), Red);
        bossPanel = panel;
        panel.gameObject.SetActive(false);
    }

    private void BuildSpecialPanel()
    {
        RectTransform panel = CreatePanel("SpecialAbility", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(190f, 106f), new Vector2(-16f, 18f));
        specialButton = panel.gameObject.AddComponent<Button>();
        specialButton.targetGraphic = panel.GetComponent<Image>();
        specialButton.targetGraphic.raycastTarget = true;
        specialButton.onClick.AddListener(() =>
        {
            if (specialAbility)
            {
                specialAbility.Activate();
            }
        });

        CreateText(panel, "SpecialTitle", 12f, Cyan, FontStyles.Bold, new Vector2(12f, 82f), new Vector2(166f, 18f), Vector2.zero).text = "SPECIAL SYSTEM";
        specialText = CreateText(panel, "SpecialStatus", 13f, Color.white, FontStyles.Bold, new Vector2(12f, 56f), new Vector2(166f, 20f), Vector2.zero);
        specialFill = CreateBar(panel, "SpecialCooldown", new Vector2(12f, 27f), Cyan);
        CreateText(panel, "SpecialHint", 10f, new Color(0.66f, 0.77f, 0.92f, 1f), FontStyles.Normal, new Vector2(12f, 7f), new Vector2(166f, 16f), Vector2.zero).text = "TAP  •  SHIFT / X";
    }

    private void BuildTargetMarker()
    {
        var markerObject = new GameObject("TargetMarker", typeof(RectTransform));
        markerObject.transform.SetParent(safeRoot, false);
        targetMarker = markerObject.GetComponent<RectTransform>();
        targetMarker.anchorMin = targetMarker.anchorMax = new Vector2(0.5f, 0.5f);
        targetMarker.pivot = new Vector2(0.5f, 0.5f);
        targetMarker.sizeDelta = new Vector2(64f, 64f);

        AddMarkerLine("TopLeftH", new Vector2(-16f, 26f), new Vector2(18f, 2f));
        AddMarkerLine("TopLeftV", new Vector2(-26f, 16f), new Vector2(2f, 18f));
        AddMarkerLine("TopRightH", new Vector2(16f, 26f), new Vector2(18f, 2f));
        AddMarkerLine("TopRightV", new Vector2(26f, 16f), new Vector2(2f, 18f));
        AddMarkerLine("BottomLeftH", new Vector2(-16f, -26f), new Vector2(18f, 2f));
        AddMarkerLine("BottomLeftV", new Vector2(-26f, -16f), new Vector2(2f, 18f));
        AddMarkerLine("BottomRightH", new Vector2(16f, -26f), new Vector2(18f, 2f));
        AddMarkerLine("BottomRightV", new Vector2(26f, -16f), new Vector2(2f, 18f));

        targetName = CreateText(targetMarker, "TargetName", 13f, Cyan, FontStyles.Bold, new Vector2(0f, 42f), new Vector2(240f, 20f), new Vector2(0.5f, 0f));
        targetDistance = CreateText(targetMarker, "TargetDistance", 11f, Color.white, FontStyles.Normal, new Vector2(0f, 24f), new Vector2(160f, 18f), new Vector2(0.5f, 0f));
        hitText = CreateText(targetMarker, "HitConfirmed", 12f, Amber, FontStyles.Bold, new Vector2(0f, -44f), new Vector2(220f, 20f), new Vector2(0.5f, 1f));
        hitText.alpha = 0f;
        targetMarker.gameObject.SetActive(false);
    }

    private void BuildTargetStatus()
    {
        RectTransform panel = CreatePanel("TargetStatus", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300f, 34f), new Vector2(0f, -138f));
        targetStatus = CreateText(panel, "TargetStatusText", 13f, Cyan, FontStyles.Bold, Vector2.zero, new Vector2(280f, 26f), new Vector2(0.5f, 0.5f));
        targetStatus.alignment = TextAlignmentOptions.Center;
    }

    private void AddMarkerLine(string name, Vector2 position, Vector2 size)
    {
        var line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.transform.SetParent(targetMarker, false);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        line.GetComponent<Image>().color = Cyan;
        line.GetComponent<Image>().raycastTarget = false;
    }

    private RectTransform CreatePanel(string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(safeRoot, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = panel.GetComponent<Image>();
        image.color = Panel;
        image.raycastTarget = false;
        return rect;
    }

    private TMP_Text CreateText(Transform parent, string name, float size, Color color, FontStyles style, Vector2 position, Vector2 dimensions, Vector2 anchor)
    {
        var labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.outlineWidth = 0.12f;
        label.outlineColor = new Color(0f, 0.015f, 0.04f, 0.9f);
        return label;
    }

    private Image CreateBar(Transform parent, string name, Vector2 position, Color color)
    {
        var trackObject = new GameObject(name + "Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackObject.transform.SetParent(parent, false);
        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.anchoredPosition = position;
        trackRect.sizeDelta = new Vector2(-24f, 10f);
        trackObject.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.24f, 0.95f);
        trackObject.GetComponent<Image>().raycastTarget = false;

        var fillObject = new GameObject(name + "Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(trackObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Simple;
        fill.color = color;
        fill.raycastTarget = false;
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        return fill;
    }

    private static void SetBarFill(Image fill, float amount)
    {
        fill.rectTransform.localScale = new Vector3(Mathf.Clamp01(amount), 1f, 1f);
    }

    private void ApplySafeArea()
    {
        lastSafeArea = Screen.safeArea;
        safeRoot.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
    }

    private void RefreshHud()
    {
        bool campaign = CombatCampaignSession.IsActive;
        bool bossBattle = CombatGameModeSession.Mode == CombatGameMode.BossBattle;
        missionTitle.text = campaign ? $"CAMPAIGN // MISSION {CombatCampaignSession.MissionIndex + 1:00}" : bossBattle ? "BOSS BATTLE // LEVIATHAN" : StageSession.IsStageRun ? $"BATTLE // STAGE {StageSession.Current.Number:00}" : "SURVIVAL // HOLD THE LINE";
        objectiveText.text = campaign
            ? $"HOSTILES ELIMINATED  {CombatCampaignSession.Kills}/{CombatCampaignSession.RequiredKills}"
            : bossBattle ? "DEFEAT THE LEVIATHAN DREADNOUGHT"
            : StageSession.IsStageRun ? "CLEAR WAVES  •  DESTROY THE DREADNOUGHT" : "OUTLAST THE INCOMING FLEET";
        waveText.text = bossBattle
            ? "PRIMARY OBJECTIVE  •  DESTROY THE BOSS"
            : $"WAVE {(waves ? waves.WaveNumber : 0):00}    •    HOSTILES {(waves ? waves.ActiveEnemyCount : 0):00}";

        if (playerHealth)
        {
            healthText.text = $"HULL  {Mathf.CeilToInt(playerHealth.Health)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
            shieldText.text = $"SHIELD  {Mathf.CeilToInt(playerHealth.Shield)} / {Mathf.CeilToInt(playerHealth.MaxShield)}";
            SetBarFill(healthFill, playerHealth.Health / Mathf.Max(1f, playerHealth.MaxHealth));
            SetBarFill(shieldFill, playerHealth.MaxShield > 0f ? playerHealth.Shield / playerHealth.MaxShield : 0f);
        }

        if (weapons && weapons.ActiveWeapon)
        {
            weaponName.text = weapons.ActiveWeapon.DisplayName;
            weaponSlot.text = $"WEAPON  {weapons.ActiveIndex + 1:00} / {weapons.WeaponCount:00}";
            if (weapons.ActiveWeapon is WeaponHomingMissiles missiles)
            {
                string reload = missiles.ReloadRemaining > 0f ? $"  •  RELOAD {missiles.ReloadRemaining:0.0}s" : string.Empty;
                ammoText.text = $"MISSILES  {missiles.AmmoInMagazine:00}/{missiles.MagazineCapacity:00}{reload}";
            }
            else
            {
                ammoText.text = "ENERGY WEAPON  •  NO MAGAZINE";
            }
        }

        var boss = FindObjectOfType<CombatBossController>();
        bossPanel.gameObject.SetActive(boss);
        if (boss)
        {
            CombatShipHealth bossHealth = boss.GetComponent<CombatShipHealth>();
            if (bossHealth)
            {
                bossTitle.text = $"LEVIATHAN // DREADNOUGHT     {Mathf.CeilToInt(bossHealth.Health)}";
                SetBarFill(bossFill, bossHealth.Health / Mathf.Max(1f, bossHealth.MaxHealth));
            }
        }

        if (specialAbility)
        {
            specialText.text = specialAbility.IsActive ? "SHIELD BURST ACTIVE" : specialAbility.IsReady ? "READY  •  SHIELD BURST" : $"RECHARGING  {specialAbility.CooldownRemaining:0.0}s";
            float readyAmount = 1f - specialAbility.CooldownRemaining / Mathf.Max(0.1f, specialAbility.CooldownDuration);
            SetBarFill(specialFill, readyAmount);
            specialButton.targetGraphic.color = specialAbility.IsReady ? new Color(0.02f, 0.09f, 0.14f, 0.94f) : Panel;
        }

        RefreshTarget();
    }

    private void RefreshTarget()
    {
        CombatEnemy nextTarget = player ? CombatEnemy.FindNearest(player.transform.position, 120f) : null;
        if (!nextTarget && player)
        {
            float nearestDistance = 120f * 120f;
            foreach (CombatEnemy candidate in FindObjectsOfType<CombatEnemy>())
            {
                if (!candidate || !candidate.IsAlive)
                {
                    continue;
                }

                float distance = (candidate.transform.position - player.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nextTarget = candidate;
                }
            }
        }

        if (nextTarget == target)
        {
            UpdateTargetStatus();
            return;
        }

        if (targetHealth)
        {
            targetHealth.Damaged -= HandleTargetDamaged;
        }

        target = nextTarget;
        targetHealth = target ? target.GetComponent<CombatShipHealth>() : null;
        if (targetHealth)
        {
            targetHealth.Damaged += HandleTargetDamaged;
        }

        UpdateTargetStatus();
    }

    private void UpdateTargetStatus()
    {
        if (!targetStatus)
        {
            return;
        }

        if (!target || !player)
        {
            targetStatus.text = "SECTOR SCAN  //  NO HOSTILE LOCK";
            targetStatus.color = new Color(0.66f, 0.77f, 0.92f, 1f);
            return;
        }

        string label = target.name.StartsWith("Enemy_") ? target.name.Substring("Enemy_".Length) : "LEVIATHAN";
        float distance = Vector3.Distance(player.transform.position, target.transform.position);
        targetStatus.text = $"HOSTILE LOCK  //  {label.ToUpperInvariant()}   {distance:0} M";
        targetStatus.color = Cyan;
    }

    private void UpdateTargetMarker()
    {
        if (!target)
        {
            targetMarker.gameObject.SetActive(false);
            return;
        }

        if (!worldCamera)
        {
            worldCamera = ResolveWorldCamera();
            if (!worldCamera)
            {
                targetMarker.gameObject.SetActive(false);
                return;
            }
        }

        Vector3 screen = worldCamera.WorldToScreenPoint(target.AimPoint);
        bool onScreen = screen.z > 0f && screen.x >= 18f && screen.x <= Screen.width - 18f && screen.y >= 18f && screen.y <= Screen.height - 18f;
        if (!onScreen)
        {
            if (screen.z <= 0f)
            {
                screen.x = Screen.width - screen.x;
                screen.y = Screen.height - screen.y;
            }

            Rect safeArea = Screen.safeArea;
            screen.x = Mathf.Clamp(screen.x, safeArea.xMin + 42f, safeArea.xMax - 42f);
            screen.y = Mathf.Clamp(screen.y, safeArea.yMin + 42f, safeArea.yMax - 42f);
        }

        targetMarker.gameObject.SetActive(true);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(safeRoot, screen, null, out Vector2 local))
        {
            targetMarker.anchoredPosition = local;
        }

        string label = target.name.StartsWith("Enemy_") ? target.name.Substring("Enemy_".Length) : "LEVIATHAN";
        targetName.text = $"TARGET // {label.ToUpperInvariant()}";
        targetDistance.text = $"{Vector3.Distance(player.transform.position, target.transform.position):0} M";
    }

    private static Camera ResolveWorldCamera()
    {
        if (Camera.main)
        {
            return Camera.main;
        }

        foreach (Camera candidate in FindObjectsOfType<Camera>())
        {
            if (candidate.enabled && !candidate.targetTexture && candidate.gameObject.name != "ScreenshotUiCamera")
            {
                return candidate;
            }
        }

        return null;
    }

    private void HandlePlayerDamaged(CombatShipHealth _, CombatDamage damage)
    {
        damageTimer = 0.4f;
        damageFlash.color = new Color(Red.r, Red.g, Red.b, 0.08f);
    }

    private void HandleTargetDamaged(CombatShipHealth _, CombatDamage damage)
    {
        hitTimer = 0.8f;
        hitText.text = "HIT CONFIRMED";
        hitText.alpha = 1f;
    }
}
