using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Focused, deterministic tests for the challenge sequencing layer
/// (<see cref="ChallengeConfig"/> / <see cref="ChallengeDirector"/>) in isolation from the
/// rest of the pattern pipeline. Covers required Tests A, B, D, E, G, K, L from the
/// Challenge &amp; Run Variety Expansion phase. See <c>ChallengeSimulationTests.cs</c> for the
/// integrated/simulation-level Tests C, F, H, I, J.
/// </summary>
public sealed class ChallengeDirectorTests
{
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

    // Test A: Challenge sequence determinism -- same seed and config always produce the
    // same NORMAL/PRESSURE/RECOVERY sequence and the same effective-difficulty values.
    [Test]
    public void A_SameSeedSameConfig_ProducesIdenticalChallengeSequence()
    {
        var config = new ChallengeConfig(0.2f, 0.5f, 0.2f, 0.25f, 3, 2, 2);
        var directorA = new ChallengeDirector(config);
        var directorB = new ChallengeDirector(config);
        var randomA = new DeterministicRandom(777u);
        var randomB = new DeterministicRandom(777u);

        for (int beat = 0; beat < 500; beat++)
        {
            float raw = (beat % 100) / 100f;
            float effectiveA = directorA.Advance(raw, randomA);
            float effectiveB = directorB.Advance(raw, randomB);

            Assert.That(directorB.State, Is.EqualTo(directorA.State));
            Assert.That(effectiveB, Is.EqualTo(effectiveA));
        }
    }

    // Test B: Different random seeds can produce different challenge sequences (the
    // sequencing layer is not secretly fixed to a single hard-coded pattern).
    [Test]
    public void B_DifferentSeeds_CanProduceDifferentChallengeSequences()
    {
        var config = new ChallengeConfig(0f, 0.5f, 0.2f, 0.25f, 3, 2, 2);

        string SequenceFor(uint seed)
        {
            var director = new ChallengeDirector(config);
            var random = new DeterministicRandom(seed);
            var builder = new System.Text.StringBuilder();

            for (int beat = 0; beat < 200; beat++)
            {
                director.Advance(1f, random);
                builder.Append((int)director.State);
            }

            return builder.ToString();
        }

        Assert.That(SequenceFor(1u), Is.Not.EqualTo(SequenceFor(2u)),
            "Different seeds should be able to produce different challenge sequences.");
    }

    // Test D: PRESSURE can never continue beyond the configured maximum consecutive beats.
    [Test]
    public void D_Pressure_NeverExceedsConfiguredMaxConsecutiveBeats()
    {
        var config = new ChallengeConfig(0f, 1f, 0.2f, 0.25f, 3, 2, 2);
        var director = new ChallengeDirector(config);
        var random = new DeterministicRandom(55u);

        int longestPressureRun = 0;
        int currentRun = 0;

        for (int beat = 0; beat < 300; beat++)
        {
            director.Advance(1f, random);

            if (director.State == ChallengeState.Pressure)
            {
                currentRun++;
                if (currentRun > longestPressureRun)
                {
                    longestPressureRun = currentRun;
                }
            }
            else
            {
                currentRun = 0;
            }
        }

        Assert.That(longestPressureRun, Is.GreaterThan(0), "Test setup should actually exercise PRESSURE.");
        Assert.That(longestPressureRun, Is.LessThanOrEqualTo(config.MaxConsecutivePressureBeats));
    }

    // Test E: A RECOVERY window always follows once the maximum consecutive PRESSURE
    // beats has been reached -- demanding stretches are always followed by a calmer one.
    [Test]
    public void E_MaxConsecutivePressure_IsAlwaysFollowedByRecovery()
    {
        var config = new ChallengeConfig(0f, 1f, 0.2f, 0.25f, 3, 2, 2);
        var director = new ChallengeDirector(config);
        var random = new DeterministicRandom(9u);

        bool sawRecoveryImmediatelyAfterCap = false;

        for (int beat = 0; beat < 20; beat++)
        {
            ChallengeState before = director.State;
            int beforeCount = director.ConsecutivePressureBeats;
            director.Advance(1f, random);

            if (before == ChallengeState.Pressure &&
                beforeCount >= config.MaxConsecutivePressureBeats &&
                director.State == ChallengeState.Recovery)
            {
                sawRecoveryImmediatelyAfterCap = true;
                break;
            }
        }

        Assert.That(sawRecoveryImmediatelyAfterCap, Is.True);
    }

