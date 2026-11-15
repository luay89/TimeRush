using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SpaceCombatPlayModeTests
{
    private const float SceneTimeoutSeconds = 30f;
    private const string BootSceneName = "Boot";
    private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";
    private const string FallbackFeedbackHubName = "GameFeedbackSignals";
    private const float WaveTestPlayerHealth = 1000000f;

    // Device-input component disabled by the current test (re-enabled in TearDown).
    private static CombatPlayerInputSource isolatedInputSource;

    // Wave director paused by the current test (re-enabled in TearDown).
    private static CombatWaveDirector isolatedWaveDirector;

    // Name prefix CombatWaveDirector.SpawnEnemy gives every wave enemy.
    private const string WaveEnemyNamePrefix = "Enemy_";

#if UNITY_EDITOR
    private const string BalanceConfigPath = "Assets/_Project/Config/GameBalanceConfig.asset";
#endif

    // The shared GameBalanceConfig asset whose combat flag a test overrode, and its value before
    // the override. Restored in TearDown, which NUnit runs even when an assertion fails or a
    // WaitUntil times out, so no test can leak its flag into the next test or into the asset.
    private static GameBalanceConfig overriddenConfig;
    private static bool originalCombatFlag;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        RunCountdown.TutorialOverride = null;
        RestoreCombatFlag();
        RestoreDeviceInput();
        RestoreWaveDirector();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // Safety net in case a TearDown was skipped by the runner.
        RestoreCombatFlag();
    }

    [UnityTest]
    public IEnumerator CombatModeInstaller_CreatesMissionController_InGameScene()
    {
        yield return StartRun(stageIndex: 0, enableCombat: true);

        Assert.NotNull(Object.FindObjectOfType<CombatMissionController>());
        Assert.NotNull(Object.FindObjectOfType<CombatPlayerController>());
    }

    [UnityTest]
    public IEnumerator CombatPlayerController_Supports3DMovement()
    {
        yield return StartRun(stageIndex: 0, enableCombat: true);

        var player = Object.FindObjectOfType<CombatPlayerController>();
        Assert.NotNull(player);
        IsolatePlayerFromDeviceInput(player);

        Vector3 start = player.transform.position;

        player.SetRequestedMove(new Vector3(1f, 1f, -1f));
        yield return new WaitForSeconds(0.6f);
        player.SetRequestedMove(Vector3.zero);
        yield return null;

        Vector3 end = player.transform.position;
        Assert.Greater(Mathf.Abs(end.x - start.x), 0.05f, "Expected X movement.");
        Assert.Greater(Mathf.Abs(end.y - start.y), 0.05f, "Expected Y movement.");
        Assert.Greater(Mathf.Abs(end.z - start.z), 0.05f, "Expected Z movement.");
    }

    [UnityTest]
    public IEnumerator AllSixWeapons_CanFire_AndDealDamage()
    {
        yield return StartRun(stageIndex: 0, enableCombat: true);

        var player = Object.FindObjectOfType<CombatPlayerController>();
        Assert.NotNull(player);
        IsolatePlayerFromDeviceInput(player);
        yield return IsolateFromWaveEnemies();

        var weapons = player.GetComponent<CombatWeaponController>();
        Assert.NotNull(weapons);

        var enemy = new GameObject("TestEnemy");
        enemy.transform.position = player.transform.position + Vector3.forward * 10f;
        enemy.AddComponent<SphereCollider>();

        var team = enemy.AddComponent<CombatTeamMember>();
        team.Team = CombatTeam.Enemy;

        var health = enemy.AddComponent<CombatShipHealth>();
        health.SetMaxVitals(5000f, 0f, true);
        enemy.AddComponent<CombatEnemy>();

        for (int i = 0; i < 6; i++)
        {
            if (i > 0)
            {
                weapons.SelectNext();
                yield return null;
            }

            float startHealth = health.Health;
            weapons.SetFireHeld(true);
            yield return new WaitForSeconds(1.25f);
            weapons.SetFireHeld(false);
            yield return new WaitForSeconds(0.4f);

            Assert.Less(health.Health, startHealth - 0.01f, $"Weapon index {i} did not deal damage.");
        }
    }

    [UnityTest]
    public IEnumerator EnemyAI_CanMove_AndFireAtPlayer()
    {
        yield return StartRun(stageIndex: 0, enableCombat: true);

        var player = Object.FindObjectOfType<CombatPlayerController>();
        Assert.NotNull(player);

        var enemy = new GameObject("AITestEnemy");
        enemy.transform.position = player.transform.position + new Vector3(0f, 0f, 18f);
        enemy.AddComponent<SphereCollider>();

        var team = enemy.AddComponent<CombatTeamMember>();
        team.Team = CombatTeam.Enemy;

        enemy.AddComponent<CombatShipHealth>().SetMaxVitals(250f, 0f, true);
        enemy.AddComponent<CombatEnemy>();

        var brain = enemy.AddComponent<CombatEnemyBrain>();
        brain.Configure(CombatEnemyArchetype.HeavyFighter, player);

        Vector3 startPos = enemy.transform.position;
        yield return new WaitForSeconds(0.75f);

        Vector3 endPos = enemy.transform.position;
        Assert.Greater((endPos - startPos).sqrMagnitude, 0.0005f, "Enemy AI did not move.");

        int boltsBefore = Object.FindObjectsOfType<CombatProjectile>().Length;
        yield return new WaitForSeconds(1.6f);
        int boltsAfter = Object.FindObjectsOfType<CombatProjectile>().Length;

        Assert.Greater(boltsAfter, boltsBefore, "Enemy AI did not fire projectiles.");
    }

    [UnityTest]
    public IEnumerator EnemyHealth_CanBeDestroyed_AndExplosionSpawns()
    {
        yield return StartRun(stageIndex: 0, enableCombat: true);

        var player = Object.FindObjectOfType<CombatPlayerController>();
        Assert.NotNull(player);

        var enemy = new GameObject("KillTestEnemy");
        enemy.transform.position = player.transform.position + Vector3.forward * 8f;
        enemy.AddComponent<SphereCollider>();

        var team = enemy.AddComponent<CombatTeamMember>();
        team.Team = CombatTeam.Enemy;

        var health = enemy.AddComponent<CombatShipHealth>();
        health.SetMaxVitals(20f, 0f, true);
        enemy.AddComponent<CombatEnemy>();

        bool died = false;
        health.Died += _ => died = true;
        health.TryApplyDamage(new CombatDamage(999f, CombatDamageKind.Kinetic), instigator: player.gameObject);

        yield return null;
        yield return null;

        Assert.IsTrue(died, "Enemy did not raise Died event.");
        Assert.IsTrue(enemy == null, "Enemy game object should be destroyed.");

        // Explosion is a short-lived effect; just ensure at least one was spawned.
        Assert.NotNull(Object.FindObjectOfType<CombatExplosionEffect>(), "Expected an explosion effect instance.");
    }

    [UnityTest]
    public IEnumerator Waves_SpawnMultipleEnemies_AndBossCanBeDefeated_ForStageVictory()
    {
        yield return StartRun(stageIndex: 0, enableCombat: true);

        var gc = GameController.Instance;
        Assert.NotNull(gc);

        var wave = Object.FindObjectOfType<CombatWaveDirector>();
        Assert.NotNull(wave);

        // This test checks wave progression and boss victory, not player survival: a player that
        // never moves or fires is otherwise killed by the first wave before the stage goal.
        // Enemy AI, projectiles and damage all stay active; only this run's player health is raised.
        var playerShip = Object.FindObjectOfType<CombatPlayerController>();
        Assert.NotNull(playerShip, "Combat player ship missing.");
        var playerHealth = playerShip.GetComponent<CombatShipHealth>();
        Assert.NotNull(playerHealth, "Combat player health missing.");
        playerHealth.SetMaxVitals(WaveTestPlayerHealth, playerHealth.MaxShield, refill: true);

        // Ensure initial wave spawns.
        yield return new WaitForSeconds(2.2f);
        int enemiesAfterFirst = wave.ActiveEnemyCount;
        Assert.Greater(enemiesAfterFirst, 0, "No enemies spawned in first wave.");

        // Ensure more waves can happen.
        yield return new WaitForSeconds(7.5f);
        int enemiesLater = wave.ActiveEnemyCount;
        Assert.GreaterOrEqual(enemiesLater, enemiesAfterFirst, "Enemy count did not progress with waves.");

        // Fast-forward to the stage goal so boss gating becomes eligible.
        Time.timeScale = 25f;
        yield return WaitUntil(() => gc.StageGoalReached, SceneTimeoutSeconds, "stage goal");

        // Clear current enemies so boss can spawn (boss gate requires <= 2 enemies).
        foreach (var enemy in Object.FindObjectsOfType<CombatShipHealth>())
        {
            if (!enemy || !enemy.IsAlive)
            {
                continue;
            }

            if (enemy.TryGetComponent<CombatTeamMember>(out var team) && team.Team == CombatTeam.Enemy)
            {
                enemy.TryApplyDamage(new CombatDamage(99999f, CombatDamageKind.Kinetic), instigator: gc.gameObject);
            }
        }

        yield return null;
        yield return WaitUntil(() => GameObject.Find("Boss_Leviathan") != null, SceneTimeoutSeconds, "boss spawn");

        var boss = GameObject.Find("Boss_Leviathan");
        Assert.NotNull(boss);

        var bossHealth = boss.GetComponent<CombatShipHealth>();
        Assert.NotNull(bossHealth);

        // Defeat boss.
        bossHealth.TryApplyDamage(new CombatDamage(999999f, CombatDamageKind.Kinetic), instigator: gc.gameObject);

        yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Results, SceneTimeoutSeconds, "results scene");
        Assert.IsTrue(StageSession.HasLastResult);
        Assert.IsTrue(StageSession.LastCleared, "Stage should be marked cleared after boss defeat.");
    }

    [UnityTest]
    public IEnumerator PlayerHealth_ShieldsAbsorbBeforeHealth_AndDeathTriggersResults()
    {
        yield return StartRun(stageIndex: 0, enableCombat: true);

        var player = Object.FindObjectOfType<CombatPlayerController>();
        Assert.NotNull(player);

        var health = player.GetComponent<CombatShipHealth>();
        Assert.NotNull(health);

        float startHealth = health.Health;
        float startShield = health.Shield;

        // Shield should absorb first.
        health.TryApplyDamage(new CombatDamage(startShield * 0.5f, CombatDamageKind.Energy), instigator: player.gameObject);
        yield return null;
        Assert.Less(health.Shield, startShield, "Expected shield to decrease.");
        Assert.AreEqual(startHealth, health.Health, 0.001f, "Expected health to remain unchanged while shield exists.");

        // Kill player.
        health.TryApplyDamage(new CombatDamage(999999f, CombatDamageKind.Kinetic), instigator: player.gameObject);
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Results, SceneTimeoutSeconds, "results scene");
    }

    [UnityTest]
    public IEnumerator BackgroundingTheApp_PausesTheRun_UntilThePlayerResumes()
    {
        yield return StartRun(stageIndex: -1, enableCombat: true);

        var gsm = GameStateMachine.Instance;
        Assert.AreEqual(GameStateKind.Playing, gsm.CurrentState, "Precondition: run in progress.");

        // Unity sends OnApplicationPause(true) to every MonoBehaviour when Android backgrounds the app.
        gsm.gameObject.SendMessage("OnApplicationPause", true);
        Assert.AreEqual(GameStateKind.Paused, gsm.CurrentState, "Backgrounding the app must pause the run.");
        Assert.AreEqual(0f, Time.timeScale, "A backgrounded run must not keep simulating.");

        yield return null;

        // Coming back to the app must not resume on its own: the player resumes from the pause menu.
        gsm.gameObject.SendMessage("OnApplicationPause", false);
        Assert.AreEqual(GameStateKind.Paused, gsm.CurrentState, "Returning to the app must leave the run paused.");

        Assert.IsTrue(gsm.Resume(), "The paused run must be resumable.");
        Assert.AreEqual(GameStateKind.Playing, gsm.CurrentState);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator LegacyRunnerSystems_AreNotRegressed_WhenCombatDisabled()
    {
        yield return StartRun(stageIndex: -1, enableCombat: false);

        Assert.IsNull(Object.FindObjectOfType<CombatMissionController>(), "Combat mission should not boot when disabled.");

        var legacyPlayer = Object.FindObjectOfType<PlayerController>();
        Assert.NotNull(legacyPlayer, "Legacy PlayerController should still exist.");
        Assert.IsTrue(legacyPlayer.enabled, "Legacy PlayerController should remain enabled when combat is disabled.");
    }

    private static IEnumerator StartRun(int stageIndex, bool enableCombat)
    {
        RunCountdown.TutorialOverride = false;

        // Every test boots from the same clean state, regardless of which scene the previous test
        // ended in (including a Boot scene that never got past startup).
        yield return ResetToCleanBootState();

        yield return SceneManager.LoadSceneAsync(BootSceneName, LoadSceneMode.Single);
        yield return WaitUntil(() => GameStateMachine.HasInstance, SceneTimeoutSeconds, "Boot's GameStateMachine to initialise");
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.MenuHub, SceneTimeoutSeconds, "menu scene");
        yield return WaitUntil(() => GameStateMachine.HasInstance, SceneTimeoutSeconds, "game state machine");

        // GameController only exists in the Game scene, and the combat runtime reads the flag in the
        // same frame that scene loads (CombatModeInstaller on sceneLoaded, CombatMissionController.Awake).
        // So the flag must be set on the shared config asset before the Game scene is requested.
        OverrideCombatFlag(enableCombat);

        if (stageIndex >= 0)
        {
            StageSession.SelectStage(stageIndex);
        }
        else
        {
            StageSession.SelectEndless();
        }

        bool started = GameStateMachine.Instance.StartRunFromMenu();
        Assert.IsTrue(started, "Failed to start run from menu.");

        yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Game, SceneTimeoutSeconds, "game scene");
        yield return WaitUntil(() => GameController.Instance != null, SceneTimeoutSeconds, "game controller");

        var gc = GameController.Instance;
        Assert.NotNull(gc);
        Assert.AreSame(overriddenConfig, gc.BalanceConfig, "Game scene uses a different GameBalanceConfig than the one the test configured.");
        Assert.AreEqual(enableCombat, gc.BalanceConfig.enableSpaceCombat, "Combat flag was not applied before the Game scene loaded.");

        yield return WaitUntil(() => !RunCountdown.IsCounting, SceneTimeoutSeconds, "run countdown");

        // The flag stays as requested for the whole test body; TearDown restores it.
    }

    /// <summary>
    /// Removes the two persistent leftovers that stop Boot from starting a second time:
    ///  1. The previous test's Boot bootstrap object (DontDestroyOnLoad, owns GameStateMachine and
    ///     its own GameFeedbackSignals). If it survives, the new Boot's GameStateMachine duplicate
    ///     check destroys the new bootstrap object.
    ///  2. The bare fallback hub that GameFeedbackSignals.EnsureHubWhenNotStartingFromBoot creates
    ///     because the test runner starts in InitTestScene instead of Boot. If it survives, Boot's
    ///     GameFeedbackSignals duplicate check destroys Boot's whole bootstrap object (BootLoader and
    ///     GameStateMachine included) before BootLoader.Start can request MenuHub.
    /// Anything that does not match these exact shapes is left alone and fails the setup instead.
    /// </summary>
    private static IEnumerator ResetToCleanBootState()
    {
        GameObject previousBootstrap = null;
        if (GameStateMachine.HasInstance)
        {
            previousBootstrap = GameStateMachine.Instance.gameObject;
            Assert.AreEqual(DontDestroyOnLoadSceneName, previousBootstrap.scene.name,
                $"Setup expected the previous GameStateMachine on a persistent Boot bootstrap object, found it on '{previousBootstrap.name}' in scene '{previousBootstrap.scene.name}'.");
            Object.Destroy(previousBootstrap);
        }

        if (GameFeedbackSignals.HasInstance)
        {
            GameObject hubHost = GameFeedbackSignals.Instance.gameObject;
            if (hubHost != previousBootstrap)
            {
                Assert.IsTrue(IsBareFallbackFeedbackHub(hubHost),
                    $"Setup found an unexpected GameFeedbackSignals host '{hubHost.name}' ({DescribeComponents(hubHost)}) and refuses to destroy it.");
                Object.Destroy(hubHost);
            }
        }

        // Destroy() completes at the end of the frame; both singletons clear themselves in OnDestroy.
        yield return WaitUntil(() => !GameStateMachine.HasInstance && !GameFeedbackSignals.HasInstance,
            SceneTimeoutSeconds, "previous Boot bootstrap and fallback feedback hub to be destroyed");
    }

    private static bool IsBareFallbackFeedbackHub(GameObject host)
    {
        if (host.name != FallbackFeedbackHubName || host.scene.name != DontDestroyOnLoadSceneName
            || host.transform.parent != null || host.transform.childCount != 0)
        {
            return false;
        }

        // Exactly what EnsureHubWhenNotStartingFromBoot creates: a Transform and the hub, nothing else.
        var components = host.GetComponents<Component>();
        return components.Length == 2 && components[0] is Transform && components[1] is GameFeedbackSignals;
    }

    private static string DescribeComponents(GameObject host)
    {
        var components = host.GetComponents<Component>();
        var names = new string[components.Length];
        for (int i = 0; i < components.Length; i++)
        {
            names[i] = components[i] ? components[i].GetType().Name : "MissingScript";
        }

        return string.Join(", ", names);
    }

    /// <summary>
    /// CombatPlayerInputSource pushes the real keyboard/touch state into the controller and weapons
    /// every frame (SetRequestedMove / SetFireHeld), which in batch mode is always "no input" and
    /// overwrites what the test drives. Disabling only that component lets the test drive the real
    /// CombatPlayerController and CombatWeaponController through their public input methods.
    /// </summary>
    private static void IsolatePlayerFromDeviceInput(CombatPlayerController player)
    {
        var input = player.GetComponent<CombatPlayerInputSource>();
        Assert.NotNull(input, "Combat player has no CombatPlayerInputSource to isolate.");
        input.enabled = false;
        isolatedInputSource = input;

        Assert.IsTrue(player.enabled, "CombatPlayerController must stay enabled.");
        var weapons = player.GetComponent<CombatWeaponController>();
        Assert.IsTrue(weapons == null || weapons.enabled, "CombatWeaponController must stay enabled.");
    }

    /// <summary>
    /// Every player weapon aims at CombatEnemy.FindNearest, so wave enemies spawned by the stage run
    /// can take the shots meant for a test's own target. Pauses only the wave director and removes
    /// only the enemies it spawned (identified by its naming and an AI brain); the test target is
    /// created afterwards and every weapon, projectile and damage system stays active.
    /// </summary>
    private static IEnumerator IsolateFromWaveEnemies()
    {
        var director = Object.FindObjectOfType<CombatWaveDirector>();
        Assert.NotNull(director, "Combat run has no CombatWaveDirector to pause.");
        director.enabled = false;
        isolatedWaveDirector = director;

        foreach (var enemy in Object.FindObjectsOfType<CombatEnemy>())
        {
            if (enemy.name.StartsWith(WaveEnemyNamePrefix) && enemy.GetComponent<CombatEnemyBrain>())
            {
                Object.Destroy(enemy.gameObject);
            }
        }

        // Destroy() completes at the end of the frame.
        yield return WaitUntil(() => Object.FindObjectsOfType<CombatEnemy>().Length == 0,
            SceneTimeoutSeconds, "wave enemies to be removed before creating the test target");
    }

    private static void RestoreWaveDirector()
    {
        // The director lives in the Game scene, so it may already be destroyed.
        if (isolatedWaveDirector)
        {
            isolatedWaveDirector.enabled = true;
        }

        isolatedWaveDirector = null;
    }

    private static void RestoreDeviceInput()
    {
        // The component lives on the Game scene's player, so it may already be destroyed.
        if (isolatedInputSource)
        {
            isolatedInputSource.enabled = true;
        }

        isolatedInputSource = null;
    }

    private static void OverrideCombatFlag(bool enableCombat)
    {
        var config = LoadBalanceConfig();
        Assert.NotNull(config, "Could not load GameBalanceConfig before starting the run.");

        // Remember the original value only once per test, so a second StartRun in the same test
        // cannot record the overridden value as the "original".
        if (overriddenConfig == null)
        {
            overriddenConfig = config;
            originalCombatFlag = config.enableSpaceCombat;
        }

        config.enableSpaceCombat = enableCombat;
    }

    private static void RestoreCombatFlag()
    {
        if (overriddenConfig != null)
        {
            // Restoring the exact original value means the in-memory asset matches the file on disk
            // again, so even a later editor save cannot write a changed flag.
            overriddenConfig.enableSpaceCombat = originalCombatFlag;
        }

        overriddenConfig = null;
    }

    private static GameBalanceConfig LoadBalanceConfig()
    {
#if UNITY_EDITOR
        // In the editor this returns the same in-memory asset instance that Game.unity references.
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalanceConfigPath);
#else
        // In a player build the config is only loaded with the Game scene, which is too late.
        Assert.Fail("SpaceCombatPlayModeTests must run in the Unity Editor (they configure GameBalanceConfig before the Game scene loads).");
        return null;
#endif
    }

    private static IEnumerator WaitUntil(System.Func<bool> predicate, float timeoutSeconds, string condition)
    {
        float start = Time.realtimeSinceStartup;
        while (!predicate())
        {
            if (Time.realtimeSinceStartup - start > timeoutSeconds)
            {
                string state = GameStateMachine.HasInstance ? GameStateMachine.Instance.CurrentState.ToString() : "missing";
                Assert.Fail($"Timed out after {timeoutSeconds:0.0}s waiting for {condition}; scene={SceneManager.GetActiveScene().name}, state={state}, countdown={RunCountdown.IsCounting}, timeScale={Time.timeScale:0.##}.");
            }

            yield return null;
        }
    }
}
