using UnityEngine;

/// <summary>
/// Three lightweight daily missions, regenerated deterministically from the calendar date so
/// every player on the same day gets the same targets without any server. Progress is the best
/// value seen across all runs that day (never decreases), and each mission pays its coin reward
/// exactly once, the first time it's completed that day. Purely a motivation/reward layer --
/// never read by gameplay, difficulty, or fairness, and coins are the same cosmetic currency
/// PlayerWallet already grants for finishing a run.
/// </summary>
public static class DailyMissions
{
    private const int MissionCount = 3;
    private const string DateKey = "TimeRush.Missions.Date";
    private const string ProgressKeyPrefix = "TimeRush.Missions.Progress";
    private const string ClaimedKeyPrefix = "TimeRush.Missions.Claimed";

    public readonly struct MissionStatus
    {
        public readonly string Title;
        public readonly int Progress;
        public readonly int Target;
        public readonly int RewardCoins;
        public readonly bool Completed;

        public MissionStatus(string title, int progress, int target, int rewardCoins, bool completed)
        {
            Title = title;
            Progress = progress;
            Target = target;
            RewardCoins = rewardCoins;
            Completed = completed;
        }
    }

    private readonly struct MissionDefinition
    {
        public readonly string Title;
        public readonly int Target;
        public readonly int RewardCoins;

        public MissionDefinition(string title, int target, int rewardCoins)
        {
            Title = title;
            Target = target;
            RewardCoins = rewardCoins;
        }
    }

    /// <summary>Call once per finished run (e.g. from GameController.TriggerGameOverInternal).</summary>
    public static void ReportRun(int score, float aliveTimeSeconds, bool finishedWithoutContinue)
    {
        EnsureCurrentDay();
        MissionDefinition[] definitions = BuildDefinitionsForToday();
        int survivalSeconds = Mathf.FloorToInt(Mathf.Max(0f, aliveTimeSeconds));

        UpdateMission(0, definitions[0], Mathf.Max(0, score));
        UpdateMission(1, definitions[1], survivalSeconds);
        UpdateMission(2, definitions[2], finishedWithoutContinue ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static MissionStatus[] GetStatuses()
    {
        EnsureCurrentDay();
        MissionDefinition[] definitions = BuildDefinitionsForToday();
        var statuses = new MissionStatus[MissionCount];

        for (int i = 0; i < MissionCount; i++)
        {
            int progress = PlayerPrefs.GetInt(ProgressKey(i), 0);
            bool completed = progress >= definitions[i].Target;
            int displayedProgress = Mathf.Min(progress, definitions[i].Target);
            statuses[i] = new MissionStatus(definitions[i].Title, displayedProgress, definitions[i].Target, definitions[i].RewardCoins, completed);
        }

        return statuses;
    }

    private static void UpdateMission(int index, MissionDefinition definition, int rawValue)
    {
        string progressKey = ProgressKey(index);
        string claimedKey = ClaimedKey(index);

        int currentProgress = PlayerPrefs.GetInt(progressKey, 0);
        int newProgress = Mathf.Max(currentProgress, rawValue);

        if (newProgress != currentProgress)
        {
            PlayerPrefs.SetInt(progressKey, newProgress);
        }

        bool alreadyClaimed = PlayerPrefs.GetInt(claimedKey, 0) == 1;

        if (!alreadyClaimed && newProgress >= definition.Target)
        {
            PlayerPrefs.SetInt(claimedKey, 1);
            PlayerWallet.Earn(definition.RewardCoins);
        }
    }

    // Deterministic per-day targets: every player sees the same three missions on the same date,
    // with no network call and no save-file state beyond the date/progress/claimed keys above.
    private static MissionDefinition[] BuildDefinitionsForToday()
    {
        int seed = System.DateTime.Now.Year * 1000 + System.DateTime.Now.DayOfYear;
        var rng = new System.Random(seed);

        int scoreTarget = 150 + rng.Next(0, 6) * 50; // 150..400
        int survivalTarget = 20 + rng.Next(0, 6) * 5; // 20..45 seconds

        return new[]
        {
            new MissionDefinition($"Score {scoreTarget}+ in one run", scoreTarget, 30),
            new MissionDefinition($"Survive {survivalTarget}s in one run", survivalTarget, 40),
            new MissionDefinition("Finish a run without Continue", 1, 50),
        };
    }

    private static void EnsureCurrentDay()
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        string stored = PlayerPrefs.GetString(DateKey, string.Empty);

        if (stored == today)
        {
            return;
        }

        PlayerPrefs.SetString(DateKey, today);

        for (int i = 0; i < MissionCount; i++)
        {
            PlayerPrefs.SetInt(ProgressKey(i), 0);
            PlayerPrefs.SetInt(ClaimedKey(i), 0);
        }

        PlayerPrefs.Save();
    }

    private static string ProgressKey(int index) => ProgressKeyPrefix + index;
    private static string ClaimedKey(int index) => ClaimedKeyPrefix + index;
}
