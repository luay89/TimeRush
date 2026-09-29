using UnityEngine;

/// <summary>
/// Tracks the player's best score for the current calendar day (local device date) only,
/// separate from the all-time personal best that GameController/CompetitionRecords already
/// track. Automatically resets the first time a score is submitted after the stored date has
/// passed. Purely a display/motivation stat -- never read by gameplay, difficulty, or fairness.
/// Backed by PlayerPrefs, same persistence pattern as PlayerWallet.
/// </summary>
public static class DailyBestScore
{
    private const string ScoreKey = "TimeRush.DailyBest.Score";
    private const string DateKey = "TimeRush.DailyBest.Date";

    public static int Score
    {
        get
        {
            EnsureCurrentDay();
            return PlayerPrefs.GetInt(ScoreKey, 0);
        }
    }

    /// <summary>Submits a finished run's score; returns true if it set a new daily best.</summary>
    public static bool Submit(int score)
    {
        EnsureCurrentDay();
        int current = PlayerPrefs.GetInt(ScoreKey, 0);

        if (score <= current)
        {
            return false;
        }

        PlayerPrefs.SetInt(ScoreKey, score);
        PlayerPrefs.Save();
        return true;
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
        PlayerPrefs.SetInt(ScoreKey, 0);
        PlayerPrefs.Save();
    }
}
