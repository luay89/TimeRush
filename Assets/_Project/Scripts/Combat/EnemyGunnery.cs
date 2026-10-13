using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Enemy warships fire red lasers at the player. Every shot is telegraphed: a thin red aiming
/// line and a red target ring appear on the player's flight plane, and only after a clear warning
/// does the bolt fly. Moving off the ring (any lane or depth step) dodges it; shooting the gunner
/// during the warning cancels the shot.
///
/// Fairness: obstacle spawning and FairnessValidator are untouched. A shot is only taken when the
/// player is not already under a falling warship and at least one neighbouring position (left or
/// right lane, or one depth step forward or back) is clear of warships that could arrive while the
/// shot plays out -- so the dodge the shot demands always exists. Shots also wait until the
/// training window is over, and never fire during the countdown or while invulnerable.
/// </summary>
public sealed class EnemyGunnery : MonoBehaviour
{
    private const float TelegraphSeconds = 0.85f;
    private const float BoltSeconds = 0.28f;
    private const float FirstShotDelay = 4f;
    private const float HitHalfWidth = 0.85f;
    private const float HitHalfDepth = 0.7f;
    private const float LaneSpacing = 2.5f;
    private const float DepthStep = 1.5f;

    private static readonly Color LaserColor = new Color(1f, 0.15f, 0.12f, 1f);

    private PlayerController player;
    private float shotTimer;
    private Shot shot;
    private Material lineMaterial;
    private Material ringMaterial;
    private LineRenderer aimLine;
    private LineRenderer bolt;
    private Transform targetRing;
    private ParticleSystem sparks;
    private Material sparkMaterial;
    private AudioSource audioSource;
    private AudioClip chargeClip;
    private AudioClip fireClip;

    private enum Phase { Idle, Telegraph, Bolt }

    private struct Shot
    {
        public Phase Phase;
        public float Time;
        public ObstacleLaneMarker Shooter;
        public Vector3 Origin;
        public Vector3 Target;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (scene.name != SceneNames.Game || FindObjectOfType<EnemyGunnery>())
        {
            return;
        }

        var go = new GameObject("EnemyGunnery");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<EnemyGunnery>();
    }

    private void Awake()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        ringMaterial = new Material(Shader.Find("Sprites/Default"));

        aimLine = CreateLine("EnemyAimLine", 0.05f, 0.03f);
        bolt = CreateLine("EnemyBolt", 0.22f, 0.12f);

        var ringObject = new GameObject("EnemyTargetRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Ring();
        var ringRenderer = ringObject.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = ringMaterial;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        targetRing = ringObject.transform;
        targetRing.gameObject.SetActive(false);

        BuildSparks();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        chargeClip = CreateChargeClip();
        fireClip = CreateFireClip();

        shotTimer = FirstShotDelay;
    }

    private void OnDestroy()
    {
        Destroy(lineMaterial);
        Destroy(ringMaterial);
        if (sparkMaterial)
        {
            Destroy(sparkMaterial);
        }

        if (chargeClip)
        {
            Destroy(chargeClip);
        }

        if (fireClip)
        {
            Destroy(fireClip);
        }
    }

    private void Update()
    {
        if (!player)
        {
            player = FindObjectOfType<PlayerController>();
            if (!player)
            {
                return;
            }
        }

        GameController controller = GameController.Instance;
        bool active = controller && !controller.IsGameOver && !RunCountdown.IsCounting && GameStateMachine.IsGameplayInputAllowed;
        if (!active)
        {
            if (controller && controller.IsGameOver)
            {
                CancelShot();
            }

            return;
        }

        switch (shot.Phase)
        {
            case Phase.Idle:
                TickIdle(controller);
                break;
            case Phase.Telegraph:
                TickTelegraph();
                break;
            case Phase.Bolt:
                TickBolt(controller);
                break;
        }
    }

    private void TickIdle(GameController controller)
    {
        if (controller.IsInTrainingWindow)
        {
            return;
        }

        shotTimer -= Time.deltaTime;
        if (shotTimer > 0f)
        {
            return;
        }

        if (TryStartShot())
        {
            // Faster volleys as the run speeds up, but never closer than ~1.6 s apart.
            float pace = Mathf.Max(1f, controller.GetPaceMultiplier());
            shotTimer = Random.Range(2.4f, 4f) / pace;
            shotTimer = Mathf.Max(1.6f, shotTimer);
        }
        else
        {
            shotTimer = 0.4f; // nothing fair to shoot right now; look again shortly
        }
    }

