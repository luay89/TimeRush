using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode half of the rewarded-ad provider checks (no other provider exists in an edit-mode
/// scene). The "real provider wins over the mock" cases need a runtime component as the stand-in
/// provider and live in RewardedAdProviderSelectionPlayModeTests.
/// </summary>
public class RewardedAdProviderSelectionTests
{
    private GameObject resultsHost;
    private GameObject mockHost;
    private ResultsController controller;

    [SetUp]
    public void SetUp()
    {
        ScoreSnapshot.Clear();
        resultsHost = new GameObject("ResultsControllerTestHost");
        controller = resultsHost.AddComponent<ResultsController>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in new[] { resultsHost, mockHost })
        {
            if (go)
            {
                Object.DestroyImmediate(go);
            }
        }

        ScoreSnapshot.Clear();
    }

    [Test]
    public void Mock_IsUsedWhenItIsTheOnlyProvider()
    {
        mockHost = new GameObject("MockRewardedAdServiceHost");
        var mock = mockHost.AddComponent<MockRewardedAdService>();

        Assert.AreSame(mock, Resolve());
    }

    [Test]
    public void NoProvider_ResolvesToNull()
    {
        Assert.IsNull(Resolve());
    }

    [Test]
    public void AdsSettings_UseGoogleSampleUnitInEditor_AndProductionUnitIsEmptyOrValid()
    {
        Assert.AreEqual("ca-app-pub-3940256099942544/5224354917", AdsSettings.GoogleSampleRewardedAdUnitIdAndroid);
        Assert.AreEqual(AdsSettings.GoogleSampleRewardedAdUnitIdAndroid, AdsSettings.RewardedAdUnitId,
            "Editor/development builds must only ever request Google's sample (test) ads.");

        string production = AdsSettings.ProductionRewardedAdUnitIdAndroid;
        Assert.IsTrue(production.Length == 0 || Regex.IsMatch(production, @"^ca-app-pub-\d{16}/\d{10}$"),
            "The production rewarded ad unit must be empty or a well-formed AdMob ad unit id.");
        Assert.AreNotEqual(AdsSettings.GoogleSampleRewardedAdUnitIdAndroid, production,
            "Release builds must not ship Google's sample ad unit.");
    }

    private IRewardedAdService Resolve()
    {
        var method = typeof(ResultsController).GetMethod("ResolveRewardedAdService", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method, "ResultsController.ResolveRewardedAdService not found.");
        return (IRewardedAdService)method.Invoke(controller, null);
    }
}
