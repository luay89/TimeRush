using UnityEngine;

/// <summary>Fires slow homing missiles that chase the nearest enemy.</summary>
public sealed class WeaponHomingMissiles : CombatWeapon
{
    [SerializeField] private float cooldownSeconds = 1.35f;
    [SerializeField] private float missileSpeed = 20f;
    [SerializeField] private float damage = 40f;
    [SerializeField] private float splashRadius = 2.6f;
    [SerializeField] private int magazineCapacity = 6;
    [SerializeField] private float reloadInterval = 2.2f;

    private float cooldown;
    private float reloadTimer;
    private int ammoInMagazine;

    public override string DisplayName => "MISSILES";
    public int AmmoInMagazine => ammoInMagazine;
    public int MagazineCapacity => magazineCapacity;
    public float ReloadRemaining => ammoInMagazine < magazineCapacity ? Mathf.Max(0f, reloadTimer) : 0f;

    private void Awake()
    {
        magazineCapacity = Mathf.Max(1, magazineCapacity);
        ammoInMagazine = magazineCapacity;
    }

    private void Update()
    {
        if (ammoInMagazine >= magazineCapacity)
        {
            return;
        }

        reloadTimer -= Time.deltaTime;
        if (reloadTimer <= 0f)
        {
            ammoInMagazine++;
            reloadTimer = ammoInMagazine < magazineCapacity ? Mathf.Max(0.1f, reloadInterval) : 0f;
        }
    }

    public override void Tick(bool fireHeld)
    {
        if (cooldown > 0f)
        {
            cooldown -= Time.deltaTime;
        }

        if (!fireHeld || cooldown > 0f || ammoInMagazine <= 0)
        {
            return;
        }

        ammoInMagazine--;
        if (ammoInMagazine < magazineCapacity && reloadTimer <= 0f)
        {
            reloadTimer = Mathf.Max(0.1f, reloadInterval);
        }

        cooldown = Mathf.Max(0.05f, cooldownSeconds);
        Fire();
    }

    private void Fire()
    {
        Vector3 origin = muzzle ? muzzle.position : transform.position + transform.forward * 1.15f;
        var target = CombatEnemy.FindNearest(origin + Vector3.forward * 2f, 70f);
        Vector3 direction = target ? (target.AimPoint - origin).normalized : Vector3.forward;
        if (direction.sqrMagnitude < 0.02f)
        {
            direction = Vector3.forward;
        }

        var missile = new GameObject("HomingMissile");
        missile.transform.position = origin;
        missile.transform.rotation = Quaternion.LookRotation(direction);
        missile.transform.localScale = new Vector3(0.24f, 0.24f, 0.7f);

        missile.AddComponent<MeshFilter>().sharedMesh = CombatPrimitiveCache.Cylinder;
        var renderer = missile.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CombatVfxLibrary.MissileMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var collider = missile.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 0.28f;
        collider.height = 1.2f;
        collider.direction = 2;

        var guidance = missile.AddComponent<CombatHomingGuidance>();
        guidance.SetTarget(target ? target.transform : null);

        var projectile = missile.AddComponent<CombatProjectile>();
        projectile.Initialize(gameObject, ResolveTeam(), missileSpeed, new CombatDamage(damage, CombatDamageKind.Explosive));
        projectile.ConfigureSplash(splashRadius);
    }
}
