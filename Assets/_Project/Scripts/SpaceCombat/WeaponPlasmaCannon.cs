using UnityEngine;

/// <summary>Slow, heavy plasma balls with splash damage.</summary>
public sealed class WeaponPlasmaCannon : CombatWeapon
{
    [SerializeField] private float cooldownSeconds = 0.6f;
    [SerializeField] private float projectileSpeed = 28f;
    [SerializeField] private float damage = 26f;
    [SerializeField] private float splashRadius = 2.2f;

    private float cooldown;

    public override string DisplayName => "PLASMA";

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
        Vector3 direction = ResolveAim(origin);

        var plasma = new GameObject("PlasmaBall");
        plasma.transform.position = origin;
        plasma.transform.rotation = Quaternion.LookRotation(direction);
        plasma.transform.localScale = Vector3.one * 0.46f;

        plasma.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Sphere;
        var renderer = plasma.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CombatVfxLibrary.PlasmaMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var collider = plasma.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.55f;

        var projectile = plasma.AddComponent<CombatProjectile>();
        projectile.Initialize(gameObject, ResolveTeam(), projectileSpeed, new CombatDamage(damage, CombatDamageKind.Energy));
        projectile.ConfigureSplash(splashRadius);
    }

    private static Vector3 ResolveAim(Vector3 origin)
    {
        var target = CombatEnemy.FindNearest(origin + Vector3.forward * 3f, 45f);
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

