using System.Globalization;
using UnityEngine;

/// <summary>
/// PlayerPrefs-backed facade over <see cref="ProgressionModel"/>. Mirrors the
/// existing preference-store pattern (TIMERUSH_* keys) and owns the only
/// persistence path for cross-run progression totals.
/// </summary>
public static class ProgressionProfile
{
    private const string TotalRunsKey = "TIMERUSH_TOTAL_RUNS";
    private const string LifetimeScoreKey = "TIMERUSH_LIFETIME_SCORE";
    private const string LastRunScoreKey = "TIMERUSH_LAST_RUN_SCORE";
    private const string LastRunCountedKey = "TIMERUSH_LAST_RUN_COUNTED";

    public static int TotalRuns => Load().TotalRuns;
    public static long LifetimeScore => Load().LifetimeScore;

    public static ProgressionModel Load()
    {
        int runs = PlayerPrefs.GetInt(TotalRunsKey, 0);
        long lifetime = ReadLong(LifetimeScoreKey);
        int lastRun = PlayerPrefs.GetInt(LastRunScoreKey, 0);
        bool counted = PlayerPrefs.GetInt(LastRunCountedKey, 0) == 1;
        return new ProgressionModel(runs, lifetime, lastRun, counted);
    }

    /// <summary>Records a completed run into the persisted lifetime totals.</summary>
    public static void RecordRun(int runScore)
    {
        Save(Load().RecordRun(runScore));
    }

    /// <summary>Undoes the last recorded run so a continued run is counted only once.</summary>
    public static void RollbackLastRun()
    {
        Save(Load().RollbackLastRun());
    }

    private static void Save(ProgressionModel model)
    {
        PlayerPrefs.SetInt(TotalRunsKey, model.TotalRuns);
        WriteLong(LifetimeScoreKey, model.LifetimeScore);
        PlayerPrefs.SetInt(LastRunScoreKey, model.LastRunScore);
        PlayerPrefs.SetInt(LastRunCountedKey, model.LastRunCounted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // PlayerPrefs has no long overload; lifetime score is stored as an invariant string.
    private static long ReadLong(string key)
    {
        string raw = PlayerPrefs.GetString(key, "0");
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) && value >= 0
            ? value
            : 0;
    }

    private static void WriteLong(string key, long value)
    {
        PlayerPrefs.SetString(key, value.ToString(CultureInfo.InvariantCulture));
    }
}
