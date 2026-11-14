using UnityEngine;

/// <summary>Fast hitscan-like bolt spam with low per-shot damage.</summary>
public sealed class WeaponLaserCannon : CombatWeapon
{
    [SerializeField] private float fireRate = 10f;
    [SerializeField] private float boltSpeed = 44f;
    [SerializeField] private float damage = 12f;

    private float cooldown;

    public override string DisplayName => "LASER";

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

        cooldown = 1f / Mathf.Max(0.5f, fireRate);
        FireBolt();
    }

    private void FireBolt()
    {
        Vector3 origin = muzzle ? muzzle.position : transform.position + transform.forward * 1.2f;
        Vector3 direction = ResolveAimDirection(origin);

        var bolt = new GameObject("LaserBolt");
        bolt.transform.position = origin;
        bolt.transform.rotation = Quaternion.LookRotation(direction);
        bolt.transform.localScale = new Vector3(0.18f, 0.18f, 0.55f);

        bolt.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Capsule;
        var renderer = bolt.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CombatVfxLibrary.LaserMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var collider = bolt.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 0.35f;
        collider.height = 1.2f;
        collider.direction = 2;

        var projectile = bolt.AddComponent<CombatProjectile>();
        projectile.Initialize(gameObject, ResolveTeam(), boltSpeed, new CombatDamage(damage, CombatDamageKind.Energy));
    }

    private static Vector3 ResolveAimDirection(Vector3 origin)
    {
        var target = CombatEnemy.FindNearest(origin + Vector3.forward * 2f, 55f);
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

