using System;
using UnityEngine;

/// <summary>
/// Health + shield for both player and enemies in the new combat loop.
/// Uses simple, predictable rules: shields absorb first, then health.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatShipHealth : MonoBehaviour
{
    [Header("Vitals")]
    [SerializeField] private float maxHealth = 120f;
    [SerializeField] private float maxShield = 60f;

    [Header("Shield Regen")]
    [SerializeField] private float shieldRegenDelaySeconds = 2.2f;
    [SerializeField] private float shieldRegenPerSecond = 18f;

    public float Health => health;
    public float Shield => shield;
    public float MaxHealth => maxHealth;
    public float MaxShield => maxShield;
    public bool IsAlive => health > 0f;

    public event Action<CombatShipHealth, CombatDamage> Damaged;
    public event Action<CombatShipHealth> Died;
    public event Action<CombatShipHealth> Changed;

    private float health;
    private float shield;
    private float shieldRegenCooldown;
    private float damageImmunityRemaining;
    private CombatTeamMember team;
    private bool deathRaised;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        maxShield = Mathf.Max(0f, maxShield);
        health = maxHealth;
        shield = maxShield;
        team = GetComponent<CombatTeamMember>();
    }

    private void Update()
    {
        damageImmunityRemaining = Mathf.Max(0f, damageImmunityRemaining - Time.deltaTime);

        if (!IsAlive)
        {
            return;
        }

        if (maxShield <= 0f)
        {
            return;
        }

        if (shieldRegenCooldown > 0f)
        {
            shieldRegenCooldown = Mathf.Max(0f, shieldRegenCooldown - Time.deltaTime);
            return;
        }

        if (shield >= maxShield || shieldRegenPerSecond <= 0f)
        {
            return;
        }

        shield = Mathf.Min(maxShield, shield + shieldRegenPerSecond * Time.deltaTime);
        Changed?.Invoke(this);
    }

    public void ConfigureDefaults(CombatTeam initialTeam)
    {
        maxHealth = initialTeam == CombatTeam.Player ? 140f : 90f;
        maxShield = initialTeam == CombatTeam.Player ? 80f : 25f;
        shieldRegenDelaySeconds = initialTeam == CombatTeam.Player ? 2f : 3.2f;
        shieldRegenPerSecond = initialTeam == CombatTeam.Player ? 22f : 12f;
        health = maxHealth;
        shield = maxShield;
        team = GetComponent<CombatTeamMember>();
        Changed?.Invoke(this);
    }

    public void SetMaxVitals(float healthMax, float shieldMax, bool refill)
    {
        maxHealth = Mathf.Max(1f, healthMax);
        maxShield = Mathf.Max(0f, shieldMax);

        if (refill)
        {
            health = maxHealth;
            shield = maxShield;
        }
        else
        {
            health = Mathf.Clamp(health, 0f, maxHealth);
            shield = Mathf.Clamp(shield, 0f, maxShield);
        }

        Changed?.Invoke(this);
    }

    public bool IsInvulnerable => damageImmunityRemaining > 0f;

    public void SetTemporaryInvulnerable(float seconds)
    {
        damageImmunityRemaining = Mathf.Max(damageImmunityRemaining, Mathf.Max(0f, seconds));
    }

    public void RestoreShield(float amount)
    {
        float restored = Mathf.Max(0f, amount);
        if (restored <= 0f || shield >= maxShield)
        {
            return;
        }

        shield = Mathf.Min(maxShield, shield + restored);
        Changed?.Invoke(this);
    }

    public bool TryApplyDamage(CombatDamage damage, GameObject instigator)
    {
        bool invulnerableForRun = team && team.Team == CombatTeam.Player && GameController.Instance && GameController.Instance.IsPlayerInvulnerable;
        if (!IsAlive || IsInvulnerable || invulnerableForRun)
        {
            return false;
        }

        float amount = Mathf.Max(0f, damage.Amount);
        if (amount <= 0f)
        {
            return false;
        }

        shieldRegenCooldown = Mathf.Max(shieldRegenCooldown, shieldRegenDelaySeconds);

        if (shield > 0f)
        {
            float absorbed = Mathf.Min(shield, amount);
            shield -= absorbed;
            amount -= absorbed;
        }

        if (amount > 0f)
        {
            health = Mathf.Max(0f, health - amount);
        }

        Damaged?.Invoke(this, damage);
        Changed?.Invoke(this);

        if (health <= 0f)
        {
            RaiseDeath(instigator);
        }

        return true;
    }

    private void RaiseDeath(GameObject instigator)
    {
        if (deathRaised)
        {
            return;
        }

        deathRaised = true;
        Died?.Invoke(this);

        // Player death goes through the existing run Results flow.
        if (team && team.Team == CombatTeam.Player)
        {
            var gc = GameController.Instance;
            if (gc && !gc.IsGameOver)
            {
                gc.TriggerGameOver(this);
            }
        }
        else
        {
            if (CombatCampaignSession.IsActive)
            {
                CombatCampaignSession.RegisterKill();
            }

            CombatExplosionEffect.Spawn(transform.position, 1.15f);
            Destroy(gameObject);
        }
    }
}
