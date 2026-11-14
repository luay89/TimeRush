using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns enemy waves (multiple simultaneous enemies) and a boss at the end of a stage mission.
///
/// This is intentionally lightweight and runtime-driven so we can iterate quickly while keeping
/// the legacy runner systems intact behind a config toggle.
/// </summary>
public sealed class CombatWaveDirector : MonoBehaviour
{
    [Header("Pacing")]
    [SerializeField] private float firstWaveDelay = 1.2f;
    [SerializeField] private float waveInterval = 6.2f;
    [SerializeField] private int baseWaveSize = 3;
    [SerializeField] private int maxWaveSize = 8;

    private readonly List<CombatShipHealth> activeEnemies = new List<CombatShipHealth>(32);
    private CombatPlayerController player;
    private GameController gameController;
    private GameBalanceConfig balance;
    private float waveTimer;
    private bool bossSpawned;
    private bool bossDefeated;

    public int WaveNumber { get; private set; }

    public int ActiveEnemyCount
    {
        get
        {
            PruneDead();
            return activeEnemies.Count;
        }
    }

    public bool IsBossDefeated => bossDefeated;

    public void Configure(GameBalanceConfig config)
    {
        balance = config;
    }

    // Backwards-compatible entry point (older campaign prototype).
    public void Configure(Transform playerTransform)
    {
        if (!player && playerTransform)
        {
            player = playerTransform.GetComponent<CombatPlayerController>();
        }
    }

    public void SetPlayer(CombatPlayerController playerController)
    {
        player = playerController;
    }

    private void Awake()
    {
        gameController = GameController.Instance ? GameController.Instance : FindObjectOfType<GameController>();
        if (!balance && gameController)
        {
            balance = gameController.BalanceConfig;
        }

        waveTimer = firstWaveDelay;
    }

    private void Update()
    {
        if (!gameController || gameController.IsGameOver || !GameStateMachine.IsGameplayInputAllowed)
        {
            return;
        }

        if (!player)
        {
            player = FindObjectOfType<CombatPlayerController>();
            if (!player)
            {
                return;
            }
        }

        PruneDead();

        if (CombatGameModeSession.Mode == CombatGameMode.BossBattle)
        {
            if (!bossSpawned)
            {
                waveTimer -= Time.deltaTime;
                if (waveTimer <= 0f)
                {
                    SpawnBoss();
                }
            }

            return;
        }

        if (!bossSpawned)
        {
            TickWaveSpawns();
            TrySpawnBossGate();
        }
    }

    private void TickWaveSpawns()
    {
        waveTimer -= Time.deltaTime;
        if (waveTimer > 0f)
        {
            return;
        }

        // Don't overwhelm mobile: keep a cap on concurrent enemies.
        int desired = ResolveWaveSize();
        int budget = Mathf.Max(0, Mathf.Min(desired, maxWaveSize) - ActiveEnemyCount);
        if (budget <= 0)
        {
            waveTimer = Mathf.Max(1.5f, waveInterval * 0.6f);
            return;
        }

        SpawnWave(budget);
        waveTimer = waveInterval;
    }

    private int ResolveWaveSize()
    {
        int stage = StageSession.IsStageRun ? StageSession.Current.Number : 1;
        int size = baseWaveSize + Mathf.Clamp(stage - 1, 0, 10) / 2;
        if (CombatCampaignSession.IsActive)
        {
            size += Mathf.Clamp(CombatCampaignSession.MissionIndex, 0, 9) / 3;
        }

        return Mathf.Clamp(size, 2, maxWaveSize);
    }

    private void SpawnWave(int count)
    {
        if (count <= 0)
        {
            return;
        }

        WaveNumber++;
        int stage = StageSession.IsStageRun ? StageSession.Current.Number : 1;
        for (int i = 0; i < count; i++)
        {
            CombatEnemyArchetype type = ResolveArchetype(stage);
            SpawnEnemy(type);
        }
    }

    private CombatEnemyArchetype ResolveArchetype(int stage)
    {
        // Ensure variety: early missions skew to lighter units, later missions mix in elite/destroyers.
        float t = Mathf.InverseLerp(1f, 10f, stage);
        float roll = Random.value;
        if (roll < Mathf.Lerp(0.45f, 0.15f, t)) return CombatEnemyArchetype.FastInterceptor;
        if (roll < Mathf.Lerp(0.78f, 0.45f, t)) return CombatEnemyArchetype.HeavyFighter;
        if (roll < Mathf.Lerp(0.9f, 0.68f, t)) return CombatEnemyArchetype.LongRangeShooter;
        if (roll < Mathf.Lerp(0.98f, 0.88f, t)) return CombatEnemyArchetype.ArmoredDestroyer;
        return CombatEnemyArchetype.Elite;
    }

