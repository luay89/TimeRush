using System;
using System.Collections.Generic;

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

    public static DisplayData Build(int finalScore, int bestScore, bool setNewBest, RunLossReason lossReason, int dailyBestScore = 0)
    {
        int clampedFinalScore = Math.Max(0, finalScore);
        int displayedBestScore = Math.Max(Math.Max(0, bestScore), clampedFinalScore);
        // "Best Today" always at least matches the run just finished, same as the all-time best,
        // so it never briefly shows a stale (too-low) number after a personal-best run.
        int displayedDailyBest = Math.Max(Math.Max(0, dailyBestScore), clampedFinalScore);
        string reason = lossReason == RunLossReason.ObstacleCollision ? "IMPACT DETECTED" : "RUN ENDED";
        string status = setNewBest ? $"NEW BEST  //  {reason}" : reason;

        return new DisplayData($"Score: {clampedFinalScore}", $"Best: {displayedBestScore}   //   Today: {displayedDailyBest}", status);
    }

    /// <summary>
    /// Builds the additive cross-run progression line shown on Results from the persisted
    /// <see cref="ProgressionModel"/> and the resolved rank. Pure so it can be tested without
    /// scenes; it only reads the supplied progression values and never mutates persistence.
    /// </summary>
    public static string BuildProgressionSummary(ProgressionModel progression, ProgressionConfig.RankResult rank)
    {
        int runs = progression.TotalRuns < 0 ? 0 : progression.TotalRuns;
        long lifetime = progression.LifetimeScore < 0 ? 0 : progression.LifetimeScore;
        string rankName = string.IsNullOrEmpty(rank.name) ? "—" : rank.name;

        return $"RANK {rankName}   //   RUNS {runs}   //   LIFETIME {lifetime}";
    }

    /// <summary>
    /// Builds the next-rank progression line from a pure <see cref="RankProgressInfo"/>. The top rank
    /// is shown cleanly as MAX RANK; otherwise it shows the next rank, points remaining, and percent.
    /// </summary>
    public static string BuildRankProgress(RankProgressInfo info)
    {
        if (info.IsMaxRank)
        {
            string name = string.IsNullOrEmpty(info.CurrentRankName) ? "—" : info.CurrentRankName;
            return $"RANK {name}   //   MAX RANK";
        }

        int percent = (int)Math.Round(Math.Max(0f, Math.Min(1f, info.Progress01)) * 100.0);
        string nextName = string.IsNullOrEmpty(info.NextRankName) ? "—" : info.NextRankName;
        return $"NEXT {nextName}   //   {info.PointsToNext} PTS TO GO   //   {percent}%";
    }

    /// <summary>
    /// Builds the milestone callout for any milestones unlocked on this Results visit. Returns an empty
    /// string when nothing new was unlocked so the caller can hide the line.
    /// </summary>
    public static string BuildMilestoneCallout(IReadOnlyList<string> newlyUnlockedTitles)
    {
        if (newlyUnlockedTitles == null || newlyUnlockedTitles.Count == 0)
        {
            return string.Empty;
        }

        if (newlyUnlockedTitles.Count == 1)
        {
            return $"MILESTONE   //   {newlyUnlockedTitles[0]}";
        }

        return $"MILESTONES   //   {string.Join("   •   ", newlyUnlockedTitles)}";
    }

    /// <summary>
    /// Builds a concise, reliable next-run target from data already tracked by the game.
    /// It never invents metrics: only next-rank distance and personal-best distance are used.
    /// </summary>
    public static string BuildNextRunGuidance(int finalScore, int bestScore, RankProgressInfo rankInfo)
    {
        int safeFinal = Math.Max(0, finalScore);
        int safeBest = Math.Max(Math.Max(0, bestScore), safeFinal);
        int pointsToBest = safeFinal >= safeBest ? 1 : (safeBest - safeFinal + 1);

        if (!rankInfo.IsMaxRank && rankInfo.PointsToNext > 0)
        {
            string nextName = string.IsNullOrEmpty(rankInfo.NextRankName) ? "—" : rankInfo.NextRankName;

            if (safeFinal < safeBest)
            {
                return $"NEXT RUN   //   +{rankInfo.PointsToNext} TO {nextName}   •   +{pointsToBest} TO NEW BEST";
            }

            return $"NEXT RUN   //   +{rankInfo.PointsToNext} TO {nextName}";
        }

        if (safeFinal < safeBest)
        {
            return $"NEXT RUN   //   +{pointsToBest} TO NEW BEST";
        }

        return $"NEXT RUN   //   BEAT BEST {safeBest + 1}";
    }

    /// <summary>
    /// Formats the compact three-line daily-missions readout from DailyMissions.GetStatuses().
    /// Pure formatting only -- all progress/reward state lives in DailyMissions.
    /// </summary>
    public static string BuildDailyMissionsSummary(DailyMissions.MissionStatus[] statuses)
    {
        if (statuses == null || statuses.Length == 0)
        {
            return string.Empty;
        }

        var lines = new string[statuses.Length + 1];
        lines[0] = "DAILY MISSIONS";

        for (int i = 0; i < statuses.Length; i++)
        {
            DailyMissions.MissionStatus status = statuses[i];
            // Plain ASCII only -- see ShipSkinCatalog's fix for why: LiberationSans SDF has no
            // glyph for characters like a checkmark and would spam a per-frame console warning.
            string mark = status.Completed ? "DONE" : $"{status.Progress}/{status.Target}";
            lines[i + 1] = $"{mark}   {status.Title}   (+{status.RewardCoins})";
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Formats brief challenge-transition HUD copy from the existing challenge state signal.
    /// </summary>
    public static string BuildChallengeStatus(ChallengeState state)
    {
        switch (state)
        {
            case ChallengeState.Pressure:
                return "CHALLENGE  //  PRESSURE";
            case ChallengeState.Recovery:
                return "CHALLENGE  //  RECOVERY";
            default:
                return string.Empty;
        }
    }
}
