using UnityEngine;

/// <summary>
/// Independent, lightweight spawner for gameplay pickups (currently: a shield power-up).
/// Deliberately kept separate from ObstacleSpawner's fairness-tested pattern system --
/// it only adds occasional bonus pickups on its own simple timer and never touches obstacle
/// placement, difficulty, or scoring.
/// </summary>
public sealed class PowerUpSpawner : MonoBehaviour
{
    [SerializeField] private float[] lanePositions = { -2.5f, 0f, 2.5f };
    [SerializeField] private float spawnHeight = 13.5f;
    [SerializeField] private float fallSpeed = 6f;
    [SerializeField] private float minIntervalSeconds = 14f;
    [SerializeField] private float maxIntervalSeconds = 22f;
    [SerializeField] private float shieldDurationSeconds = 3f;
    [SerializeField] private float pickupRadius = 0.7f;
    [SerializeField] private float pickupLifetimeSeconds = 10f;
    [SerializeField] private Color pickupColor = new Color(0.4f, 0.9f, 1f, 1f);

    private float nextSpawnTime;

    private void OnEnable()
    {
        ScheduleNext();
    }

    private void Update()
    {
        if (!GameStateMachine.IsGameplayInputAllowed)
        {
            return;
        }

        if (Time.time < nextSpawnTime)
        {
            return;
        }

        SpawnPickup();
        ScheduleNext();
    }

    private void ScheduleNext()
    {
        nextSpawnTime = Time.time + Random.Range(minIntervalSeconds, maxIntervalSeconds);
    }

    private void SpawnPickup()
    {
        if (lanePositions == null || lanePositions.Length == 0)
        {
            return;
        }

        int lane = Random.Range(0, lanePositions.Length);
        Vector3 spawnPos = new Vector3(lanePositions[lane], spawnHeight, transform.position.z);

        GameObject pickupGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickupGO.name = "ShieldPickup";
        pickupGO.transform.position = spawnPos;
        pickupGO.transform.localScale = Vector3.one * 0.9f;

        SphereCollider sphereCollider = pickupGO.GetComponent<SphereCollider>();
        if (sphereCollider)
        {
            sphereCollider.isTrigger = true;
            sphereCollider.radius = pickupRadius;
        }

        Renderer pickupRenderer = pickupGO.GetComponent<Renderer>();
        if (pickupRenderer)
        {
            Material material = new Material(pickupRenderer.sharedMaterial);
            if (material.HasProperty("_Color"))
            {
                material.color = pickupColor;
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", pickupColor * 1.6f);
            }

            pickupRenderer.material = material;
        }

        ObstacleMover mover = pickupGO.AddComponent<ObstacleMover>();
        mover.SetSpeed(fallSpeed);

        PowerUpPickup pickup = pickupGO.AddComponent<PowerUpPickup>();
        pickup.Configure(shieldDurationSeconds);

        AutoDestroy autoDestroy = pickupGO.AddComponent<AutoDestroy>();
        autoDestroy.SetLifetime(pickupLifetimeSeconds);
    }
}
