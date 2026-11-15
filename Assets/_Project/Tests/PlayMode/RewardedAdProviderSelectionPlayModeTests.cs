using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A real rewarded-ad provider must win over MockRewardedAdService, which the Boot scene (and a
/// development fallback) always provide. Runs in PlayMode because the stand-in provider has to be
/// a runtime component. All hosts are created inactive so no Awake/Start game logic runs; the
/// lookup includes inactive objects. Other mocks left over from earlier PlayMode tests may exist,
/// which is exactly the situation the preference rule has to handle.
/// </summary>
public class RewardedAdProviderSelectionPlayModeTests
{
    private GameObject resultsHost;
    private GameObject mockHost;
    private GameObject realHost;
    private ResultsController controller;

    [SetUp]
    public void SetUp()
    {
        resultsHost = CreateInactive("ResultsControllerTestHost");
        controller = resultsHost.AddComponent<ResultsController>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in new[] { resultsHost, mockHost, realHost })
        {
            if (go)
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    [Test]
    public void RealProvider_IsPreferredOverDevelopmentMock()
    {
        mockHost = CreateInactive("MockRewardedAdServiceHost");
        mockHost.AddComponent<MockRewardedAdService>();
        realHost = CreateInactive("RealRewardedAdProviderHost");
        var real = realHost.AddComponent<TestRewardedAdProvider>();
        Assert.NotNull(real, "Precondition: stand-in provider created.");

        Assert.AreSame(real, Resolve(), "A real rewarded-ad provider must win over MockRewardedAdService.");
    }

    [Test]
    public void CachedMock_IsReplacedOnceRealProviderAppears()
    {
        mockHost = CreateInactive("MockRewardedAdServiceHost");
        mockHost.AddComponent<MockRewardedAdService>();
        Assert.IsInstanceOf<MockRewardedAdService>(Resolve(), "Precondition: a mock is selected while only mocks exist.");

        realHost = CreateInactive("RealRewardedAdProviderHost");
        var real = realHost.AddComponent<TestRewardedAdProvider>();

        Assert.AreSame(real, Resolve(), "A provider created after the mock was cached must still take over.");
    }

    private static GameObject CreateInactive(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        return go;
    }

    private IRewardedAdService Resolve()
    {
        var method = typeof(ResultsController).GetMethod("ResolveRewardedAdService", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method, "ResultsController.ResolveRewardedAdService not found.");
        return (IRewardedAdService)method.Invoke(controller, null);
    }
}
