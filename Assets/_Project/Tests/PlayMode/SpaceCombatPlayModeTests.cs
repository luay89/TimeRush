using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SpaceCombatPlayModeTests
{
    private const float SceneTimeoutSeconds = 30f;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        RunCountdown.TutorialOverride = null;
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

        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene != "Boot" && activeScene != SceneNames.MenuHub)
        {
            if (GameStateMachine.HasInstance)
            {
                Object.Destroy(GameStateMachine.Instance.gameObject);
                yield return null;
            }

            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
        }

        yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.MenuHub, SceneTimeoutSeconds, "menu scene");
        yield return WaitUntil(() => GameStateMachine.HasInstance, SceneTimeoutSeconds, "game state machine");

        var gc = GameController.Instance;
        Assert.NotNull(gc);

        bool originalCombatFlag = gc.BalanceConfig.enableSpaceCombat;
        gc.BalanceConfig.enableSpaceCombat = enableCombat;

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
        yield return WaitUntil(() => !RunCountdown.IsCounting, SceneTimeoutSeconds, "run countdown");

        // Restore default so tests remain isolated.
        gc.BalanceConfig.enableSpaceCombat = originalCombatFlag;
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
