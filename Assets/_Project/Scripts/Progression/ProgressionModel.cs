/// <summary>
/// Pure, immutable persistent-progression state so accumulation rules can be
/// tested without loading Unity scenes or touching PlayerPrefs.
/// A single logical run is recorded exactly once; a continued run rolls back the
/// intermediate record so the final death recounts it with the full score.
/// </summary>
public readonly struct ProgressionModel
{
    public int TotalRuns { get; }
    public long LifetimeScore { get; }

    /// <summary>Score of the most recently recorded run, kept so a continue can undo it.</summary>
    public int LastRunScore { get; }

    /// <summary>True when a recorded run is still pending and may be rolled back by a continue.</summary>
    public bool LastRunCounted { get; }

    public ProgressionModel(int totalRuns, long lifetimeScore, int lastRunScore, bool lastRunCounted)
    {
        TotalRuns = totalRuns < 0 ? 0 : totalRuns;
        LifetimeScore = lifetimeScore < 0 ? 0 : lifetimeScore;
        LastRunScore = lastRunScore < 0 ? 0 : lastRunScore;
        LastRunCounted = lastRunCounted;
    }

    public static ProgressionModel Empty => new ProgressionModel(0, 0, 0, false);

    /// <summary>Counts a completed run and adds its score to the lifetime total.</summary>
    public ProgressionModel RecordRun(int runScore)
    {
        int safeScore = runScore < 0 ? 0 : runScore;
        return new ProgressionModel(TotalRuns + 1, LifetimeScore + safeScore, safeScore, true);
    }

    /// <summary>
    /// Reverses the last recorded run so a continued run is not double counted.
    /// No-op when there is nothing pending to roll back.
    /// </summary>
    public ProgressionModel RollbackLastRun()
    {
        if (!LastRunCounted)
        {
            return this;
        }

        return new ProgressionModel(TotalRuns - 1, LifetimeScore - LastRunScore, 0, false);
    }
}
