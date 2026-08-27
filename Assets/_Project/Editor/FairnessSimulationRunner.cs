#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs reproducible fairness samples from the Unity Editor without adding runtime UI.
/// </summary>
public static class FairnessSimulationRunner
{
    private const string BalancePath = "Assets/_Project/Config/GameBalanceConfig.asset";
    private const string LayoutPath = "Assets/_Project/Config/TrackLayoutConfig.asset";
    private const int ScenarioCount = 10000;
    private const uint Seed = 424242u;

    [MenuItem("TimeRush/Validation/Run Fairness Simulation")]
    public static void Run()
    {
        GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
        TrackLayoutConfig layout = AssetDatabase.LoadAssetAtPath<TrackLayoutConfig>(LayoutPath);

        if (!balance || !layout)
        {
            Debug.LogError("FairnessSimulationRunner: Required TimeRush config assets are missing.");
            return;
        }

        RunBand("Early", 0f, balance, layout);
        RunBand("Medium", 60f, balance, layout);
        RunBand("High", 120f, balance, layout);
    }

    private static void RunBand(string label, float aliveTime, GameBalanceConfig balance, TrackLayoutConfig layout)
    {
        FairnessSimulationResult result = new FairnessSimulation().Run(balance, layout, Seed, ScenarioCount, aliveTime);
        Debug.Log($"[FairnessSimulation:{label}] seed={Seed} scenarios={result.Scenarios} accepted={result.Accepted} rejected={result.Rejected} failures={result.Failures} lanes=L{result.LeftChoices}/C{result.CenterChoices}/R{result.RightChoices}");
    }
}
#endif
