using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

public sealed class PatternDirectorTests
{
    private static ObstaclePatternDefinition MultiBeatOnly()
    {
        return new ObstaclePatternDefinition("Alternating", ObstaclePatternType.Alternating, 0f, 10f, false,
            new[]
            {
                new PatternPlacement(0, 1, 0),
                new PatternPlacement(2, 1, 1),
                new PatternPlacement(1, 1, 2),
            });
    }

    private static ObstaclePatternDefinition[] SingleBeatFamilies()
    {
        return new[]
        {
            new ObstaclePatternDefinition("Single", ObstaclePatternType.Single, 0f, 10f, false, System.Array.Empty<PatternPlacement>()),
            new ObstaclePatternDefinition("Double", ObstaclePatternType.DoubleLaneBlock, 0f, 10f, false,
                new[] { new PatternPlacement(0, 1, 0), new PatternPlacement(1, 1, 0) }),
            new ObstaclePatternDefinition("Combo", ObstaclePatternType.DepthLaneCombo, 0f, 10f, false,
                new[] { new PatternPlacement(1, 0, 0), new PatternPlacement(1, 2, 0) }),
        };
    }

    private static string Encode(IReadOnlyList<PatternSpawnRequest> requests)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < requests.Count; i++)
        {
            PatternSpawnRequest request = requests[i];
            builder.Append(request.IsBaseline ? 'B' : 'P').Append(request.Lane).Append(':').Append(request.DepthIndex).Append('|');
        }
        return builder.ToString();
    }

    [Test]
    public void SameSeed_ProducesIdenticalBeatSequence()
    {
        ObstaclePatternDefinition[] families = SingleBeatFamilies();
        var directorA = new PatternDirector(families, new PatternSelector());
        var directorB = new PatternDirector(families, new PatternSelector());
        var randomA = new DeterministicRandom(4242u);
        var randomB = new DeterministicRandom(4242u);
        var bufferA = new List<PatternSpawnRequest>();
        var bufferB = new List<PatternSpawnRequest>();

        for (int beat = 0; beat < 300; beat++)
        {
            directorA.TickBeat(1f, randomA, bufferA);
            directorB.TickBeat(1f, randomB, bufferB);
            Assert.That(Encode(bufferB), Is.EqualTo(Encode(bufferA)));
        }
    }

    [Test]
    public void MultiBeatPattern_UnfoldsAcrossConsecutiveBeats()
    {
        var families = new[] { MultiBeatOnly() };
        var director = new PatternDirector(families, new PatternSelector());
        var random = new DeterministicRandom(1u);
        var buffer = new List<PatternSpawnRequest>();

        director.TickBeat(1f, random, buffer);
        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer[0].IsBaseline, Is.False);
        Assert.That(director.IsMidPattern, Is.True);

        director.TickBeat(1f, random, buffer);
        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer[0].IsBaseline, Is.False);
        Assert.That(director.IsMidPattern, Is.True);

        director.TickBeat(1f, random, buffer);
        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer[0].IsBaseline, Is.False);
        Assert.That(director.IsMidPattern, Is.False, "Pattern should be complete after its final scheduled beat.");
    }

    [Test]
    public void NoEligibleFamily_EmitsBaselineSingle()
    {
        var families = new[]
        {
            new ObstaclePatternDefinition("Late", ObstaclePatternType.Staggered, 0.9f, 10f, false,
                new[] { new PatternPlacement(0, 1, 0) }),
        };
        var director = new PatternDirector(families, new PatternSelector());
        var buffer = new List<PatternSpawnRequest>();

        director.TickBeat(0f, new DeterministicRandom(2u), buffer);

        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer[0].IsBaseline, Is.True);
        Assert.That(director.LastSelectedType, Is.EqualTo(ObstaclePatternType.Single));
    }

    [Test]
    public void ConsecutiveSelections_DoNotRepeatWhenAlternativesExist()
    {
        ObstaclePatternDefinition[] families = SingleBeatFamilies();
        var director = new PatternDirector(families, new PatternSelector());
        var random = new DeterministicRandom(88u);
        var buffer = new List<PatternSpawnRequest>();

        ObstaclePatternType previous = (ObstaclePatternType)(-1);

        for (int beat = 0; beat < 200; beat++)
        {
            director.TickBeat(1f, random, buffer);
            ObstaclePatternType current = director.LastSelectedType;
            Assert.That(current, Is.Not.EqualTo(previous), "Immediate family repetition should be avoided when alternatives exist.");
            previous = current;
        }
    }
}
