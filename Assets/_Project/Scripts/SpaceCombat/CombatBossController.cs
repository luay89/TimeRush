using UnityEngine;

/// <summary>
/// First boss implementation: Leviathan-class dreadnought.
/// Multiple phases based on remaining health.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatBossController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.4f;
    [SerializeField] private float hoverAmplitude = 1.2f;
    [SerializeField] private float hoverFrequency = 0.55f;
    [SerializeField] private float preferredRange = 22f;

    [Header("Weapons")]
    [SerializeField] private float cannonCooldown = 0.9f;
    [SerializeField] private float spreadCooldown = 2.2f;
    [SerializeField] private float missileCooldown = 3.6f;

    private CombatPlayerController player;
    private CombatShipHealth health;
    private CombatTeamMember team;
    private float seed;

    private float cannonTimer;
    private float spreadTimer;
    private float missileTimer;
    private Transform muzzleLeft;
    private Transform muzzleRight;
    private Transform missileBay;

    private int phase;

    public void Configure(CombatPlayerController playerController)
    {
        player = playerController;
    }

    private void Awake()
    {
        seed = Random.value * 1000f;
        health = GetComponent<CombatShipHealth>();
        team = GetComponent<CombatTeamMember>();
        EnsureMuzzles();
    }

    private void Update()
    {
        if (!player)
        {
            player = FindObjectOfType<CombatPlayerController>();
            if (!player)
            {
                return;
            }
        }

        if (!health || !health.IsAlive)
        {
            return;
        }

        TickPhase();
        TickMovement();
        TickWeapons();
    }

    private void TickPhase()
    {
        if (!health)
        {
            phase = 0;
            return;
        }

        float pct = Mathf.Clamp01(health.Health / Mathf.Max(1f, health.MaxHealth));
        int next = pct > 0.66f ? 1 : pct > 0.33f ? 2 : 3;
        if (next != phase)
        {
            phase = next;
            CombatAnnouncer.Announce("BOSS PHASE", "PHASE " + phase, new Color(1f, 0.6f, 0.15f, 1f), -1);
            GameFeedbackSignals.RaiseAmbientCameraJolt(0.9f);
        }
    }

    private void TickMovement()
    {
        Vector3 p = player.transform.position;
        float z = p.z + preferredRange;
        float hover = Mathf.Sin((Time.time + seed) * hoverFrequency) * hoverAmplitude;
        Vector3 target = new Vector3(p.x * 0.55f, p.y * 0.35f + hover + 2.2f, z);
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        Vector3 forward = (p - transform.position);
        if (forward.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward.normalized), 1.8f * Time.deltaTime);
        }
    }

    private void TickWeapons()
    {
        if (!GameStateMachine.IsGameplayInputAllowed || RunCountdown.IsCounting)
        {
            return;
        }

        cannonTimer -= Time.deltaTime;
        spreadTimer -= Time.deltaTime;
        missileTimer -= Time.deltaTime;

        if (cannonTimer <= 0f)
        {
            FireCannons();
            cannonTimer = Mathf.Max(0.15f, cannonCooldown) / (phase == 3 ? 0.75f : 1f);
        }

        if (phase >= 2 && spreadTimer <= 0f)
        {
            FireSpread();
            spreadTimer = Mathf.Max(0.4f, spreadCooldown);
        }

        if (phase >= 3 && missileTimer <= 0f)
        {
            FireMissileVolley();
            missileTimer = Mathf.Max(0.8f, missileCooldown);
        }
    }

    private void FireCannons()
    {
        FireBolt(muzzleLeft.position, 22f, 16f);
        FireBolt(muzzleRight.position, 22f, 16f);
    }

    private void FireSpread()
    {
        Vector3 origin = missileBay.position;
        int shots = phase == 3 ? 9 : 6;
        float spread = phase == 3 ? 24f : 16f;
        for (int i = 0; i < shots; i++)
        {
            float t = shots == 1 ? 0.5f : i / (float)(shots - 1);
            float yaw = Mathf.Lerp(-spread, spread, t);
            float pitch = Mathf.Lerp(-6f, 6f, Mathf.PerlinNoise(Time.time, i));
            FireBolt(origin, 20f, 12f, yaw, pitch);
        }
    }

    private void FireMissileVolley()
    {
        Vector3 origin = missileBay.position;
        int missiles = 3;
        for (int i = 0; i < missiles; i++)
        {
            float yaw = (i - 1) * 12f;
            FireHoming(origin, 18f, 18f, yaw);
        }
    }

    private void FireBolt(Vector3 origin, float speed, float damage, float yaw = 0f, float pitch = 0f)
    {
        Vector3 direction = (player.transform.position + player.Velocity * 0.2f) - origin;
        if (direction.sqrMagnitude < 0.25f)
        {
            direction = transform.forward;
        }

        Quaternion rot = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(pitch, yaw, 0f);
        var bolt = new GameObject("BossBolt");
        bolt.transform.position = origin;
        bolt.transform.rotation = rot;
        bolt.transform.localScale = new Vector3(0.28f, 0.28f, 0.9f);
        bolt.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Capsule;
        var renderer = bolt.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CombatVfxLibrary.RailMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var collider = bolt.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 0.42f;
        collider.height = 1.7f;
        collider.direction = 2;
        var proj = bolt.AddComponent<CombatProjectile>();
        proj.Initialize(gameObject, team ? team.Team : CombatTeam.Enemy, speed, new CombatDamage(damage, CombatDamageKind.Energy));
    }

    private void FireHoming(Vector3 origin, float speed, float damage, float yaw)
    {
        Vector3 direction = (player.transform.position - origin);
        if (direction.sqrMagnitude < 0.25f)
        {
            direction = transform.forward;
        }

        Quaternion rot = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(0f, yaw, 0f);
        var missile = new GameObject("BossMissile");
        missile.transform.position = origin;
        missile.transform.rotation = rot;
        missile.transform.localScale = new Vector3(0.3f, 0.3f, 1.0f);
        missile.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Cylinder;
        var renderer = missile.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CombatVfxLibrary.MissileMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var collider = missile.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 0.33f;
        collider.height = 1.8f;
        collider.direction = 2;
        var guidance = missile.AddComponent<CombatHomingGuidance>();
        guidance.SetTarget(player.transform);
        var proj = missile.AddComponent<CombatProjectile>();
        proj.Initialize(gameObject, team ? team.Team : CombatTeam.Enemy, speed, new CombatDamage(damage, CombatDamageKind.Explosive));
        proj.ConfigureSplash(2.4f);
    }

    private void EnsureMuzzles()
    {
        muzzleLeft = CreateMuzzle("MuzzleLeft", new Vector3(-2.1f, 0.35f, -0.6f));
        muzzleRight = CreateMuzzle("MuzzleRight", new Vector3(2.1f, 0.35f, -0.6f));
        missileBay = CreateMuzzle("MissileBay", new Vector3(0f, 0.15f, -0.2f));
    }

    private Transform CreateMuzzle(string name, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }
}

