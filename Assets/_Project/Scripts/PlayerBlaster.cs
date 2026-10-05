using UnityEngine;

/// <summary>
/// The ship's laser cannon: Space (or a quick tap on touch screens) fires a short beam at the
/// nearest enemy warship coming down the player's own lane and destroys it. A cooldown keeps it a
/// tactical option rather than a way to clear the track. Shooting can only remove hazards, never
/// add or move one, so every survival option FairnessValidator guaranteed at spawn time still
/// exists after a shot.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBlaster : MonoBehaviour
{
    [SerializeField, Range(0.2f, 3f)] private float cooldownSeconds = 0.9f;
    [Tooltip("Half-width (world units) around the ship's X position that counts as 'in my lane'.")]
    [SerializeField] private float laneTolerance = 1.2f;
    [Tooltip("How far above the ship a warship can be and still be hit.")]
    [SerializeField] private float maxRange = 15f;
    [SerializeField] private int scorePerKill = 20;
    [SerializeField] private float beamDuration = 0.09f;

    private static readonly Color BeamColor = new Color(0.45f, 1f, 1f, 1f);

    private float cooldown;
    private float beamTimer;
    private LineRenderer beam;
    private ParticleSystem burst;
    private Material beamMaterial;
    private Material burstMaterial;

    public bool IsReady => cooldown <= 0f;

    private void Awake()
    {
        BuildBeam();
        BuildBurst();
    }

    private void OnDestroy()
    {
        if (beamMaterial)
        {
            Destroy(beamMaterial);
        }

        if (burstMaterial)
        {
            Destroy(burstMaterial);
        }
    }

    private void Update()
    {
        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
        }

        if (beamTimer > 0f)
        {
            beamTimer -= Time.deltaTime;
            if (beamTimer <= 0f && beam)
            {
                beam.enabled = false;
            }
        }
    }

    /// <summary>Fires if the cannon is ready during play. Returns true when a shot went out.</summary>
    public bool TryFire()
    {
        if (cooldown > 0f || !GameStateMachine.IsGameplayInputAllowed)
        {
            return false;
        }

        var controller = GameController.Instance;
        if (controller && controller.IsGameOver)
        {
            return false;
        }

        cooldown = cooldownSeconds;
        Vector3 muzzle = transform.position + new Vector3(0f, 0.15f, 0.6f);
        ObstacleLaneMarker target = FindTarget();
        Vector3 end = target ? target.transform.position : muzzle + new Vector3(0f, maxRange * 0.6f, 6f);
        ShowBeam(muzzle, end);

        if (!target)
        {
            return true;
        }

        Explode(target.transform.position);
        // Deactivate now so it can neither collide this frame nor stay in any registry, then free it.
        target.gameObject.SetActive(false);
        Destroy(target.gameObject);

        if (controller)
        {
            controller.AddScore(scorePerKill);
        }

        return true;
    }

    // The lowest warship still above the ship in its lane: the one about to arrive first.
    private ObstacleLaneMarker FindTarget()
    {
        Vector3 origin = transform.position;
        ObstacleLaneMarker best = null;
        float bestHeight = float.MaxValue;
        var list = ObstacleLaneMarker.Active;

        for (int i = 0; i < list.Count; i++)
        {
            ObstacleLaneMarker marker = list[i];
            if (!marker)
            {
                continue;
            }

            Vector3 position = marker.transform.position;
            float above = position.y - origin.y;
            if (Mathf.Abs(position.x - origin.x) > laneTolerance || above < -0.2f || above > maxRange)
            {
                continue;
            }

            if (above < bestHeight)
            {
                bestHeight = above;
                best = marker;
            }
        }

        return best;
    }

    private void BuildBeam()
    {
        var go = new GameObject("BlasterBeam");
        go.transform.SetParent(transform, false);
        beam = go.AddComponent<LineRenderer>();
        beamMaterial = new Material(Shader.Find("Sprites/Default"));
        beam.sharedMaterial = beamMaterial;
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.startWidth = 0.16f;
        beam.endWidth = 0.06f;
        beam.startColor = BeamColor;
        beam.endColor = new Color(BeamColor.r, BeamColor.g, BeamColor.b, 0.6f);
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;
        beam.enabled = false;
    }

    private void ShowBeam(Vector3 from, Vector3 to)
    {
        if (!beam)
        {
            return;
        }

        beam.SetPosition(0, from);
        beam.SetPosition(1, to);
        beam.enabled = true;
        beamTimer = beamDuration;
    }

    private void BuildBurst()
    {
        var go = new GameObject("BlasterExplosion");
        go.transform.SetParent(transform, false);
        burst = go.AddComponent<ParticleSystem>();
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = burst.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.35f, 0.1f));
        main.maxParticles = 120;
        main.gravityModifier = 0f;

        var emission = burst.emission;
        emission.rateOverTime = 0f;

        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var colorOverLifetime = burst.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        burstMaterial = SoftParticleMaterial.Create();
        particleRenderer.sharedMaterial = burstMaterial;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        burst.Play();
    }

    private void Explode(Vector3 position)
    {
        if (!burst)
        {
            return;
        }

        burst.transform.position = position;
        burst.Emit(28);
    }
}
