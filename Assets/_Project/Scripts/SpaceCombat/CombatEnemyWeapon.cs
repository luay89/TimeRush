using UnityEngine;

/// <summary>Enemy weapon logic with archetype-driven patterns.</summary>
[DisallowMultipleComponent]
public sealed class CombatEnemyWeapon : MonoBehaviour
{
    [SerializeField] private float baseCooldown = 0.8f;
    [SerializeField] private float projectileSpeed = 26f;
    [SerializeField] private float damage = 12f;

    private float cooldown;
    private CombatPlayerController target;
    private Transform muzzle;
    private CombatTeamMember team;

    private void Awake()
    {
        team = GetComponent<CombatTeamMember>();
        if (!muzzle)
        {
            var go = new GameObject("Muzzle");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 1.4f);
            muzzle = go.transform;
        }
    }

    public void SetTarget(CombatPlayerController player)
    {
        target = player;
    }

    public void Tick(CombatEnemyArchetype archetype)
    {
        if (!target || !GameStateMachine.IsGameplayInputAllowed || RunCountdown.IsCounting)
        {
            return;
        }

        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
            return;
        }

        float range = Vector3.Distance(transform.position, target.transform.position);
        if (range > 45f)
        {
            cooldown = 0.25f;
            return;
        }

        FirePattern(archetype);
    }

    private void FirePattern(CombatEnemyArchetype archetype)
    {
        switch (archetype)
        {
            case CombatEnemyArchetype.FastInterceptor:
                baseCooldown = 0.65f;
                projectileSpeed = 30f;
                damage = 10f;
                FireBurst(2, 6f);
                cooldown = baseCooldown;
                break;
            case CombatEnemyArchetype.HeavyFighter:
                baseCooldown = 0.95f;
                projectileSpeed = 24f;
                damage = 16f;
                FireSingle(0f);
                cooldown = baseCooldown;
                break;
            case CombatEnemyArchetype.LongRangeShooter:
                baseCooldown = 1.25f;
                projectileSpeed = 32f;
                damage = 14f;
                FireSingle(0f);
                cooldown = baseCooldown;
                break;
            case CombatEnemyArchetype.ArmoredDestroyer:
                baseCooldown = 1.35f;
                projectileSpeed = 20f;
                damage = 22f;
                FireSpread(3, 10f);
                cooldown = baseCooldown;
                break;
            case CombatEnemyArchetype.Elite:
                baseCooldown = 0.7f;
                projectileSpeed = 28f;
                damage = 16f;
                FireBurst(3, 8f);
                cooldown = baseCooldown;
                break;
            default:
                FireSingle(0f);
                cooldown = 1f;
                break;
        }
    }

    private void FireSingle(float yaw)
    {
        FireBolt(yaw, 0f);
    }

    private void FireBurst(int shots, float yawJitter)
    {
        int count = Mathf.Clamp(shots, 1, 5);
        for (int i = 0; i < count; i++)
        {
            float jitter = (Random.value - 0.5f) * yawJitter;
            FireBolt(jitter, (Random.value - 0.5f) * (yawJitter * 0.35f));
        }
    }

    private void FireSpread(int shots, float spread)
    {
        int count = Mathf.Clamp(shots, 2, 6);
        float s = Mathf.Clamp(spread, 2f, 25f);
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float yaw = Mathf.Lerp(-s, s, t);
            FireBolt(yaw, 0f);
        }
    }

    private void FireBolt(float yaw, float pitch)
    {
        Vector3 origin = muzzle ? muzzle.position : transform.position + transform.forward * 1.2f;
        Vector3 direction = (target.transform.position + target.Velocity * 0.2f) - origin;
        if (direction.sqrMagnitude < 0.2f)
        {
            direction = transform.forward;
        }

        Quaternion rot = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(pitch, yaw, 0f);

        var bolt = new GameObject("EnemyBolt");
        bolt.transform.position = origin;
        bolt.transform.rotation = rot;
        bolt.transform.localScale = new Vector3(0.2f, 0.2f, 0.6f);
        bolt.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Capsule;
        var renderer = bolt.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CombatVfxLibrary.PlasmaMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var collider = bolt.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 0.35f;
        collider.height = 1.2f;
        collider.direction = 2;

        var projectile = bolt.AddComponent<CombatProjectile>();
        projectile.Initialize(gameObject, team ? team.Team : CombatTeam.Enemy, projectileSpeed, new CombatDamage(damage, CombatDamageKind.Energy));
    }
}

