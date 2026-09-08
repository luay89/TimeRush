/// <summary>
/// Pure, immutable personal-records state for local competition. Only reliably
/// measurable outcomes are tracked; currently the single value that survives every
/// gameplay path (including a Continue, which resets the run's alive time and
/// difficulty) is the highest score achieved. Keeping this a struct lets the
/// new-record rules be exercised without Unity scenes or PlayerPrefs.
/// </summary>
public readonly struct CompetitionRecords
{
    /// <summary>Best single-run score the player has ever reached.</summary>
    public int HighestScore { get; }

    public CompetitionRecords(int highestScore)
    {
        HighestScore = highestScore < 0 ? 0 : highestScore;
    }

    public static CompetitionRecords Empty => new CompetitionRecords(0);

    /// <summary>Result of registering a run: the (possibly updated) records and whether a new record was set.</summary>
    public readonly struct Submission
    {
        public readonly CompetitionRecords Records;
        public readonly bool IsNewRecord;

        public Submission(CompetitionRecords records, bool isNewRecord)
        {
            Records = records;
            IsNewRecord = isNewRecord;
        }
    }

    /// <summary>
    /// Registers a completed run's score. A strictly greater score becomes the new
    /// highest and reports a new record; an equal or lower score leaves the records
    /// untouched and reports no new record.
    /// </summary>
    public Submission Register(int score)
    {
        int safeScore = score < 0 ? 0 : score;

        if (safeScore > HighestScore)
        {
            return new Submission(new CompetitionRecords(safeScore), true);
        }

        return new Submission(this, false);
    }
}
