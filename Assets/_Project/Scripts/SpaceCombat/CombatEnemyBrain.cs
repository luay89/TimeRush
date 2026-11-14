using UnityEngine;

/// <summary>
/// Simple, mobile-friendly enemy AI: approaches the player, strafes, and fires based on archetype.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatEnemyBrain : MonoBehaviour
{
    [SerializeField] private CombatEnemyArchetype archetype = CombatEnemyArchetype.HeavyFighter;
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float strafeAmplitude = 3f;
    [SerializeField] private float strafeFrequency = 0.7f;
    [SerializeField] private float preferredRange = 10f;
    [SerializeField] private float aimLead = 0.2f;

    private CombatPlayerController player;
    private CombatEnemyWeapon weapon;
    private float seed;
    private Transform visual;

    public CombatEnemyArchetype Archetype => archetype;

    public void Configure(CombatEnemyArchetype type, CombatPlayerController playerController)
    {
        archetype = type;
        player = playerController;
        ApplyArchetypeTuning();
    }

    private void Awake()
    {
        seed = Random.value * 1000f;
        visual = transform.Find("Visual");
        weapon = GetComponent<CombatEnemyWeapon>();
        if (!weapon)
        {
            weapon = gameObject.AddComponent<CombatEnemyWeapon>();
        }

        ApplyArchetypeTuning();
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

        TickMovement();
        TickWeapon();
        TickVisual();
    }

    private void TickMovement()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 toPlayer = playerPos - transform.position;

        // Keep in front of the player (positive Z) so aiming is readable.
        float desiredZ = Mathf.Max(playerPos.z + preferredRange, transform.position.z);
        float z = Mathf.Lerp(transform.position.z, desiredZ, 0.5f * Time.deltaTime);

        // Strafe around the player.
        float strafe = Mathf.Sin((Time.time + seed) * strafeFrequency) * strafeAmplitude;
        float rise = Mathf.Cos((Time.time + seed) * (strafeFrequency * 0.6f)) * (strafeAmplitude * 0.35f);
        Vector3 target = new Vector3(playerPos.x + strafe, playerPos.y + rise, z);

        // Archetype-specific movement quirks.
        if (archetype == CombatEnemyArchetype.FastInterceptor)
        {
            target += (toPlayer.normalized * -2.5f);
        }
        else if (archetype == CombatEnemyArchetype.ArmoredDestroyer)
        {
            target = Vector3.Lerp(target, new Vector3(playerPos.x, playerPos.y, z), 0.4f);
        }
        else if (archetype == CombatEnemyArchetype.LongRangeShooter)
        {
            target.z = playerPos.z + Mathf.Max(preferredRange, 16f);
        }

        transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(0.1f, moveSpeed) * Time.deltaTime);

        // Face the player.
        Vector3 aimTarget = playerPos + player.Velocity * aimLead;
        Vector3 forward = (aimTarget - transform.position);
        if (forward.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward.normalized), 4f * Time.deltaTime);
        }
    }

    private void TickWeapon()
    {
        if (!weapon)
        {
            return;
        }

        weapon.SetTarget(player);
        weapon.Tick(archetype);
    }

    private void TickVisual()
    {
        if (!visual)
        {
            return;
        }

        float roll = Mathf.Sin((Time.time + seed) * 1.3f) * 10f;
        float pitch = Mathf.Cos((Time.time + seed) * 1.1f) * 6f;
        visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(pitch, 0f, roll), 6f * Time.deltaTime);
    }

    private void ApplyArchetypeTuning()
    {
        switch (archetype)
        {
            case CombatEnemyArchetype.FastInterceptor:
                moveSpeed = 11.5f;
                strafeAmplitude = 4.6f;
                strafeFrequency = 1.6f;
                preferredRange = 8.5f;
                aimLead = 0.35f;
                break;
            case CombatEnemyArchetype.HeavyFighter:
                moveSpeed = 7.5f;
                strafeAmplitude = 2.4f;
                strafeFrequency = 0.9f;
                preferredRange = 11f;
                aimLead = 0.25f;
                break;
            case CombatEnemyArchetype.LongRangeShooter:
                moveSpeed = 6.2f;
                strafeAmplitude = 3.1f;
                strafeFrequency = 0.7f;
                preferredRange = 18f;
                aimLead = 0.15f;
                break;
            case CombatEnemyArchetype.ArmoredDestroyer:
                moveSpeed = 4.6f;
                strafeAmplitude = 1.6f;
                strafeFrequency = 0.55f;
                preferredRange = 14f;
                aimLead = 0.12f;
                break;
            case CombatEnemyArchetype.Elite:
                moveSpeed = 9.2f;
                strafeAmplitude = 3.4f;
                strafeFrequency = 1.1f;
                preferredRange = 12.5f;
                aimLead = 0.3f;
                break;
        }
    }
}

