using UnityEngine;

/// <summary>
/// New free 3D ship movement for space combat.
///
/// Movement model:
/// - Player input requests a local-space strafe vector (X/Y/Z).
/// - The ship accelerates smoothly toward the requested velocity.
/// - A constant forward drift sells motion even when the player is idle.
/// - A soft radial clamp keeps combat readable on mobile.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatPlayerController : MonoBehaviour
{
    private GameBalanceConfig balance;
    private Vector3 velocity;
    private Vector3 requestedMove;
    private Transform visual;
    private float rollVelocity;

    public Vector3 Velocity => velocity;
    public Vector3 RequestedMove => requestedMove;
    public float PlayAreaRadius => balance ? balance.combatPlayAreaRadius : 14f;

    public void Configure(GameBalanceConfig config)
    {
        balance = config;
    }

    public void SetRequestedMove(Vector3 move)
    {
        requestedMove = Vector3.ClampMagnitude(move, 1f);
    }

    private void Awake()
    {
        visual = transform.Find("Visual");
    }

    private void Update()
    {
        if (!GameStateMachine.IsGameplayInputAllowed || RunCountdown.IsCounting)
        {
            requestedMove = Vector3.zero;
        }

        TickMovement();
        TickVisual();
    }

    private void TickMovement()
    {
        float moveSpeed = balance ? balance.combatPlayerMoveSpeed : 11f;
        float accel = balance ? balance.combatPlayerAcceleration : 14f;
        float forward = balance ? balance.combatForwardSpeed : 6f;

        Vector3 desiredVelocity = requestedMove * moveSpeed + Vector3.forward * forward;
        velocity = Vector3.MoveTowards(velocity, desiredVelocity, Mathf.Max(0.1f, accel) * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;

        // Soft clamp into a readable mobile-friendly volume.
        float radius = Mathf.Max(4f, PlayAreaRadius);
        Vector3 position = transform.position;

        Vector2 xy = new Vector2(position.x, position.y);
        if (xy.sqrMagnitude > radius * radius)
        {
            xy = xy.normalized * radius;
            position.x = xy.x;
            position.y = xy.y;
            transform.position = position;
            velocity.x *= 0.6f;
            velocity.y *= 0.6f;
        }

        position.z = Mathf.Clamp(position.z, -radius, radius);
        transform.position = position;
    }

    private void TickVisual()
    {
        if (!visual)
        {
            return;
        }

        float targetRoll = -requestedMove.x * 22f;
        float targetPitch = requestedMove.y * 10f;
        rollVelocity = Mathf.Lerp(rollVelocity, targetRoll, 10f * Time.deltaTime);
        Quaternion targetRotation = Quaternion.Euler(targetPitch, 0f, rollVelocity);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, 12f * Time.deltaTime);
    }
}

