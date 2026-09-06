using NUnit.Framework;

public sealed class ProgressionModelTests
{
    [Test]
    public void Empty_HasZeroTotals()
    {
        var model = ProgressionModel.Empty;

        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
        Assert.That(model.LastRunCounted, Is.False);
    }

    [Test]
    public void RecordRun_IncrementsRunsAndAddsScore()
    {
        var model = ProgressionModel.Empty.RecordRun(120);

        Assert.That(model.TotalRuns, Is.EqualTo(1));
        Assert.That(model.LifetimeScore, Is.EqualTo(120));
        Assert.That(model.LastRunScore, Is.EqualTo(120));
        Assert.That(model.LastRunCounted, Is.True);
    }

    [Test]
    public void RecordRun_AccumulatesAcrossMultipleRuns()
    {
        var model = ProgressionModel.Empty
            .RecordRun(100)
            .RecordRun(250)
            .RecordRun(50);

        Assert.That(model.TotalRuns, Is.EqualTo(3));
        Assert.That(model.LifetimeScore, Is.EqualTo(400));
    }

    [Test]
    public void RecordRun_ClampsNegativeScoreToZero()
    {
        var model = ProgressionModel.Empty.RecordRun(-500);

        Assert.That(model.TotalRuns, Is.EqualTo(1));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
        Assert.That(model.LastRunScore, Is.EqualTo(0));
    }

    [Test]
    public void RollbackLastRun_ReversesTheLastRecord()
    {
        var model = ProgressionModel.Empty
            .RecordRun(100)
            .RollbackLastRun();

        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
        Assert.That(model.LastRunCounted, Is.False);
    }

    [Test]
    public void ContinuedRun_CountsOnceWithFinalScore()
    {
        // First death records the run, the continue rolls it back,
        // then the final death recounts the same logical run with its full score.
        var model = ProgressionModel.Empty
            .RecordRun(100)   // died at 100
            .RollbackLastRun() // player continued
            .RecordRun(250);   // died again at 250 (score preserved across continue)

        Assert.That(model.TotalRuns, Is.EqualTo(1));
        Assert.That(model.LifetimeScore, Is.EqualTo(250));
    }

    [Test]
    public void RollbackLastRun_IsNoOpWhenNothingPending()
    {
        var model = ProgressionModel.Empty.RollbackLastRun();

        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
        Assert.That(model.LastRunCounted, Is.False);
    }

    [Test]
    public void DoubleRollback_DoesNotUndercount()
    {
        var model = ProgressionModel.Empty
            .RecordRun(300)
            .RollbackLastRun()
            .RollbackLastRun();

        Assert.That(model.TotalRuns, Is.EqualTo(0));
        Assert.That(model.LifetimeScore, Is.EqualTo(0));
    }
}
