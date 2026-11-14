using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the high-level combat mission loop inside the Game scene: enables 3D combat controls,
/// spawns enemies in waves, and decides when a stage mission is cleared.
/// </summary>
public sealed class CombatMissionController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private bool disableLegacyRunnerSystems = true;

    private GameController gameController;
    private GameBalanceConfig balance;
    private CombatWaveDirector waveDirector;
    private CombatPlayerController playerController;
    private readonly List<Behaviour> disabledLegacy = new List<Behaviour>(32);

    private void Awake()
    {
        gameController = GameController.Instance ? GameController.Instance : FindObjectOfType<GameController>();
        balance = gameController ? gameController.BalanceConfig : null;

        if (!balance || !balance.enableSpaceCombat)
        {
            enabled = false;
            return;
        }

        WirePlayerShip();
        if (disableLegacyRunnerSystems)
        {
            DisableLegacyRunner();
        }

        waveDirector = gameObject.AddComponent<CombatWaveDirector>();
        waveDirector.Configure(balance);
        waveDirector.SetPlayer(playerController);
        gameObject.AddComponent<CombatHudController>();
    }

    private void Update()
    {
        if (!gameController || gameController.IsGameOver)
        {
            return;
        }

        if (CombatGameModeSession.Mode == CombatGameMode.BossBattle)
        {
            if (waveDirector && waveDirector.IsBossDefeated && waveDirector.ActiveEnemyCount == 0)
            {
                gameController.RequestStageClear();
            }

            return;
        }

        if (!StageSession.IsStageRun)
        {
            return;
        }

        if (!gameController.StageGoalReached)
        {
            return;
        }

        // Stage goal time reached: in combat mode we also require the current threat to be cleared.
        if (CombatCampaignSession.IsActive)
        {
            if (CombatCampaignSession.ObjectiveComplete && (!waveDirector || waveDirector.ActiveEnemyCount == 0))
            {
                gameController.RequestStageClear();
            }

            return;
        }

        if (waveDirector && waveDirector.IsBossDefeated && waveDirector.ActiveEnemyCount == 0)
        {
            gameController.RequestStageClear();
        }
    }

    private void WirePlayerShip()
    {
        var legacyPlayer = FindObjectOfType<PlayerController>();
        GameObject playerObject = legacyPlayer ? legacyPlayer.gameObject : GameObject.FindGameObjectWithTag("Player");
        if (!playerObject)
        {
            Debug.LogError("CombatMissionController: Unable to locate player ship in Game scene.");
            enabled = false;
            return;
        }

        // Team + survivability
        var team = EnsureComponent<CombatTeamMember>(playerObject);
        team.Team = CombatTeam.Player;

        var health = EnsureComponent<CombatShipHealth>(playerObject);
        health.ConfigureDefaults(CombatTeam.Player);
        EnsureComponent<CombatSpecialAbilityController>(playerObject);

        // Player combat movement + input
        playerController = EnsureComponent<CombatPlayerController>(playerObject);
        playerController.Configure(balance);

        // Weapons
        var weapons = EnsureComponent<CombatWeaponController>(playerObject);
        weapons.SetPlayer(playerController);

        var input = EnsureComponent<CombatPlayerInputSource>(playerObject);
        input.SetPlayer(playerController);
    }

    private void DisableLegacyRunner()
    {
        // Disable the old runner movement/shooting so we don't have two systems fighting over transforms.
        DisableBehaviour<PlayerController>();
        DisableBehaviour<PlayerInputSource>();
        DisableBehaviour<PlayerBlaster>();

        // Turn off obstacle/pickup generation.
        DisableBehaviour<ObstacleSpawner>();
        DisableBehaviour<PowerUpSpawner>();
        DisableBehaviour<EnemyGunnery>();

        // Existing obstacles already in the scene (or spawned before this boots) should not kill the player.
        foreach (var killOnHit in FindObjectsOfType<KillOnHit>())
        {
            killOnHit.enabled = false;
        }
    }

    private void DisableBehaviour<T>() where T : Behaviour
    {
        foreach (var item in FindObjectsOfType<T>())
        {
            if (!item || !item.enabled)
            {
                continue;
            }

            item.enabled = false;
            disabledLegacy.Add(item);
        }
    }

    private static T EnsureComponent<T>(GameObject host) where T : Component
    {
        if (host.TryGetComponent<T>(out var existing) && existing)
        {
            return existing;
        }

        return host.AddComponent<T>();
    }
}
