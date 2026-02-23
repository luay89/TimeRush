using UnityEngine;

/// <summary>
/// Static holder for run-time score data.
/// </summary>
public static class GameState
{
    private const string HighScoreKey = "HIGH_SCORE";

    public static int Score { get; private set; }
    public static int HighScore { get; private set; }

    static GameState()
    {
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        Score = 0;
    }

    /// <summary>
    /// Clears the current run score.
    /// </summary>
    public static void ResetRun()
    {
        Score = 0;
    }

    /// <summary>
    /// Adds score and persists the high score when beaten.
    /// </summary>
    public static void AddScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Score += amount;

        if (Score > HighScore)
        {
            HighScore = Score;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
        }
    }
}