    private void SpawnEnemy(CombatEnemyArchetype archetype)
    {
        Vector3 p = player.transform.position;
        float radius = balance ? balance.combatPlayAreaRadius : 14f;
        float x = p.x + Random.Range(-radius * 0.75f, radius * 0.75f);
        float y = p.y + Random.Range(-radius * 0.35f, radius * 0.55f);
        float z = p.z + Random.Range(14f, 24f);

        var enemy = new GameObject("Enemy_" + archetype);
        enemy.transform.position = new Vector3(x, y, z);

        var team = enemy.AddComponent<CombatTeamMember>();
        team.Team = CombatTeam.Enemy;

        var health = enemy.AddComponent<CombatShipHealth>();
        health.ConfigureDefaults(CombatTeam.Enemy);
        ApplyArchetypeHealth(health, archetype);
        health.Died += HandleEnemyDied;
        activeEnemies.Add(health);

        enemy.AddComponent<CombatEnemy>();
        enemy.AddComponent<SphereCollider>().isTrigger = false;

        var brain = enemy.AddComponent<CombatEnemyBrain>();
        brain.Configure(archetype, player);

        CombatEnemyVisualFactory.Build(enemy, archetype);
    }

    private static void ApplyArchetypeHealth(CombatShipHealth health, CombatEnemyArchetype archetype)
    {
        // Tweaks on top of CombatShipHealth defaults.
        switch (archetype)
        {
            case CombatEnemyArchetype.FastInterceptor:
                health.SetMaxVitals(60f, 15f, true);
                break;
            case CombatEnemyArchetype.HeavyFighter:
                health.SetMaxVitals(95f, 25f, true);
                break;
            case CombatEnemyArchetype.LongRangeShooter:
                health.SetMaxVitals(75f, 18f, true);
                break;
            case CombatEnemyArchetype.ArmoredDestroyer:
                health.SetMaxVitals(140f, 40f, true);
                break;
            case CombatEnemyArchetype.Elite:
                health.SetMaxVitals(120f, 55f, true);
                break;
        }
    }

    private void TrySpawnBossGate()
    {
        if (!StageSession.IsStageRun)
        {
            return;
        }

        // Boss gating:
        // - In campaign: once the kill objective is met.
        // - In normal stage: once the stage timer finish line is reached.
        bool ready = CombatCampaignSession.IsActive ? CombatCampaignSession.ObjectiveComplete : gameController.StageGoalReached;
        if (!ready)
        {
            return;
        }

        // Let the current wave breathe a bit before boss spawns.
        if (ActiveEnemyCount > 2)
        {
            return;
        }

        SpawnBoss();
    }

    private void SpawnBoss()
    {
        bossSpawned = true;
        bossDefeated = false;

        Vector3 p = player.transform.position;
        float z = p.z + 28f;
        var boss = new GameObject("Boss_Leviathan");
        boss.transform.position = new Vector3(p.x, p.y + 1.2f, z);

        var team = boss.AddComponent<CombatTeamMember>();
        team.Team = CombatTeam.Enemy;

        var health = boss.AddComponent<CombatShipHealth>();
        health.ConfigureDefaults(CombatTeam.Enemy);
        health.SetMaxVitals(650f, 220f, true);

        health.Died += _ =>
        {
            bossDefeated = true;
            CombatGameModeSession.MarkBossBattleComplete();
            if (CombatGameModeSession.Mode == CombatGameMode.BossBattle && gameController && !gameController.IsGameOver)
            {
                gameController.RequestStageClear();
            }
        };

        activeEnemies.Add(health);
        boss.AddComponent<CombatEnemy>();

        var brain = boss.AddComponent<CombatBossController>();
        brain.Configure(player);

        CombatBossVisualFactory.Build(boss);

        CombatAnnouncer.Announce("BOSS INCOMING", "LEVIATHAN-CLASS DREADNOUGHT", new Color(1f, 0.35f, 0.2f, 1f), -1);
        GameFeedbackSignals.RaiseAmbientCameraJolt(1.2f);
    }

    private void HandleEnemyDied(CombatShipHealth who)
    {
        // Remove via prune path.
    }

    private void PruneDead()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            CombatShipHealth h = activeEnemies[i];
            if (!h || !h.IsAlive)
            {
                activeEnemies.RemoveAt(i);
            }
        }
    }
}
