using UnityEngine;

/// <summary>Short-range burst that fires several bolts in a cone.</summary>
public sealed class WeaponSpreadShot : CombatWeapon
{
    [SerializeField] private float cooldownSeconds = 0.55f;
    [SerializeField] private float boltSpeed = 32f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private int pellets = 7;
    [SerializeField] private float spreadDegrees = 12f;

    private float cooldown;

    public override string DisplayName => "SPREAD";

    public override void Tick(bool fireHeld)
    {
        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
        }

        if (!fireHeld || cooldown > 0f)
        {
            return;
        }

        cooldown = Mathf.Max(0.05f, cooldownSeconds);
        Fire();
    }

    private void Fire()
    {
        Vector3 origin = muzzle ? muzzle.position : transform.position + transform.forward * 1.1f;
        Vector3 baseDir = ResolveAim(origin);
        int count = Mathf.Clamp(pellets, 3, 12);
        float spread = Mathf.Clamp(spreadDegrees, 2f, 35f);

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float yaw = Mathf.Lerp(-spread, spread, t);
            float pitch = Mathf.Lerp(-spread * 0.35f, spread * 0.35f, Mathf.PerlinNoise(Time.time, i) );
            Quaternion rot = Quaternion.LookRotation(baseDir) * Quaternion.Euler(pitch, yaw, 0f);

            var bolt = new GameObject("SpreadBolt");
            bolt.transform.position = origin;
            bolt.transform.rotation = rot;
            bolt.transform.localScale = new Vector3(0.16f, 0.16f, 0.42f);

            bolt.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Capsule;
            var renderer = bolt.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CombatVfxLibrary.LaserMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var collider = bolt.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = 0.3f;
            collider.height = 0.9f;
            collider.direction = 2;

            var projectile = bolt.AddComponent<CombatProjectile>();
            projectile.Initialize(gameObject, ResolveTeam(), boltSpeed, new CombatDamage(damage, CombatDamageKind.Energy));
        }
    }

    private static Vector3 ResolveAim(Vector3 origin)
    {
        var target = CombatEnemy.FindNearest(origin + Vector3.forward * 2f, 40f);
        if (!target)
        {
            return Vector3.forward;
        }

        Vector3 to = target.AimPoint - origin;
        if (to.sqrMagnitude < 0.05f)
        {
            return Vector3.forward;
        }

        return to.normalized;
    }
}

