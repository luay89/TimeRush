using System;

/// <summary>
/// Converts persisted run data into player-facing Results copy without coupling display policy to scene construction.
/// </summary>
public static class ResultsPresentation
{
    public readonly struct DisplayData
    {
        public readonly string FinalScoreText;
        public readonly string BestScoreText;
        public readonly string StatusText;

        public DisplayData(string finalScoreText, string bestScoreText, string statusText)
        {
            FinalScoreText = finalScoreText;
            BestScoreText = bestScoreText;
            StatusText = statusText;
        }
    }

    public static DisplayData Build(int finalScore, int bestScore, bool setNewBest, RunLossReason lossReason)
    {
        int clampedFinalScore = Math.Max(0, finalScore);
        int displayedBestScore = Math.Max(Math.Max(0, bestScore), clampedFinalScore);
        string reason = lossReason == RunLossReason.ObstacleCollision ? "IMPACT DETECTED" : "RUN ENDED";
        string status = setNewBest ? $"NEW BEST  //  {reason}" : reason;

        return new DisplayData($"Score: {clampedFinalScore}", $"Best: {displayedBestScore}", status);
    }
}
