using UnityEngine;

/// <summary>
/// Reuses a small pool of short world-space particle bursts for high-value gameplay feedback.
/// </summary>
public sealed class FeedbackVfxPresenter : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    private ParticleSystem[] pulsePool;
    private int nextPulseIndex;

    private void Awake()
    {
        CreatePool();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopAllPulses();
    }

    private void Subscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.NearMissTriggered += HandleNearMiss;
        events.ObstacleCollision += HandleCollision;
        events.RunStarted += StopAllPulses;
        events.RunPaused += StopAllPulses;
    }

    private void Unsubscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.NearMissTriggered -= HandleNearMiss;
        events.ObstacleCollision -= HandleCollision;
        events.RunStarted -= StopAllPulses;
        events.RunPaused -= StopAllPulses;
    }

    private void CreatePool()
    {
        int count = feedbackConfig ? feedbackConfig.pooledPulseCount : 1;
        pulsePool = new ParticleSystem[count];

        for (int i = 0; i < count; i++)
        {
            var pulseObject = new GameObject("FeedbackPulse_" + i);
            pulseObject.transform.SetParent(transform, false);
            var particles = pulseObject.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles);
            pulsePool[i] = particles;
        }
    }

    private void ConfigureParticleSystem(ParticleSystem particles)
    {
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 3f;
        main.startSize = 0.10f;

        var emission = particles.emission;
        emission.enabled = false;

        var shape = particles.shape;
        shape.enabled = false;
    }

    private void HandleNearMiss(NearMissFeedback payload)
    {
        Emit(payload.Position, feedbackConfig ? feedbackConfig.nearMissColor : Color.cyan, feedbackConfig ? feedbackConfig.nearMissParticleCount : 0, feedbackConfig ? feedbackConfig.nearMissParticleLifetime : 0f);
    }

    private void HandleCollision(ObstacleCollisionFeedback payload)
    {
        Emit(payload.Position, feedbackConfig ? feedbackConfig.collisionColor : Color.red, feedbackConfig ? feedbackConfig.collisionParticleCount : 0, feedbackConfig ? feedbackConfig.collisionParticleLifetime : 0f);
    }

    private void Emit(Vector3 position, Color color, int particleCount, float lifetime)
    {
        if (pulsePool == null || pulsePool.Length == 0 || particleCount <= 0 || lifetime <= 0f)
        {
            return;
        }

        ParticleSystem particles = pulsePool[nextPulseIndex];
        nextPulseIndex = (nextPulseIndex + 1) % pulsePool.Length;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.transform.position = position;

        var main = particles.main;
        main.startColor = color;
        main.startLifetime = lifetime;
        particles.Emit(particleCount);
    }

    private void StopAllPulses()
    {
        if (pulsePool == null)
        {
            return;
        }

        for (int i = 0; i < pulsePool.Length; i++)
        {
            if (pulsePool[i])
            {
                pulsePool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
