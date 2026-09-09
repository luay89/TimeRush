using NUnit.Framework;

/// <summary>
/// Focused tests added for the Product Polish -- Visual Identity &amp; Player Experience phase.
/// This phase's only behavior change is the ambient speed-perception camera FOV cue (see
/// SpeedPerceptionMath / CameraFollow); every gameplay/fairness/pattern/progression system was
/// left untouched and is already covered by the existing test suite.
/// </summary>
public sealed class SpeedPerceptionMathTests
{
    // Test A/B: pure, deterministic presentation math -- same inputs always produce the same
    // output, and the functions have no side effects (no gameplay state to alter).
    [Test]
    public void A_ResolveEffectivePace_IsPureAndDeterministic()
    {
        float first = SpeedPerceptionMath.ResolveEffectivePace(1.8f, true, true);
        float second = SpeedPerceptionMath.ResolveEffectivePace(1.8f, true, true);
        Assert.That(second, Is.EqualTo(first));
    }

    // Test C: Camera Shake accessibility preference gates the cue -- disabled means the FOV
    // cue stays pinned at the neutral baseline regardless of actual pace.
    [Test]
    public void C_ResolveEffectivePace_CameraShakeDisabled_ForcesNeutralBaseline()
    {
        Assert.That(SpeedPerceptionMath.ResolveEffectivePace(2f, true, false), Is.EqualTo(1f));
    }

    // Test C: the cue must also stay neutral whenever gameplay isn't active (menus, results,
    // pause, game over) so it never lingers boosted outside a live run.
    [Test]
    public void C_ResolveEffectivePace_GameplayInactive_ForcesNeutralBaseline()
    {
        Assert.That(SpeedPerceptionMath.ResolveEffectivePace(2f, false, true), Is.EqualTo(1f));
    }

    [Test]
    public void ResolveEffectivePace_ActiveAndEnabled_PassesRawPaceThrough()
    {
        Assert.That(SpeedPerceptionMath.ResolveEffectivePace(1.6f, true, true), Is.EqualTo(1.6f));
    }

    // Test B: baseline pace (<=1, no speed-up yet) must produce zero boost, so a run's opening
    // moments look byte-identical to before this phase.
    [Test]
    public void ComputeTargetFieldOfView_BaselinePace_ProducesNoBoost()
    {
        float fov = SpeedPerceptionMath.ComputeTargetFieldOfView(58f, 1f, 2f, 5f);
        Assert.That(fov, Is.EqualTo(58f));
    }

    // The cue reaches exactly the configured max boost at the configured reference pace, and
    // never exceeds it beyond that -- keeps the effect bounded/readable on a mobile screen.
    [Test]
    public void ComputeTargetFieldOfView_ReachesMaxBoostAtReferencePace_AndNeverExceedsIt()
    {
        float atReference = SpeedPerceptionMath.ComputeTargetFieldOfView(58f, 2f, 2f, 5f);
        float beyondReference = SpeedPerceptionMath.ComputeTargetFieldOfView(58f, 10f, 2f, 5f);

        Assert.That(atReference, Is.EqualTo(63f).Within(0.0001f));
        Assert.That(beyondReference, Is.EqualTo(63f).Within(0.0001f));
    }

    // Monotonic non-decreasing as pace rises -- escalating speed always reads as equal-or-wider
    // FOV, never a visible regression/flicker.
    [Test]
    public void ComputeTargetFieldOfView_IsMonotonicNonDecreasingWithPace()
    {
        float previous = SpeedPerceptionMath.ComputeTargetFieldOfView(58f, 1f, 2f, 5f);
        for (float pace = 1.2f; pace <= 2.4f; pace += 0.2f)
        {
            float current = SpeedPerceptionMath.ComputeTargetFieldOfView(58f, pace, 2f, 5f);
            Assert.That(current, Is.GreaterThanOrEqualTo(previous), $"FOV regressed at pace={pace}.");
            previous = current;
        }
    }
}
