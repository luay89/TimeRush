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
    // Kills landed within this many seconds of each other chain into a streak with bonus score.
    [SerializeField] private float streakWindowSeconds = 3f;

    private static readonly string[] StreakNames = { "", "", "DOUBLE KILL", "TRIPLE KILL", "MEGA KILL", "RAMPAGE", "UNSTOPPABLE" };
    private int killStreak;
    private float lastKillTime = -100f;
    [SerializeField] private float beamDuration = 0.09f;

    private static readonly Color BeamColor = new Color(0.45f, 1f, 1f, 1f);

    private float cooldown;
    private float beamTimer;
    private LineRenderer beam;
    private ParticleSystem burst;
    private ParticleSystem debris;
    private Material debrisMaterial;
    private Material beamMaterial;
    private Material burstMaterial;
    private AudioSource audioSource;

    // Generated once per process (no audio assets needed): a falling laser zap and a noisy boom.
    private static AudioClip laserClip;
    private static AudioClip explosionClip;

    public bool IsReady => cooldown <= 0f;

    private void Awake()
    {
        BuildBeam();
        BuildBurst();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        if (!laserClip)
        {
            laserClip = CreateLaserClip();
            explosionClip = CreateExplosionClip();
        }
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

        if (debrisMaterial)
        {
            Destroy(debrisMaterial);
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
        if (cooldown > 0f || !GameStateMachine.IsGameplayInputAllowed || RunCountdown.IsCounting)
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
        PlaySound(laserClip, 0.45f);

        if (!target)
        {
            return true;
        }

        Vector3 hitPosition = target.transform.position;
        Explode(hitPosition);
        PlaySound(explosionClip, 0.8f);
        float now = Time.time;
        killStreak = now - lastKillTime <= streakWindowSeconds ? killStreak + 1 : 1;
        lastKillTime = now;
        // Each extra kill in a streak is worth half a kill more: 20, 30, 40, 50 ...
        int award = scorePerKill + scorePerKill * (killStreak - 1) / 2;
        FloatingScorePopup.Spawn(hitPosition, "+" + award, killStreak > 1 ? new Color(1f, 0.45f, 0.9f, 1f) : new Color(1f, 0.85f, 0.3f, 1f));
        AnnounceStreak(award);
        // A short kick on the camera sells the kill; goes through the Camera Shake preference.
        GameFeedbackSignals.RaiseAmbientCameraJolt(0.7f);
        // Deactivate now so it can neither collide this frame nor stay in any registry, then free it.
        target.gameObject.SetActive(false);
        Destroy(target.gameObject);

        if (controller)
        {
            controller.AddScore(award);
        }

        return true;
    }

    private void AnnounceStreak(int award)
    {
        if (killStreak < 2)
        {
            return;
        }

        string name = StreakNames[Mathf.Min(killStreak, StreakNames.Length - 1)];
        if (killStreak >= StreakNames.Length)
        {
            name += " x" + killStreak;
        }

        Color color = killStreak == 2 ? new Color(0.4f, 0.95f, 1f, 1f)
            : killStreak == 3 ? new Color(0.75f, 0.5f, 1f, 1f)
            : killStreak == 4 ? new Color(1f, 0.4f, 0.85f, 1f)
            : new Color(1f, 0.6f, 0.15f, 1f);
        CombatAnnouncer.Announce(name, $"STREAK BONUS  +{award - scorePerKill}", color, killStreak - 2);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource && clip && FeedbackPreferences.IsAudioEnabled(null))
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private static AudioClip CreateLaserClip()
    {
        const int rate = 44100;
        int length = (int)(rate * 0.18f);
        var data = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float frequency = Mathf.Lerp(1900f, 260f, t);
            phase += frequency / rate;
            float square = (phase % 1f) < 0.5f ? 1f : -1f;
            float sine = Mathf.Sin(phase * Mathf.PI * 2f);
            data[i] = (sine * 0.7f + square * 0.3f) * Mathf.Exp(-t * 5f) * 0.6f;
        }

        var clip = AudioClip.Create("BlasterLaser", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateExplosionClip()
    {
        const int rate = 44100;
        int length = (int)(rate * 0.7f);
        var data = new float[length];
        var random = new System.Random(1337);
        float low = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            // One-pole low-pass that closes over time: a bright crack decaying into a rumble.
            low += (noise - low) * Mathf.Lerp(0.5f, 0.04f, t);
            float rumble = Mathf.Sin(i * 2f * Mathf.PI * 55f / rate) * 0.35f;
            data[i] = (low + rumble * (1f - t)) * Mathf.Exp(-t * 4f) * 0.9f;
        }

        var clip = AudioClip.Create("BlasterExplosion", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.35f, 0.1f));
        main.maxParticles = 240;
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
        BuildDebris();
    }

    // Dark hull fragments that tumble outward and fall away -- reads as the ship breaking apart
    // rather than just a puff of fire.
    private void BuildDebris()
    {
        var go = new GameObject("BlasterDebris");
        go.transform.SetParent(transform, false);
        debris = go.AddComponent<ParticleSystem>();
        debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = debris.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startRotation3D = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.22f, 0.22f), new Color(1f, 0.5f, 0.2f));
        main.gravityModifier = 0.9f;
        main.maxParticles = 120;

        var emission = debris.emission;
        emission.rateOverTime = 0f;

        var shape = debris.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var rotation = debris.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        debrisMaterial = new Material(Shader.Find("Sprites/Default"));
        particleRenderer.sharedMaterial = debrisMaterial;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        debris.Play();
    }

    private void Explode(Vector3 position)
    {
        if (!burst)
        {
            return;
        }

        burst.transform.position = position;
        burst.Emit(60);
        if (debris)
        {
            debris.transform.position = position;
            debris.Emit(18);
        }
    }
}
