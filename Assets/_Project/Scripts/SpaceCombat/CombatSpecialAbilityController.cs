using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatSpecialAbilityController : MonoBehaviour
{
    [SerializeField] private float cooldownDuration = 18f;
    [SerializeField] private float activeDuration = 2.5f;
    [SerializeField] private float shieldRestore = 25f;

    private CombatShipHealth health;
    private float cooldownRemaining;
    private float activeRemaining;

    public float CooldownDuration => cooldownDuration;
    public float CooldownRemaining => cooldownRemaining;
    public bool IsReady => cooldownRemaining <= 0f;
    public bool IsActive => activeRemaining > 0f;

    private void Awake()
    {
        health = GetComponent<CombatShipHealth>();
    }

    private void Update()
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
        activeRemaining = Mathf.Max(0f, activeRemaining - Time.deltaTime);
    }

    public bool Activate()
    {
        if (!health || !health.IsAlive || !IsReady || !GameStateMachine.IsGameplayInputAllowed || RunCountdown.IsCounting)
        {
            return false;
        }

        cooldownRemaining = cooldownDuration;
        activeRemaining = activeDuration;
        health.SetTemporaryInvulnerable(activeDuration);
        health.RestoreShield(shieldRestore);
        GameFeedbackSignals.RaiseAmbientCameraJolt(0.55f);
        return true;
    }
}
