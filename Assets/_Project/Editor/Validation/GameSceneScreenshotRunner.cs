#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Dev-only visual check: boots the real game flow (Boot -> MenuHub -> Game -> Results) in Play
/// Mode and renders what the player would see -- 3D camera plus uGUI canvases and the UI Toolkit
/// menu -- to PNG files, so screens can be reviewed without a person pressing Play. Never part of
/// a build (Editor folder).
/// Batch usage: Unity -batchmode -projectPath . -executeMethod GameSceneScreenshotRunner.RunBatch
/// Environment variables (all optional):
///   TIMERUSH_SHOT_DIR      output folder (defaults to Logs/Screenshots)
///   TIMERUSH_SHOT_SIZE     capture size, e.g. "720x1280" for a portrait phone (default 1280x720)
///   TIMERUSH_SHOT_TIMES    comma-separated seconds of gameplay to capture at (default 1.5,3,4.2)
///   TIMERUSH_SHOT_GODMODE  "1" keeps the ship invulnerable so long runs don't end on a crash
///   TIMERUSH_SHOT_SCORE    score added at run start
///   TIMERUSH_SHOT_MENU     "1" captures the menu; a comma list of panel names (e.g.
///                          "settings,stages,help,credits") also captures each menu panel
///   TIMERUSH_SHOT_MENU_ONLY "1" stops after the menu captures (no run)
///   TIMERUSH_SHOT_LAYERS   "1" also saves the 3D, canvas and UI Toolkit layers separately
///   TIMERUSH_SHOT_STAGE    stage number to launch instead of an endless run
///   TIMERUSH_SHOT_MODE     survival, campaign, or boss
///   TIMERUSH_SHOT_FIRE     "1" holds the primary weapon during gameplay
///   TIMERUSH_SHOT_WEAPON   zero-based combat weapon slot to select
///   TIMERUSH_SHOT_SPECIAL  "1" activates the combat shield burst
///   TIMERUSH_SHOT_BOSS_VICTORY "1" completes a boss-battle validation run as a victory
///   TIMERUSH_SHOT_PAUSE    "1" opens the pause menu after the last gameplay capture and captures it
///   TIMERUSH_SHOT_RESULTS  "1" ends the run after the gameplay captures and captures Results
///   TIMERUSH_SHOT_TUTORIAL "1" shows the first-run "how to fly" card (skipped otherwise)
/// </summary>
[InitializeOnLoad]
public static class GameSceneScreenshotRunner
{
    private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
    private const string ActiveKey = "TimeRush.Shot.Active";
    private const string BatchKey = "TimeRush.Shot.Batch";
    private const float TimeoutSeconds = 120f;
    private static int Width = 1280;
    private static int Height = 720;
    private const int UiLayer = 31;

    private static readonly float[] DefaultCaptureTimes = { 1.5f, 3f, 4.2f };

    private enum Phase
    {
        WaitMenu,
        Menu,
        WaitGame,
        Game,
        Pause,
        WaitResults,
        Results,
        Done
    }

    private static Phase phase;
    private static float phaseStartedAt;
    private static float startedAt;
    private static float[] captureTimes = DefaultCaptureTimes;
    private static int nextCapture;
    private static int menuStep;
    private static string[] menuPanels = new string[0];
    private static bool bossDamageApplied;
    private static bool specialAbilityActivated;
    private static RenderTexture panelTarget;
    private static PanelSettings hookedPanel;
    private static bool runRequested;