    private bool TryStartShot()
    {
        Vector3 playerPosition = player.transform.position;
        ObstacleLaneMarker shooter = PickShooter(playerPosition);
        if (!shooter || !HasFairDodge(playerPosition))
        {
            return false;
        }

        shot = new Shot
        {
            Phase = Phase.Telegraph,
            Time = 0f,
            Shooter = shooter,
            Origin = shooter.transform.position,
            Target = new Vector3(playerPosition.x, playerPosition.y, playerPosition.z),
        };

        targetRing.gameObject.SetActive(true);
        targetRing.position = new Vector3(shot.Target.x, shot.Target.y - 0.3f, shot.Target.z);
        aimLine.enabled = true;
        PlayClip(chargeClip, 0.35f);
        return true;
    }

    // A visible warship well above the player, off to one side or ahead -- never one that is
    // already about to arrive (that one is the player's problem already).
    private static ObstacleLaneMarker PickShooter(Vector3 playerPosition)
    {
        ObstacleLaneMarker best = null;
        float bestScore = float.MaxValue;
        var list = ObstacleLaneMarker.Active;
        for (int i = 0; i < list.Count; i++)
        {
            ObstacleLaneMarker marker = list[i];
            if (!marker)
            {
                continue;
            }

            float above = marker.transform.position.y - playerPosition.y;
            if (above < 5f || above > 13f)
            {
                continue;
            }

            // Prefer the one closest to mid-screen height so the aim line is easy to read.
            float score = Mathf.Abs(above - 8f) + Random.value;
            if (score < bestScore)
            {
                bestScore = score;
                best = marker;
            }
        }

        return best;
    }

    private bool HasFairDodge(Vector3 playerPosition)
    {
        // How far a warship falls while the shot plays out (plus a margin for the dodge itself).
        float window = TelegraphSeconds + BoltSeconds + 0.6f;

        // Not while the player is already under an incoming warship.
        if (IsCellThreatened(playerPosition.x, playerPosition.z, playerPosition.y, window))
        {
            return false;
        }

        float minDepth = player.MinimumSafeDepth;
        float maxDepth = player.MaximumSafeDepth;
        float[] laneOffsets = { -LaneSpacing, LaneSpacing };
        foreach (float offset in laneOffsets)
        {
            float x = playerPosition.x + offset;
            if (x >= -LaneSpacing - 0.1f && x <= LaneSpacing + 0.1f && !IsCellThreatened(x, playerPosition.z, playerPosition.y, window))
            {
                return true;
            }
        }

        float[] depthOffsets = { -DepthStep, DepthStep };
        foreach (float offset in depthOffsets)
        {
            float z = playerPosition.z + offset;
            if (z >= minDepth - 0.05f && z <= maxDepth + 0.05f && !IsCellThreatened(playerPosition.x, z, playerPosition.y, window))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCellThreatened(float x, float z, float playerY, float window)
    {
        var list = ObstacleLaneMarker.Active;
        for (int i = 0; i < list.Count; i++)
        {
            ObstacleLaneMarker marker = list[i];
            if (!marker)
            {
                continue;
            }

            Vector3 p = marker.transform.position;
            if (Mathf.Abs(p.x - x) > 1.3f || Mathf.Abs(p.z - z) > 1.1f)
            {
                continue;
            }

            float above = p.y - playerY;
            float reach = marker.CurrentFallSpeed * window + 1.5f;
            if (above > -1.2f && above < reach)
            {
                return true;
            }
        }

        return false;
    }

    private void TickTelegraph()
    {
        shot.Time += Time.deltaTime;

        // Shooting the gunner during the warning cancels its shot.
        if (!shot.Shooter || !shot.Shooter.isActiveAndEnabled)
        {
            CancelShot();
            return;
        }

        shot.Origin = shot.Shooter.transform.position;
        float t = Mathf.Clamp01(shot.Time / TelegraphSeconds);
        float blink = t > 0.6f && Mathf.Repeat(shot.Time * 14f, 1f) < 0.5f ? 0.35f : 1f;

        aimLine.SetPosition(0, shot.Origin);
        aimLine.SetPosition(1, Vector3.Lerp(shot.Origin, shot.Target, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 2.2f))));
        Color lineColor = new Color(LaserColor.r, LaserColor.g, LaserColor.b, (0.25f + 0.45f * t) * blink);
        aimLine.startColor = lineColor;
        aimLine.endColor = lineColor;

        float ringScale = Mathf.Lerp(3.2f, 1.9f, t);
        targetRing.localScale = Vector3.one * ringScale;
        targetRing.rotation = Quaternion.Euler(0f, shot.Time * 90f, 0f);
        ringMaterial.color = new Color(LaserColor.r, LaserColor.g, LaserColor.b, (0.35f + 0.6f * t) * blink);

        if (shot.Time >= TelegraphSeconds)
        {
            shot.Phase = Phase.Bolt;
            shot.Time = 0f;
            aimLine.enabled = false;
            bolt.enabled = true;
            PlayClip(fireClip, 0.55f);
        }
    }

