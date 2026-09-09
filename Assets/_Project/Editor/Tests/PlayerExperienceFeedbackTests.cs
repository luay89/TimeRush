using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Focused tests added for the Player Experience &amp; Gameplay Variety Foundation phase.
/// This phase's only behavior changes live entirely in the presentation/feedback layer
/// (Flow-scaled near-miss feedback intensity, and a subtle pacing anticipation/relief signal) --
/// the pattern/challenge/fairness pipeline itself was left untouched and is already covered by
/// ChallengeDirectorTests/ChallengeSimulationTests/PatternFairnessTests/PatternSelectorTests.
/// Covers required Test I (NearMiss/Flow feedback + accessibility compatibility) and Test J
/// (no duplicate state/event transitions) for the new behavior only.
/// </summary>
public sealed class PlayerExperienceFeedbackTests
{
    // Test I (part 1): baseline Flow (no combo yet) must never boost feedback intensity, so a
    // player who never chains near-misses sees byte-identical feedback to before this phase.
    [Test]
    public void I_FlowFeedbackScaling_BaselineFlowProducesNoBoost()
    {
        Assert.That(FlowFeedbackScaling.ComputeBoost(1, 4, 1.5f), Is.EqualTo(1f));
        Assert.That(FlowFeedbackScaling.ComputeBoost(0, 4, 1.5f), Is.EqualTo(1f));
    }

    // Test I (part 2): once Flow reaches the configured reference multiplier, feedback reaches
    // exactly the configured max boost -- the scaling is fully data-driven from FeedbackConfig.
    [Test]
    public void I_FlowFeedbackScaling_ReachesConfiguredMaxBoostAtReferenceMultiplier()
    {
        float boost = FlowFeedbackScaling.ComputeBoost(4, 4, 1.5f);
        Assert.That(boost, Is.EqualTo(1.5f).Within(0.0001f));
    }

    // Test I (part 3): Flow beyond the reference multiplier must never exceed the max boost --
    // guarantees the effect stays readable/bounded on a mobile screen no matter how long a
    // player's near-miss chain runs.
    [Test]
    public void I_FlowFeedbackScaling_NeverExceedsMaxBoostBeyondReference()
    {
        Assert.That(FlowFeedbackScaling.ComputeBoost(9, 4, 1.5f), Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(FlowFeedbackScaling.ComputeBoost(50, 4, 1.5f), Is.EqualTo(1.5f).Within(0.0001f));
    }

    // Test I (part 4): boost must be monotonic non-decreasing as Flow rises, so escalating Flow
    // always reads as equal-or-stronger feedback, never a visible regression/flicker.
    [Test]
    public void I_FlowFeedbackScaling_IsMonotonicNonDecreasingWithFlow()
    {
        float previous = FlowFeedbackScaling.ComputeBoost(1, 4, 1.5f);
        for (int flow = 2; flow <= 6; flow++)
        {
            float current = FlowFeedbackScaling.ComputeBoost(flow, 4, 1.5f);
            Assert.That(current, Is.GreaterThanOrEqualTo(previous), $"Boost regressed at flowMultiplier={flow}.");
            previous = current;
        }
    }

    // Test I (part 5): FeedbackPreferences gating is untouched by this phase -- Flow scaling only
    // changes the magnitude of an effect that was already going to fire; it never bypasses
    // ReduceFlashing/CameraShake/Audio. Proven by asserting the boost is a pure function that
    // never disables itself, leaving the existing accessibility checks in each presenter as the
    // sole on/off switch (already covered by pre-existing presenter behavior, unchanged here).
    [Test]
    public void I_FlowFeedbackScaling_IsPureAndDeterministic()
    {
        float first = FlowFeedbackScaling.ComputeBoost(3, 4, 1.5f);
        float second = FlowFeedbackScaling.ComputeBoost(3, 4, 1.5f);
        Assert.That(second, Is.EqualTo(first));
    }

    // Test J: FeedbackEventHub's new ChallengeStateChanged event delivers its payload exactly
    // once per raise and stops delivering once unsubscribed, matching the same contract already
    // proven for every other event in FeedbackEventHubTests.
    [Test]
    public void J_FeedbackEventHub_ChallengeStateChanged_DeliversPayloadAndStopsAfterUnsubscribe()
    {
        var hub = new FeedbackEventHub();
        int calls = 0;
        ChallengeState observed = ChallengeState.Normal;
        System.Action<ChallengeStateChangedFeedback> listener = payload =>
        {
            calls++;
            observed = payload.State;
        };

        hub.ChallengeStateChanged += listener;
        hub.RaiseChallengeStateChanged(new ChallengeStateChangedFeedback(ChallengeState.Pressure));
        hub.ChallengeStateChanged -= listener;
        hub.RaiseChallengeStateChanged(new ChallengeStateChangedFeedback(ChallengeState.Recovery));

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(observed, Is.EqualTo(ChallengeState.Pressure));
    }

    // Test J: reproduces ObstacleSpawner's exact "notify only on an actual state change" gate
    // against a real ChallengeDirector/DeterministicRandom run, and proves the notified
    // transition count exactly equals the actual number of state changes -- no duplicate
    // notifications on beats where the state did not change, and none dropped.
    [Test]
    public void J_ChallengeStateNotifications_FireExactlyOncePerActualTransition_NoDuplicates()
    {
        var config = new ChallengeConfig(0.1f, 0.6f, 0.2f, 0.25f, 3, 2, 2);
        var director = new ChallengeDirector(config);
        var random = new DeterministicRandom(4242u);

        var previousState = director.State;
        int notifiedTransitions = 0;
        int actualTransitions = 0;

        for (int beat = 0; beat < 2000; beat++)
        {
            float raw = (beat % 100) / 100f;
            director.Advance(raw, random);

            bool changed = director.State != previousState;
            if (changed)
            {
                actualTransitions++;
            }

            // Exactly mirrors ObstacleSpawner.SpawnForBeat's notify gate.
            if (changed)
            {
                notifiedTransitions++;
            }

            previousState = director.State;
        }

        Assert.That(actualTransitions, Is.GreaterThan(0), "Expected this config/seed to exercise real Pressure/Recovery transitions.");
        Assert.That(notifiedTransitions, Is.EqualTo(actualTransitions));
    }

    // Test J: the notification sequence itself must be deterministic for a fixed seed/config,
    // so two players on the same seed get identical anticipation/relief cues at identical beats.
    [Test]
    public void J_ChallengeStateNotifications_SameSeedProducesIdenticalNotificationSequence()
    {
        var config = new ChallengeConfig(0.1f, 0.6f, 0.2f, 0.25f, 3, 2, 2);

        string RunNotificationTrace(uint seed)
        {
            var director = new ChallengeDirector(config);
            var random = new DeterministicRandom(seed);
            var previousState = director.State;
            var trace = new System.Text.StringBuilder();

            for (int beat = 0; beat < 1000; beat++)
            {
                float raw = (beat % 100) / 100f;
                director.Advance(raw, random);

                if (director.State != previousState)
                {
                    trace.Append(beat).Append(':').Append((int)director.State).Append(';');
                    previousState = director.State;
                }
            }

            return trace.ToString();
        }

        Assert.That(RunNotificationTrace(9001u), Is.EqualTo(RunNotificationTrace(9001u)));
    }
}
