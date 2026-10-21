using NUnit.Framework;

/// <summary>
/// Pure tests for Stage mode: the catalogue gets harder and longer stage by stage, stars follow
/// clear + score, and stages unlock strictly in order while best stars never go down.
/// </summary>
public sealed class StageModeTests
{
    [Test]
    public void Catalog_HasTenStages_EachLongerAndHarderThanTheLast()
    {
        Assert.AreEqual(10, StageCatalog.Count);
        for (int i = 0; i < StageCatalog.Count; i++)
        {
            StageCatalog.StageDefinition stage = StageCatalog.Stages[i];
            Assert.AreEqual(i + 1, stage.Number);
            Assert.IsFalse(string.IsNullOrEmpty(stage.Name));
            Assert.Greater(stage.ThreeStarScore, stage.TwoStarScore);
            Assert.Greater(stage.TwoStarScore, (int)(stage.GoalSeconds * 10f));
            if (i > 0)
            {
                Assert.Greater(stage.GoalSeconds, StageCatalog.Stages[i - 1].GoalSeconds);
                Assert.Greater(stage.DifficultyHeadStart, StageCatalog.Stages[i - 1].DifficultyHeadStart);
            }
        }

        Assert.AreEqual(0f, StageCatalog.Stages[0].DifficultyHeadStart);
    }

    [Test]
    public void Stars_FollowClearAndScore()
    {
        StageCatalog.StageDefinition stage = StageCatalog.Stages[0];
        Assert.AreEqual(0, StageCatalog.StarsFor(stage, false, 99999));
        Assert.AreEqual(1, StageCatalog.StarsFor(stage, true, 0));
        Assert.AreEqual(1, StageCatalog.StarsFor(stage, true, stage.TwoStarScore - 1));
        Assert.AreEqual(2, StageCatalog.StarsFor(stage, true, stage.TwoStarScore));
        Assert.AreEqual(3, StageCatalog.StarsFor(stage, true, stage.ThreeStarScore));
    }

    [Test]
    public void Progress_UnlocksInOrder_AndKeepsBestStars()
    {
        var model = new StageProgressModel(new int[0]);
        Assert.IsTrue(model.IsUnlocked(0));
        Assert.IsFalse(model.IsUnlocked(1));
        Assert.IsFalse(model.IsUnlocked(-1));
        Assert.IsFalse(model.IsUnlocked(StageCatalog.Count));

        Assert.IsFalse(model.Record(0, 0), "A failed attempt stores nothing.");
        Assert.IsFalse(model.IsUnlocked(1));

        Assert.IsTrue(model.Record(0, 2));
        Assert.IsTrue(model.IsUnlocked(1));
        Assert.IsFalse(model.IsUnlocked(2));

        Assert.IsFalse(model.Record(0, 1), "A worse result never lowers the best.");
        Assert.AreEqual(2, model.GetStars(0));
        Assert.IsTrue(model.Record(0, 7));
        Assert.AreEqual(StageCatalog.MaxStars, model.GetStars(0), "Stars are clamped to the maximum.");

        Assert.AreEqual(3, model.TotalStars);
        Assert.AreEqual(StageCatalog.Count * StageCatalog.MaxStars, model.MaxTotalStars);
    }

    [Test]
    public void Progress_SanitisesStoredValues()
    {
        var model = new StageProgressModel(new[] { -4, 9, 1 });
        Assert.AreEqual(0, model.GetStars(0));
        Assert.AreEqual(StageCatalog.MaxStars, model.GetStars(1));
        Assert.AreEqual(1, model.GetStars(2));
    }
}
