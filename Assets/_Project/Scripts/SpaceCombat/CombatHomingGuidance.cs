using UnityEngine;

/// <summary>Steers a projectile toward a target transform.</summary>
[DisallowMultipleComponent]
public sealed class CombatHomingGuidance : MonoBehaviour
{
    [SerializeField] private float turnRate = 220f;
    [SerializeField] private float acquireRange = 35f;
    [SerializeField] private float wobble = 0.35f;

    private Transform target;
    private float seed;

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    private void Awake()
    {
        seed = Random.value * 100f;
    }

    private void Update()
    {
        if (!target)
        {
            target = CombatEnemy.FindNearest(transform.position, acquireRange)?.transform;
            if (!target)
            {
                return;
            }
        }

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude < 0.01f)
        {
            return;
        }

        Vector3 desired = toTarget.normalized;
        desired += new Vector3(
            Mathf.PerlinNoise(seed, Time.time * 2.4f) - 0.5f,
            Mathf.PerlinNoise(seed + 13.7f, Time.time * 2.1f) - 0.5f,
            0f) * wobble;
        desired.Normalize();

        Quaternion desiredRot = Quaternion.LookRotation(desired);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, turnRate * Time.deltaTime);
    }
}