    private void TickBolt(GameController controller)
    {
        shot.Time += Time.deltaTime;
        float t = Mathf.Clamp01(shot.Time / BoltSeconds);
        Vector3 head = Vector3.Lerp(shot.Origin, shot.Target, t);
        Vector3 tail = Vector3.Lerp(shot.Origin, shot.Target, Mathf.Max(0f, t - 0.35f));
        bolt.SetPosition(0, tail);
        bolt.SetPosition(1, head);
        bolt.startColor = new Color(1f, 0.45f, 0.4f, 0.8f);
        bolt.endColor = new Color(1f, 0.9f, 0.85f, 1f);

        if (t < 1f)
        {
            return;
        }

        Vector3 playerPosition = player.transform.position;
        bool onTarget = Mathf.Abs(playerPosition.x - shot.Target.x) < HitHalfWidth && Mathf.Abs(playerPosition.z - shot.Target.z) < HitHalfDepth;
        Burst(shot.Target, onTarget ? 40 : 14);
        CancelShot();

        if (onTarget && !controller.IsPlayerInvulnerable && !controller.IsGameOver)
        {
            GameFeedbackSignals.RaiseObstacleCollision(new ObstacleCollisionFeedback(playerPosition));
            controller.TriggerGameOver(this);
        }
    }

    private void CancelShot()
    {
        shot = default;
        aimLine.enabled = false;
        bolt.enabled = false;
        targetRing.gameObject.SetActive(false);
    }

    private LineRenderer CreateLine(string name, float startWidth, float endWidth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = startWidth;
        line.endWidth = endWidth;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private void BuildSparks()
    {
        var go = new GameObject("EnemyBoltSparks");
        go.transform.SetParent(transform, false);
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.2f), new Color(1f, 0.85f, 0.6f));
        main.maxParticles = 120;
        var emission = sparks.emission;
        emission.rateOverTime = 0f;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        sparkMaterial = SoftParticleMaterial.Create();
        particleRenderer.sharedMaterial = sparkMaterial;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        sparks.Play();
    }

    private void Burst(Vector3 position, int count)
    {
        sparks.transform.position = position;
        sparks.Emit(count);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip && FeedbackPreferences.IsAudioEnabled(null))
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    // Rising whine while the enemy charges.
    private static AudioClip CreateChargeClip()
    {
        const int rate = 44100;
        int length = (int)(rate * TelegraphSeconds);
        var data = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            phase += Mathf.Lerp(220f, 880f, t * t) / rate;
            data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Min(1f, t * 6f) * 0.35f;
        }

        var clip = AudioClip.Create("EnemyCharge", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Lower, harsher zap than the player's cannon so the two never sound alike.
    private static AudioClip CreateFireClip()
    {
        const int rate = 44100;
        int length = (int)(rate * 0.22f);
        var data = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            phase += Mathf.Lerp(900f, 140f, t) / rate;
            float saw = (phase % 1f) * 2f - 1f;
            data[i] = saw * Mathf.Exp(-t * 6f) * 0.5f;
        }

        var clip = AudioClip.Create("EnemyFire", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
