using UnityEngine;

/// <summary>High damage, slow cadence, piercing shots.</summary>
public sealed class WeaponHeavyRailgun : CombatWeapon
{
    [SerializeField] private float cooldownSeconds = 1.05f;
    [SerializeField] private float projectileSpeed = 55f;
    [SerializeField] private float damage = 70f;

    private float cooldown;

    public override string DisplayName => "RAILGUN";

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
        Vector3 origin = muzzle ? muzzle.position : transform.position + transform.forward * 1.25f;
        Vector3 direction = ResolveAim(origin);

        var slug = new GameObject("RailSlug");
        slug.transform.position = origin;
        slug.transform.rotation = Quaternion.LookRotation(direction);
        slug.transform.localScale = new Vector3(0.18f, 0.18f, 1.1f);

        slug.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Cylinder;
        var renderer = slug.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CombatVfxLibrary.RailMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var collider = slug.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 0.24f;
        collider.height = 2f;
        collider.direction = 2;

        var projectile = slug.AddComponent<CombatProjectile>();
        projectile.Initialize(gameObject, ResolveTeam(), projectileSpeed, new CombatDamage(damage, CombatDamageKind.Kinetic));
        projectile.SetDestroyOnHit(false);
    }

    private static Vector3 ResolveAim(Vector3 origin)
    {
        var target = CombatEnemy.FindNearest(origin + Vector3.forward * 4f, 85f);
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