    static GameSceneScreenshotRunner()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            startedAt = Time.realtimeSinceStartup;
        }
    }

    [MenuItem("TimeRush/Validation/Capture Game Scene Screenshots")]
    public static void RunFromMenu()
    {
        Start(false);
    }

    public static void RunBatch()
    {
        Start(true);
    }

    private static void Start(bool batch)
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(BatchKey, batch);
        bossDamageApplied = false;
        specialAbilityActivated = false;
        EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        startedAt = Time.realtimeSinceStartup;
        EditorApplication.isPlaying = true;
    }

    private static string Env(string name) => Environment.GetEnvironmentVariable(name);

    private static void Enter(Phase next)
    {
        phase = next;
        phaseStartedAt = Time.realtimeSinceStartup;
    }

    private static float PhaseAge => Time.realtimeSinceStartup - phaseStartedAt;

    private static void Update()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        float lastTime = captureTimes.Length > 0 ? captureTimes[captureTimes.Length - 1] : 0f;
        if (Time.realtimeSinceStartup - startedAt > TimeoutSeconds + lastTime)
        {
            Finish("Timed out before all screenshots were captured.", 1);
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        AttachCanvases();
        switch (phase)
        {
            case Phase.WaitMenu:
                ReadSize();
                if (sceneName == SceneNames.MenuHub && GameStateMachine.HasInstance)
                {
                    string menu = Env("TIMERUSH_SHOT_MENU");
                    if (!string.IsNullOrEmpty(menu))
                    {
                        menuPanels = menu == "1" ? new string[0] : menu.Split(',');
                        menuStep = -1;
                        HookPanel();
                        Enter(Phase.Menu);
                    }
                    else
                    {
                        LaunchRun();
                    }
                }

                break;

            case Phase.Menu:
                TickMenu();
                break;

            case Phase.WaitGame:
                if (sceneName == SceneNames.Game && GameController.Instance)
                {
                    captureTimes = ReadCaptureTimes();
                    nextCapture = 0;
                    if (int.TryParse(Env("TIMERUSH_SHOT_SCORE"), out int score) && score > 0)
                    {
                        GameController.Instance.AddScore(score);
                    }

                    Enter(Phase.Game);
                }

                break;

            case Phase.Game:
                TickGame(sceneName);
                break;

            case Phase.Pause:
                if (PhaseAge > 0.6f)
                {
                    Capture("pause");
                    GameStateMachine.Instance.Resume();
                    AfterGameplay();
                }

                break;

            case Phase.WaitResults:
                if (sceneName == SceneNames.Results)
                {
                    Enter(Phase.Results);
                }

                break;

            case Phase.Results:
                if (PhaseAge > 2.5f)
                {
                    Capture("results");
                    Finish(null, 0);
                }

                break;
        }
    }

    private static void TickMenu()
    {
        var menu = UnityEngine.Object.FindObjectOfType<MenuHubUI>();
        if (!menu || PhaseAge < 2.2f)
        {
            return;
        }

        Capture(menuStep < 0 ? "menu" : "menu_" + menuPanels[menuStep]);
        if (menuStep >= 0)
        {
            menu.ClosePanels();
        }

        menuStep++;
        if (menuStep < menuPanels.Length)
        {
            menu.OpenPanelForCapture(menuPanels[menuStep].Trim());
            phaseStartedAt = Time.realtimeSinceStartup - 1.6f;
            return;
        }

        UnhookPanel();
        if (Env("TIMERUSH_SHOT_MENU_ONLY") == "1")
        {
            Finish(null, 0);
            return;
        }

        LaunchRun();
    }

    private static void LaunchRun()
    {
        if (runRequested)
        {
            return;
        }

        string mode = (Env("TIMERUSH_SHOT_MODE") ?? string.Empty).Trim().ToLowerInvariant();
        if (mode == "campaign")
        {
            CombatGameModeSession.BeginCampaign();
        }
        else if (mode == "boss")
        {
            CombatGameModeSession.BeginBossBattle();
        }
        else if (int.TryParse(Env("TIMERUSH_SHOT_STAGE"), out int stage) && stage > 0)
        {
            CombatGameModeSession.BeginStage(stage - 1);
        }
        else
        {
            CombatGameModeSession.BeginSurvival();
        }

        RunCountdown.TutorialOverride = Env("TIMERUSH_SHOT_TUTORIAL") == "1";
        runRequested = GameStateMachine.Instance.StartRunFromMenu();
        Enter(Phase.WaitGame);
    }

    private static void TickGame(string sceneName)
    {
        if (sceneName == SceneNames.Results)
        {
            // The unattended run crashed (or cleared its stage); keep whatever was captured.
            if (Env("TIMERUSH_SHOT_RESULTS") == "1")
            {
                Enter(Phase.Results);
                return;
            }

            Finish(nextCapture > 0 ? null : "Run ended before the first capture.", nextCapture > 0 ? 0 : 1);
            return;
        }

        if (sceneName != SceneNames.Game || !GameController.Instance)
        {
            return;
        }

        if (Env("TIMERUSH_SHOT_GODMODE") == "1")
        {
            GameController.Instance.GrantInvulnerability(1f);
            CombatPlayerController protectedPlayer = UnityEngine.Object.FindObjectOfType<CombatPlayerController>();
            CombatShipHealth playerHealth = protectedPlayer ? protectedPlayer.GetComponent<CombatShipHealth>() : null;
            if (playerHealth)
            {
                playerHealth.SetTemporaryInvulnerable(1f);
            }
        }

        CombatPlayerInputSource inputSource = UnityEngine.Object.FindObjectOfType<CombatPlayerInputSource>();
        if (inputSource)
        {
            inputSource.SetVisualValidationFireHeld(Env("TIMERUSH_SHOT_FIRE") == "1");
        }

        if (Env("TIMERUSH_SHOT_SPECIAL") == "1" && !specialAbilityActivated)
        {
            CombatSpecialAbilityController ability = UnityEngine.Object.FindObjectOfType<CombatSpecialAbilityController>();
            specialAbilityActivated = ability && ability.Activate();
        }

        if (int.TryParse(Env("TIMERUSH_SHOT_WEAPON"), out int weaponIndex))
        {
            CombatWeaponController weapons = UnityEngine.Object.FindObjectOfType<CombatWeaponController>();
            if (weapons && weapons.WeaponCount > 0)
            {
                int targetIndex = Mathf.Clamp(weaponIndex, 0, weapons.WeaponCount - 1);
                while (weapons.ActiveIndex != targetIndex)
                {
                    weapons.SelectNext();
                }
            }
        }

        if (nextCapture < captureTimes.Length && PhaseAge >= captureTimes[nextCapture])
        {
            Capture("game_" + nextCapture);
            nextCapture++;
            ApplyValidationBossDamage();
        }

        if (nextCapture < captureTimes.Length)
        {
            return;
        }

        if (Env("TIMERUSH_SHOT_PAUSE") == "1" && GameStateMachine.Instance.Pause())
        {
            Enter(Phase.Pause);
            return;
        }

        AfterGameplay();
    }

    private static void AfterGameplay()
    {
        if (Env("TIMERUSH_SHOT_BOSS_VICTORY") == "1")
        {
            CombatBossController boss = UnityEngine.Object.FindObjectOfType<CombatBossController>();
            CombatShipHealth health = boss ? boss.GetComponent<CombatShipHealth>() : null;
            CombatPlayerController player = UnityEngine.Object.FindObjectOfType<CombatPlayerController>();
            if (!health || !player)
            {
                Finish("Boss-victory capture requested, but the boss or player ship was not found.", 1);
                return;
            }

            bool applied = health.TryApplyDamage(new CombatDamage(100000f, CombatDamageKind.Explosive), player.gameObject);
            Debug.Log($"[BossVictoryCapture] damageApplied={applied} mode={CombatGameModeSession.Mode} completed={CombatGameModeSession.BossBattleCompleted} stageCleared={(GameController.Instance && GameController.Instance.IsStageCleared)}");
            Capture("boss_destroyed");
            Enter(Phase.WaitResults);
            return;
        }

        if (Env("TIMERUSH_SHOT_RESULTS") == "1")
        {
            GameController.Instance.TriggerGameOver();
            Enter(Phase.WaitResults);
            return;
        }

        Finish(null, 0);
    }

    private static void ApplyValidationBossDamage()
    {
        if (bossDamageApplied || (Env("TIMERUSH_SHOT_MODE") ?? string.Empty).Trim().ToLowerInvariant() != "boss")
        {
            return;
        }

        string rawDamage = Env("TIMERUSH_SHOT_BOSS_DAMAGE");
        if (!float.TryParse(rawDamage, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float amount) || amount <= 0f)
        {
            return;
        }

        CombatBossController boss = UnityEngine.Object.FindObjectOfType<CombatBossController>();
        CombatShipHealth health = boss ? boss.GetComponent<CombatShipHealth>() : null;
        CombatPlayerController player = UnityEngine.Object.FindObjectOfType<CombatPlayerController>();
        if (!health || !player)
        {
            return;
        }

        bossDamageApplied = true;
        health.TryApplyDamage(new CombatDamage(amount, CombatDamageKind.Energy), player.gameObject);
    }

    private static void ReadSize()
    {
        string[] parts = (Env("TIMERUSH_SHOT_SIZE") ?? string.Empty).Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && w >= 64 && h >= 64)
        {
            Width = w;
            Height = h;
        }
    }

    private static float[] ReadCaptureTimes()
    {
        string raw = Env("TIMERUSH_SHOT_TIMES");
        if (string.IsNullOrEmpty(raw))
        {
            return DefaultCaptureTimes;
        }

        var times = new List<float>();
        foreach (string part in raw.Split(','))
        {
            if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t) && t >= 0f)
            {
                times.Add(t);
            }
        }

        times.Sort();
        return times.Count > 0 ? times.ToArray() : DefaultCaptureTimes;
    }

    // UI Toolkit panels draw to the screen, which batch mode doesn't have; point the shared panel
    // at a texture for the duration of the menu captures (restored before the run starts).
    private static void HookPanel()
    {
        var document = UnityEngine.Object.FindObjectOfType<UIDocument>();
        if (!document || !document.panelSettings)
        {
            return;
        }

        hookedPanel = document.panelSettings;
        panelTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        hookedPanel.targetTexture = panelTarget;
        hookedPanel.clearColor = true;
        hookedPanel.colorClearValue = Color.clear;
    }

    private static void UnhookPanel()
    {
        if (hookedPanel)
        {
            hookedPanel.targetTexture = null;
            hookedPanel.clearColor = false;
            hookedPanel = null;
        }

        if (panelTarget)
        {
            panelTarget.Release();
            UnityEngine.Object.DestroyImmediate(panelTarget);
            panelTarget = null;
        }
    }

    private static void Capture(string name)
    {
        Camera camera = Camera.main;
        if (!camera)
        {
            Debug.LogError("[Screenshot] No main camera in scene " + SceneManager.GetActiveScene().name);
            return;
        }

        string directory = Env("TIMERUSH_SHOT_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Screenshots");
        }

        Directory.CreateDirectory(directory);

        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = previousTarget;
        bool layers = Env("TIMERUSH_SHOT_LAYERS") == "1";
        Texture2D image = Read(target);
        Opaque(image);
        if (layers)
        {
            File.WriteAllBytes(Path.Combine(directory, name + "__scene.png"), image.EncodeToPNG());
        }

        // uGUI and UI Toolkit both write premultiplied colour, so each is drawn into its own
        // transparent texture and blended over the scene here.
        var canvasTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = canvasTarget;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previousActive;
        if (RenderOverlayCanvases(canvasTarget))
        {
            AddLayer(image, canvasTarget, layers ? Path.Combine(directory, name + "__canvas.png") : null);
        }

        canvasTarget.Release();
        UnityEngine.Object.DestroyImmediate(canvasTarget);

        if (panelTarget)
        {
            RepaintPanels();
            AddLayer(image, panelTarget, layers ? Path.Combine(directory, name + "__panel.png") : null);
        }

        string path = Path.Combine(directory, name + ".png");
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        Debug.Log("[Screenshot] Saved " + path);
    }

    // Screen-space-overlay canvases never reach a camera's target texture. For the whole test run
    // every overlay canvas is re-parented onto a disabled UI-only camera (switching mode on the
    // capture frame itself draws nothing), and each capture renders that camera into its own
    // transparent texture.
    private static Camera uiCamera;

    private static void AttachCanvases()
    {
        if (!uiCamera)
        {
            var cameraObject = new GameObject("ScreenshotUiCamera");
            UnityEngine.Object.DontDestroyOnLoad(cameraObject);
            uiCamera = cameraObject.AddComponent<Camera>();
            uiCamera.enabled = false;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = Color.clear;
            uiCamera.cullingMask = 1 << UiLayer;
            uiCamera.orthographic = true;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 10f;
        }

        if (!uiCamera.targetTexture || uiCamera.targetTexture.width != Width || uiCamera.targetTexture.height != Height)
        {
            uiCamera.targetTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        }

        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
            }

            if (canvas.isRootCanvas && canvas.worldCamera == uiCamera)
            {
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = UiLayer;
                }
            }
        }
    }

    private static bool RenderOverlayCanvases(RenderTexture target)
    {
        if (!uiCamera)
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();
        RenderTexture own = uiCamera.targetTexture;
        uiCamera.targetTexture = target;
        uiCamera.Render();
        uiCamera.targetTexture = own;
        return true;
    }

    // Batch mode has no Game View, so the player loop never repaints off-screen UI Toolkit panels;
    // drive the same internal steps the player loop would.
    private static void RepaintPanels()
    {
        Type utility = typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
        if (utility == null)
        {
            Debug.LogWarning("[Screenshot] UIElementsRuntimeUtility not found; menu UI will be missing.");
            return;
        }

        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (string method in new[] { "UpdateRuntimePanels", "RepaintOffscreenPanels" })
        {
            var info = utility.GetMethod(method, flags, null, Type.EmptyTypes, null);
            if (info == null)
            {
                Debug.LogWarning("[Screenshot] UIElementsRuntimeUtility." + method + " not found.");
                continue;
            }

            info.Invoke(null, null);
        }
    }

    private static void AddLayer(Texture2D image, RenderTexture source, string debugPath)
    {
        Texture2D layer = Read(source);
        if (debugPath != null)
        {
            File.WriteAllBytes(debugPath, layer.EncodeToPNG());
        }

        Composite(image, layer);
        UnityEngine.Object.DestroyImmediate(layer);
    }

    // The camera's alpha channel is meaningless for a screenshot; keep the PNG fully opaque.
    private static void Opaque(Texture2D image)
    {
        Color32[] pixels = image.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].a = 255;
        }

        image.SetPixels32(pixels);
        image.Apply();
    }

    private static Texture2D Read(RenderTexture source)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = source;
        var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        return image;
    }

    private static void Composite(Texture2D baseImage, Texture2D overlay)
    {
        Color32[] under = baseImage.GetPixels32();
        Color32[] over = overlay.GetPixels32();
        for (int i = 0; i < under.Length && i < over.Length; i++)
        {
            float a = over[i].a / 255f;
            under[i] = new Color32(
                (byte)Mathf.Min(255f, over[i].r + under[i].r * (1f - a)),
                (byte)Mathf.Min(255f, over[i].g + under[i].g * (1f - a)),
                (byte)Mathf.Min(255f, over[i].b + under[i].b * (1f - a)),
                255);
        }

        baseImage.SetPixels32(under);
        baseImage.Apply();
    }

    private static void Finish(string error, int exitCode)
    {
        bool batch = SessionState.GetBool(BatchKey, false);
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Update;
        UnhookPanel();
        runRequested = false;
        nextCapture = 0;
        Enter(Phase.WaitMenu);

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("[Screenshot] " + error);
        }

        if (batch)
        {
            EditorApplication.Exit(exitCode);
        }
        else
        {
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
