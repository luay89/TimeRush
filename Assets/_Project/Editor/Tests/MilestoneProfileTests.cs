using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Persistence tests for <see cref="MilestoneProfile"/> over real PlayerPrefs. The milestone bitmask
/// lives under its own key and is snapshotted in setup / restored in teardown so the player's live
/// milestone progress is never clobbered. Covers Continue single-counting (G) and reload persistence
/// (H) for the progression-depth phase.
/// </summary>
public sealed class MilestoneProfileTests
{
    private const string MilestoneMaskKey = "TIMERUSH_MILESTONES";

    private bool hadMask;
    private int savedMask;

    [SetUp]
    public void SetUp()
    {
        hadMask = PlayerPrefs.HasKey(MilestoneMaskKey);
        savedMask = PlayerPrefs.GetInt(MilestoneMaskKey, 0);
        PlayerPrefs.DeleteKey(MilestoneMaskKey);
        PlayerPrefs.Save();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(MilestoneMaskKey);
        if (hadMask)
        {
            PlayerPrefs.SetInt(MilestoneMaskKey, savedMask);
        }

        PlayerPrefs.Save();
    }

    // G: Results is shown twice for one continued logical run (the intermediate death before Continue
    // and the final death). Evaluating the same facts twice must unlock a milestone only once.
    [Test]
    public void G_Continue_DoesNotDoubleCount()
    {
        MilestoneFacts facts = new MilestoneFacts(totalRuns: 1, bestScore: 1000, rankIndex: 1, setNewBest: true);

        MilestoneState.Evaluation firstVisit = MilestoneProfile.EvaluateAndPersist(facts);
        Assert.IsTrue(firstVisit.HasNewUnlocks);
        int countAfterFirst = MilestoneProfile.UnlockedCount;

        MilestoneState.Evaluation secondVisit = MilestoneProfile.EvaluateAndPersist(facts);
        Assert.IsFalse(secondVisit.HasNewUnlocks, "Second Results visit of one run must not re-unlock.");
        Assert.AreEqual(countAfterFirst, MilestoneProfile.UnlockedCount);
    }

    // H: Unlocked milestones persist across a reload (simulated by reading the store fresh).
    [Test]
    public void H_Reload_PreservesUnlocks()
    {
        MilestoneFacts facts = new MilestoneFacts(totalRuns: 4, bestScore: 5000, rankIndex: 2, setNewBest: false);
        MilestoneProfile.EvaluateAndPersist(facts);

        Assert.IsTrue(MilestoneProfile.IsUnlocked(MilestoneId.Break1000));
        Assert.IsTrue(MilestoneProfile.IsUnlocked(MilestoneId.Break5000));
        Assert.IsTrue(MilestoneProfile.IsUnlocked(MilestoneId.ReachDodger));

        // Load() reads straight from PlayerPrefs, mirroring a fresh session after a restart.
        MilestoneState reloaded = MilestoneProfile.Load();
        Assert.IsTrue(reloaded.IsUnlocked(MilestoneId.Break5000));
        Assert.AreEqual(MilestoneProfile.UnlockedCount, reloaded.UnlockedCount);
    }
}
