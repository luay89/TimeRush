using NUnit.Framework;

/// <summary>
/// Deterministic, scene-free tests for the pure <see cref="CompetitionRecords"/> personal-record
/// model. Covers new-best detection and the strict-greater rule that distinguishes a genuine new
/// record from an equal or lower score.
/// </summary>
public sealed class CompetitionRecordsTests
{
    // A — New personal best is detected.
    [Test]
    public void Register_HigherScore_SetsNewRecord()
    {
        CompetitionRecords records = new CompetitionRecords(100);

        CompetitionRecords.Submission result = records.Register(250);

        Assert.That(result.IsNewRecord, Is.True);
        Assert.That(result.Records.HighestScore, Is.EqualTo(250));
    }

    [Test]
    public void Register_FromEmpty_DetectsFirstScoreAsRecord()
    {
        CompetitionRecords.Submission result = CompetitionRecords.Empty.Register(10);

        Assert.That(result.IsNewRecord, Is.True);
        Assert.That(result.Records.HighestScore, Is.EqualTo(10));
    }

    // B — Lower score does not replace personal best.
    [Test]
    public void Register_LowerScore_KeepsExistingBestAndReportsNoRecord()
    {
        CompetitionRecords records = new CompetitionRecords(500);

        CompetitionRecords.Submission result = records.Register(499);

        Assert.That(result.IsNewRecord, Is.False);
        Assert.That(result.Records.HighestScore, Is.EqualTo(500));
    }

    // C — Equal score does not incorrectly report a new record.
    [Test]
    public void Register_EqualScore_DoesNotReportNewRecord()
    {
        CompetitionRecords records = new CompetitionRecords(500);

        CompetitionRecords.Submission result = records.Register(500);

        Assert.That(result.IsNewRecord, Is.False);
        Assert.That(result.Records.HighestScore, Is.EqualTo(500));
    }

    [Test]
    public void Register_NegativeScore_IsClampedAndNeverRecordFromZero()
    {
        CompetitionRecords.Submission result = CompetitionRecords.Empty.Register(-42);

        Assert.That(result.IsNewRecord, Is.False);
        Assert.That(result.Records.HighestScore, Is.EqualTo(0));
    }

    [Test]
    public void Records_ClampNegativeHighestScoreToZero()
    {
        CompetitionRecords records = new CompetitionRecords(-7);

        Assert.That(records.HighestScore, Is.EqualTo(0));
    }
}
