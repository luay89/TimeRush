using System.Collections.Generic;
using NUnit.Framework;

public sealed class PatternSelectorTests
{
    private readonly PatternSelector selector = new PatternSelector();

    private static ObstaclePatternDefinition[] Families()
    {
        return new[]
        {
            new ObstaclePatternDefinition("Single", ObstaclePatternType.Single, 0f, 50f, false, System.Array.Empty<PatternPlacement>()),
            new ObstaclePatternDefinition("Alternating", ObstaclePatternType.Alternating, 0.2f, 20f, true,
                new[] { new PatternPlacement(0, 1, 0), new PatternPlacement(2, 1, 1) }),
            new ObstaclePatternDefinition("Double", ObstaclePatternType.DoubleLaneBlock, 0.4f, 15f, true,
                new[] { new PatternPlacement(0, 1, 0), new PatternPlacement(1, 1, 0) }),
            new ObstaclePatternDefinition("Staggered", ObstaclePatternType.Staggered, 0.6f, 10f, true,
                new[] { new PatternPlacement(0, 0, 0), new PatternPlacement(2, 2, 1) }),
        };
    }

    [Test]
    public void LowDifficulty_SelectsOnlyBaselineFamily()
    {
        ObstaclePatternDefinition[] families = Families();
        var random = new DeterministicRandom(101u);

        for (int i = 0; i < 250; i++)
        {
            int index = selector.Select(families, 0f, null, random);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(families[index].Type, Is.EqualTo(ObstaclePatternType.Single));
        }
    }

    [Test]
    public void HighDifficulty_ProducesMultipleFamilies()
    {
        ObstaclePatternDefinition[] families = Families();
        var random = new DeterministicRandom(7u);
        var seen = new HashSet<ObstaclePatternType>();

        for (int i = 0; i < 600; i++)
        {
            int index = selector.Select(families, 1f, null, random);
            seen.Add(families[index].Type);
        }

        Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3), "High difficulty should surface several recognizable families.");
    }

    [Test]
    public void SameSeed_ProducesIdenticalSelectionSequence()
    {
        ObstaclePatternDefinition[] families = Families();
        var first = new DeterministicRandom(2024u);
        var second = new DeterministicRandom(2024u);

        for (int i = 0; i < 200; i++)
        {
            Assert.That(selector.Select(families, 0.75f, null, second), Is.EqualTo(selector.Select(families, 0.75f, null, first)));
        }
    }

    [Test]
    public void RecentFamily_IsAvoidedWhenAlternativesExist()
    {
        ObstaclePatternDefinition[] families = Families();
        var random = new DeterministicRandom(55u);
        var recent = new List<ObstaclePatternType> { ObstaclePatternType.Single };

        for (int i = 0; i < 300; i++)
        {
            int index = selector.Select(families, 1f, recent, random);
            Assert.That(families[index].Type, Is.Not.EqualTo(ObstaclePatternType.Single));
        }
    }

    [Test]
    public void AllFamiliesRecent_StillReturnsAnEligiblePattern()
    {
        ObstaclePatternDefinition[] families = Families();
        var random = new DeterministicRandom(9u);
        var recent = new List<ObstaclePatternType>
        {
            ObstaclePatternType.Single,
            ObstaclePatternType.Alternating,
            ObstaclePatternType.DoubleLaneBlock,
            ObstaclePatternType.Staggered,
        };

        int index = selector.Select(families, 1f, recent, random);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), "Eligibility must win over anti-repetition.");
    }

    [Test]
    public void NoEligibleFamily_ReturnsMinusOne()
    {
        var families = new[]
        {
            new ObstaclePatternDefinition("Alternating", ObstaclePatternType.Alternating, 0.5f, 20f, true,
                new[] { new PatternPlacement(0, 1, 0) }),
        };

        Assert.That(selector.Select(families, 0f, null, new DeterministicRandom(3u)), Is.EqualTo(-1));
    }
}
