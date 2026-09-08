using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Focused tests added for the Mobile Performance &amp; Production Readiness Foundation phase.
/// These exist to prove specific, measurable properties called out by the mobile performance
/// audit -- they are not implementation-detail tests and are not a general performance suite.
/// </summary>
public sealed class MobilePerformanceTests
{
    // Test A: gameplay scripts never pull in System.Linq. LINQ extension methods (Where/Select/
    // etc.) allocate enumerators/closures, which is exactly the kind of hidden per-beat garbage
    // this phase audited for. This scans actual source files, so it directly proves the property
    // rather than re-testing behavior already covered elsewhere.
    [Test]
    public void A_GameplayScriptsContainNoLinqUsage()
    {
        string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
        Assert.That(Directory.Exists(scriptsRoot), Is.True, $"Expected gameplay scripts folder at {scriptsRoot}.");

        string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
        Assert.That(files.Length, Is.GreaterThan(0), "Expected to find gameplay script files to scan.");

        var offenders = new List<string>();
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            if (text.Contains("System.Linq"))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.That(offenders, Is.Empty,
            "Gameplay scripts must not depend on System.Linq (allocation risk in per-beat hot paths): " + string.Join(", ", offenders));
    }

    // Test B: the challenge sequencing layer stays allocation-free once warmed up. ChallengeDirector
    // runs every beat for the lifetime of a run, so a per-call allocation here would be a steady,
    // unbounded GC cost during normal gameplay.
    [Test]
    public void B_ChallengeDirector_AdvanceRemainsAllocationFree()
    {
        var config = new ChallengeConfig(0.2f, 0.5f, 0.2f, 0.25f, 3, 2, 2);
        var director = new ChallengeDirector(config);
        var random = new DeterministicRandom(999u);

        // Warm up first (JIT compilation, static initializers) before measuring.
        for (int i = 0; i < 1000; i++)
        {
            director.Advance((i % 100) / 100f, random);
        }

        long before = System.GC.GetTotalMemory(true);
        for (int i = 0; i < 20000; i++)
        {
            director.Advance((i % 100) / 100f, random);
        }
        long after = System.GC.GetTotalMemory(false);

        Assert.That(after - before, Is.LessThan(2048),
            $"ChallengeDirector.Advance allocated {after - before} bytes across 20000 calls; it must stay allocation-free per call.");
    }

    // Test C: repeated pattern selection stays allocation-free. PatternSelector.Select runs on
    // every spawn-eligible beat, so the same steady-state GC-pressure concern applies here.
    [Test]
    public void C_PatternSelector_SelectRemainsAllocationFree()
    {
        var patterns = new[]
        {
            new ObstaclePatternDefinition("Single", ObstaclePatternType.Single, 0f, 55f, false, System.Array.Empty<PatternPlacement>()),
            new ObstaclePatternDefinition("Alt", ObstaclePatternType.Alternating, 0.15f, 18f, true,
                new[] { new PatternPlacement(0, 1, 0), new PatternPlacement(2, 1, 1) }),
            new ObstaclePatternDefinition("Combo", ObstaclePatternType.DepthLaneCombo, 0.3f, 12f, true,
                new[] { new PatternPlacement(1, 0, 0), new PatternPlacement(1, 2, 0) }),
        };
        var recent = new List<ObstaclePatternType>(2);
        var selector = new PatternSelector();
        var random = new DeterministicRandom(555u);

        for (int i = 0; i < 1000; i++)
        {
            selector.Select(patterns, 1f, recent, random);
        }

        long before = System.GC.GetTotalMemory(true);
        for (int i = 0; i < 20000; i++)
        {
            selector.Select(patterns, 1f, recent, random);
        }
        long after = System.GC.GetTotalMemory(false);

        Assert.That(after - before, Is.LessThan(2048),
            $"PatternSelector.Select allocated {after - before} bytes across 20000 calls; it must stay allocation-free per call.");
    }
}
