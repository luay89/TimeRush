using UnityEngine;

/// <summary>
/// Slowly rotates the obstacle's visual mesh only, so hazards read as tumbling space rocks
/// instead of static cubes. Purely cosmetic: it lives on the child "Visual" transform, never on
/// the root "Obstacle" GameObject, so the BoxCollider, Rigidbody, ObstacleMover, AutoDestroy and
/// every fairness/reaction-time calculation in ObstacleSpawner keep reading the same unrotated
/// root transform and axis-aligned collider bounds they always have. This script is never read
/// by any other system -- it only spins a mesh.
/// </summary>
public sealed class ObstacleTumble : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = new Vector3(0.4f, 1f, 0.2f);
    [SerializeField, Range(5f, 60f)] private float degreesPerSecond = 24f;

    private void OnEnable()
    {
        // Randomized only for cosmetic variety between spawned instances -- never consulted by
        // gameplay, fairness, or pattern logic, so it cannot introduce any non-determinism there.
        rotationAxis = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
        if (rotationAxis.sqrMagnitude < 0.0001f)
        {
            rotationAxis = Vector3.up;
        }
        rotationAxis.Normalize();
        degreesPerSecond = Random.Range(14f, 42f);
    }

    private void Update()
    {
        transform.Rotate(rotationAxis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