    // Test G: Immediate pattern-family repetition is still avoided when alternatives
    // exist, even when the challenge layer is modulating the difficulty signal that
    // feeds PatternDirector/PatternSelector.
    [Test]
    public void G_ChallengeModulatedDifficulty_StillAvoidsImmediateFamilyRepetition()
    {
        ObstaclePatternDefinition[] families = SingleBeatFamilies();
        var director = new PatternDirector(families, new PatternSelector());
        var challenge = new ChallengeDirector(new ChallengeConfig(0f, 0.5f, 0.2f, 0.25f, 3, 2, 2));
        var random = new DeterministicRandom(303u);
        var buffer = new List<PatternSpawnRequest>();

        ObstaclePatternType previous = (ObstaclePatternType)(-1);

        for (int beat = 0; beat < 300; beat++)
        {
            bool wasMidPattern = director.IsMidPattern;
            float effective = challenge.Advance(1f, random);
            director.TickBeat(effective, random, buffer);

            if (wasMidPattern)
            {
                continue;
            }

            ObstaclePatternType current = director.LastSelectedType;
            Assert.That(current, Is.Not.EqualTo(previous),
                "Immediate family repetition should be avoided even when challenge modulates difficulty.");
            previous = current;
        }
    }

    // Test K: Normal gameplay randomness -- the deterministic random source exists only
    // for testing/debugging (reproducible for a fixed seed), while the production random
    // source used by real gameplay is never forced into a fixed sequence.
    [Test]
    public void K_ProductionRandomSource_IsNotFixedLikeDeterministicRandom()
    {
        var seededA = new DeterministicRandom(42u);
        var seededB = new DeterministicRandom(42u);
        bool deterministicReproducible = true;

        for (int i = 0; i < 20; i++)
        {
            if (seededA.NextFloat() != seededB.NextFloat())
            {
                deterministicReproducible = false;
                break;
            }
        }

        Assert.That(deterministicReproducible, Is.True,
            "DeterministicRandom must stay reproducible for a fixed seed (tests/simulation only).");

        float first = UnityRandomSource.Shared.NextFloat();
        bool sawVariation = false;

        for (int i = 0; i < 50; i++)
        {
            if (UnityRandomSource.Shared.NextFloat() != first)
            {
                sawVariation = true;
                break;
            }
        }

        Assert.That(sawVariation, Is.True,
            "Normal gameplay randomness (UnityRandomSource) must not be forced into a fixed sequence.");
    }

    // Test L: Challenge intensity boundaries (min/mid/max configured values) never
    // produce invalid state -- out-of-range configuration always clamps to a safe value.
    [Test]
    public void L_ChallengeConfig_BoundaryValues_RemainWithinValidRanges()
    {
        var belowRange = new ChallengeConfig(-5f, -1f, -1f, -1f, 0, 0, -5);
        Assert.That(belowRange.MinDifficultyForPressure, Is.EqualTo(0f));
        Assert.That(belowRange.PressureChance, Is.EqualTo(0f));
        Assert.That(belowRange.PressureDifficultyBoost, Is.EqualTo(0f));
        Assert.That(belowRange.RecoveryDifficultyCut, Is.EqualTo(0f));
        Assert.That(belowRange.MaxConsecutivePressureBeats, Is.EqualTo(1));
        Assert.That(belowRange.RecoveryBeats, Is.EqualTo(1));
        Assert.That(belowRange.PressureCooldownBeats, Is.EqualTo(0));

        var aboveRange = new ChallengeConfig(5f, 5f, 5f, 5f, int.MaxValue, int.MaxValue, int.MaxValue);
        Assert.That(aboveRange.MinDifficultyForPressure, Is.EqualTo(1f));
        Assert.That(aboveRange.PressureChance, Is.EqualTo(1f));
        Assert.That(aboveRange.PressureDifficultyBoost, Is.EqualTo(0.5f));
        Assert.That(aboveRange.RecoveryDifficultyCut, Is.EqualTo(0.5f));
        Assert.That(aboveRange.MaxConsecutivePressureBeats, Is.EqualTo(int.MaxValue));
        Assert.That(aboveRange.RecoveryBeats, Is.EqualTo(int.MaxValue));
        Assert.That(aboveRange.PressureCooldownBeats, Is.EqualTo(int.MaxValue));

        var director = new ChallengeDirector(belowRange);
        var random = new DeterministicRandom(1u);

        for (int beat = 0; beat < 50; beat++)
        {
            float effective = director.Advance(0.5f, random);
            Assert.That(float.IsNaN(effective), Is.False);
            Assert.That(effective, Is.InRange(0f, 1f));
        }
    }
}
