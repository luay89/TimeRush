using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Guards the committed Resources/ObstaclePatternSet.asset: it must exist, load, and
/// be byte-for-configuration identical to <see cref="ObstaclePatternSet.CreateDefault"/>.
/// This proves no accidental balance change slipped in when the runtime fallback was
/// promoted to a real authored asset.
/// </summary>
public sealed class ObstaclePatternSetAssetTests
{
    private const string ResourceName = "ObstaclePatternSet";

    [Test]
    public void RealAsset_LoadsFromResources()
    {
        var asset = Resources.Load<ObstaclePatternSet>(ResourceName);
        Assert.That(asset, Is.Not.Null,
            "Resources/ObstaclePatternSet.asset must exist so runtime uses the authored asset, not the fallback.");
    }

    [Test]
    public void RealAsset_ContainsExactlyFivePatternFamilies()
    {
        var asset = Resources.Load<ObstaclePatternSet>(ResourceName);
        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.Patterns, Is.Not.Null);
        Assert.That(asset.Patterns.Count, Is.EqualTo(5));

        var expectedNames = new List<string>
        {
            "Single", "Alternating Lanes", "Double Lane Block", "Staggered", "Depth Lane Combo"
        };
        var actualNames = new List<string>();
        foreach (var p in asset.Patterns)
        {
            actualNames.Add(p.Name);
        }
        Assert.That(actualNames, Is.EqualTo(expectedNames));
    }

    [Test]
    public void RealAsset_MatchesCreateDefaultExactly()
    {
        var asset = Resources.Load<ObstaclePatternSet>(ResourceName);
        Assert.That(asset, Is.Not.Null);

        var reference = ObstaclePatternSet.CreateDefault();
        IReadOnlyList<ObstaclePatternDefinition> expected = reference.Patterns;
        IReadOnlyList<ObstaclePatternDefinition> actual = asset.Patterns;

        Assert.That(actual.Count, Is.EqualTo(expected.Count),
            "Family count must match CreateDefault().");

        for (int i = 0; i < expected.Count; i++)
        {
            ObstaclePatternDefinition e = expected[i];
            ObstaclePatternDefinition a = actual[i];

            Assert.That(a.Name, Is.EqualTo(e.Name), $"Name mismatch at index {i}.");
            Assert.That(a.Type, Is.EqualTo(e.Type), $"Type mismatch for '{e.Name}'.");
            Assert.That(a.MinDifficulty, Is.EqualTo(e.MinDifficulty).Within(0.0001f),
                $"MinDifficulty mismatch for '{e.Name}'.");
            Assert.That(a.Weight, Is.EqualTo(e.Weight).Within(0.0001f),
                $"Weight mismatch for '{e.Name}'.");
            Assert.That(a.AllowMirror, Is.EqualTo(e.AllowMirror),
                $"AllowMirror mismatch for '{e.Name}'.");

            int expectedPlacements = e.Placements == null ? 0 : e.Placements.Length;
            int actualPlacements = a.Placements == null ? 0 : a.Placements.Length;
            Assert.That(actualPlacements, Is.EqualTo(expectedPlacements),
                $"Placement count mismatch for '{e.Name}'.");

            for (int p = 0; p < expectedPlacements; p++)
            {
                PatternPlacement ep = e.Placements[p];
                PatternPlacement ap = a.Placements[p];
                Assert.That(ap.Lane, Is.EqualTo(ep.Lane), $"Lane mismatch for '{e.Name}' placement {p}.");
                Assert.That(ap.DepthIndex, Is.EqualTo(ep.DepthIndex), $"DepthIndex mismatch for '{e.Name}' placement {p}.");
                Assert.That(ap.Beat, Is.EqualTo(ep.Beat), $"Beat mismatch for '{e.Name}' placement {p}.");
            }
        }
    }
}
