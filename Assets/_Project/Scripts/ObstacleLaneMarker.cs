using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleLaneMarker : MonoBehaviour
{
    [SerializeField] private float visualSpinDegreesPerSecond = 72f;
    [SerializeField, Range(0.5f, 1f)] private float farVisualScale = 0.58f;
    [SerializeField, Range(1f, 1.4f)] private float nearVisualScale = 1.18f;
    [SerializeField] private float dangerHeight = 4.5f;

    public int LaneIndex { get; private set; } = -1;

    private Transform visual;
    private ObstacleSpawner owner;
    private bool registered;
    private bool initialized;
    private bool facetsBuilt;
    private float spawnHeight = 13.5f;
    private Vector3 baseVisualScale = Vector3.one;

    public float CurrentHeight => transform.position.y;
    public float CurrentDepth => transform.position.z;
    public float CurrentFallSpeed { get; private set; }

    public void Initialize(ObstacleSpawner spawner, int laneIndex, float initialSpawnHeight, float fallSpeed)
    {
        owner = spawner;
        LaneIndex = laneIndex;
        spawnHeight = Mathf.Max(dangerHeight + 0.1f, initialSpawnHeight);
        CurrentFallSpeed = Mathf.Max(0.1f, fallSpeed);
        if (LaneIndex < 0)
        {
            return;
        }
        initialized = true;

        if (!isActiveAndEnabled)
        {
            return;
        }

        Register();
    }

    private void OnEnable()
    {
        if (!visual)
        {
            visual = transform.Find("Visual");
        }

        if (visual)
        {
            baseVisualScale = visual.localScale;
            BuildFacetsIfNeeded();
        }

        if (initialized)
        {
            Register();
        }
    }

    // Adds a few small, collider-less "shard" cubes onto the obstacle's own visual so its
    // silhouette reads as a jagged chunk of debris instead of a plain box -- purely cosmetic, never
    // affects the obstacle's actual collider/hit-box, and reuses the obstacle's own existing
    // material so a chip automatically matches whatever color that obstacle already renders in.
    // Guarded by facetsBuilt (a per-instance field, not reset on disable) so an obstacle builds its
    // chips only once even if it is disabled and re-enabled.
    private void BuildFacetsIfNeeded()
    {
        if (facetsBuilt || !visual)
        {
            return;
        }

        facetsBuilt = true;

        var visualRenderer = visual.GetComponent<Renderer>();
        if (!visualRenderer)
        {
            return;
        }

        Material sharedMat = visualRenderer.sharedMaterial;

        // Deterministic per-instance jitter (GetInstanceID, not gameplay RNG) so every obstacle
        // looks slightly different without ever touching fairness-relevant state.
        var rng = new System.Random(GetInstanceID());
        const int chipCount = 3;
        for (int i = 0; i < chipCount; i++)
        {
            float rx = (float)(rng.NextDouble() * 2.0 - 1.0);
            float ry = (float)(rng.NextDouble() * 2.0 - 1.0);
            float rz = (float)(rng.NextDouble() * 2.0 - 1.0);
            float chipScale = 0.22f + (float)rng.NextDouble() * 0.16f;

            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.name = $"Facet_{i}";
            chip.transform.SetParent(visual, false);
            chip.transform.localPosition = new Vector3(rx, ry, rz) * 0.4f;
            chip.transform.localScale = Vector3.one * chipScale;
            chip.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f);

            var chipCollider = chip.GetComponent<Collider>();
            if (chipCollider)
            {
                Destroy(chipCollider);
            }

            var chipRenderer = chip.GetComponent<Renderer>();
            if (chipRenderer && sharedMat)
            {
                chipRenderer.sharedMaterial = sharedMat;
            }
        }
    }

    private void Update()
    {
        if (visual && Mathf.Abs(visualSpinDegreesPerSecond) > 0.01f)
        {
            visual.Rotate(Vector3.up, visualSpinDegreesPerSecond * Time.deltaTime, Space.Self);
        }

        UpdateApproachVisual();
    }

    private void UpdateApproachVisual()
    {
        if (!visual)
        {
            return;
        }

        float range = Mathf.Max(0.1f, spawnHeight - dangerHeight);
        float approach = Mathf.Clamp01((spawnHeight - transform.position.y) / range);
        float scale = Mathf.Lerp(farVisualScale, nearVisualScale, approach);

        if (transform.position.y <= dangerHeight)
        {
            scale *= 1f + Mathf.Sin(Time.time * 11f) * 0.035f;
        }

        visual.localScale = baseVisualScale * scale;
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void Register()
    {
        if (registered || owner == null || LaneIndex < 0)
        {
            return;
        }

        owner.RegisterLaneMarker(this);
        registered = true;
    }

    private void OnDestroy()
    {
        Unregister();
        owner = null;
        initialized = false;
    }

    private void Unregister()
    {
        if (!registered || owner == null)
        {
            return;
        }

        owner.UnregisterLaneMarker(this);
        registered = false;
    }
}
