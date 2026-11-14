using UnityEngine;

/// <summary>
/// Generic projectile for space combat (player and enemies).
/// Uses trigger collisions against <see cref="CombatShipHealth"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CombatProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 26f;
    [SerializeField] private float lifetimeSeconds = 4f;
    [SerializeField] private float hitRadius = 0.35f;
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private CombatDamageKind damageKind = CombatDamageKind.Energy;
    [SerializeField] private float damageAmount = 18f;
    [SerializeField] private float splashRadius;
    [SerializeField] private float splashFalloff = 0.55f;

    private CombatTeam sourceTeam;
    private GameObject instigator;
    private float age;

    public void Initialize(GameObject instigator, CombatTeam team, float speed, CombatDamage damage)
    {
        this.instigator = instigator;
        sourceTeam = team;
        this.speed = speed;
        damageKind = damage.Kind;
        damageAmount = damage.Amount;
    }

    public void ConfigureSplash(float radius)
    {
        splashRadius = Mathf.Max(0f, radius);
    }

    public void SetDestroyOnHit(bool value)
    {
        destroyOnHit = value;
    }

    private void Awake()
    {
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;

        // Triggers need a Rigidbody somewhere in the collision pair; keep it lightweight.
        var body = GetComponent<Rigidbody>();
        if (!body)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        lifetimeSeconds = Mathf.Max(0.1f, lifetimeSeconds);
        speed = Mathf.Max(0.1f, speed);
    }

    private void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
        age += Time.deltaTime;
        if (age >= lifetimeSeconds)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other)
        {
            return;
        }

        if (!other.TryGetComponent<CombatShipHealth>(out var health) || !health.IsAlive)
        {
            return;
        }

        // Avoid friendly fire by default.
        if (other.TryGetComponent<CombatTeamMember>(out var otherTeam) && otherTeam.Team == sourceTeam)
        {
            return;
        }

        // Ignore self-collisions.
        if (instigator && other.gameObject == instigator)
        {
            return;
        }

        ApplyDamageAt(other.transform.position, health);

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyDamageAt(Vector3 impactPoint, CombatShipHealth primary)
    {
        primary.TryApplyDamage(new CombatDamage(damageAmount, damageKind), instigator);

        if (splashRadius <= 0.05f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(impactPoint, splashRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (!hit || hit == primary)
            {
                continue;
            }

            if (!hit.TryGetComponent<CombatShipHealth>(out var health) || !health.IsAlive)
            {
                continue;
            }

            if (hit.TryGetComponent<CombatTeamMember>(out var team) && team.Team == sourceTeam)
            {
                continue;
            }

            float distance = Vector3.Distance(impactPoint, hit.transform.position);
            float t = Mathf.Clamp01(distance / Mathf.Max(0.01f, splashRadius));
            float scaled = Mathf.Lerp(1f, splashFalloff, t);
            health.TryApplyDamage(new CombatDamage(damageAmount * scaled, damageKind), instigator);
        }
    }
}

