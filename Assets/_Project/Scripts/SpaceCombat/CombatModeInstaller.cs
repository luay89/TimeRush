using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boots the new 3D space-combat mission loop inside the existing Game scene.
///
/// This is installed at runtime so we can evolve combat systems without risky scene-YAML edits.
/// The legacy runner loop remains available by disabling GameBalanceConfig.enableSpaceCombat.
/// </summary>
public static class CombatModeInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (scene.name != SceneNames.Game)
        {
            return;
        }

        var gc = GameController.Instance ? GameController.Instance : Object.FindObjectOfType<GameController>();
        var config = gc ? gc.BalanceConfig : null;
        if (!config || !config.enableSpaceCombat)
        {
            return;
        }

        if (Object.FindObjectOfType<CombatMissionController>())
        {
            return;
        }

        var root = new GameObject("CombatMission");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<CombatMissionController>();
    }
}

