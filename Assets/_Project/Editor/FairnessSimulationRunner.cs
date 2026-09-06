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
    private const string PatternSetPath = "Assets/_Project/Resources/ObstaclePatternSet.asset";
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

    [MenuItem("TimeRush/Validation/Run Pattern Simulation")]
    public static void RunPatterns()
    {
        GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
        TrackLayoutConfig layout = AssetDatabase.LoadAssetAtPath<TrackLayoutConfig>(LayoutPath);

        if (!balance || !layout)
        {
            Debug.LogError("FairnessSimulationRunner: Required TimeRush config assets are missing.");
            return;
        }

        ObstaclePatternSet set = AssetDatabase.LoadAssetAtPath<ObstaclePatternSet>(PatternSetPath);
        if (!set)
        {
            set = ObstaclePatternSet.CreateDefault();
        }

        RunPatternBand("Early", 0f, set, balance, layout);
        RunPatternBand("Medium", 60f, set, balance, layout);
        RunPatternBand("High", 120f, set, balance, layout);
    }

    private static void RunPatternBand(string label, float aliveTime, ObstaclePatternSet set, GameBalanceConfig balance, TrackLayoutConfig layout)
    {
        PatternSimulationResult result = new PatternSimulation().Run(set, balance, layout, Seed, ScenarioCount, aliveTime);
        Debug.Log($"[PatternSimulation:{label}] seed={Seed} beats={result.Beats} spawned={result.ObstaclesSpawned} groupsAccepted={result.GroupsAccepted} groupsRejected={result.GroupsRejected} failures={result.Failures} families=Sgl{result.SingleCount}/Alt{result.AlternatingCount}/Dbl{result.DoubleLaneBlockCount}/Stg{result.StaggeredCount}/Dpt{result.DepthLaneComboCount} valid={result.IsValid}");
    }
}
#endif
