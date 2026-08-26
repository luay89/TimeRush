using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleLaneMarker : MonoBehaviour
{
    [SerializeField] private float visualSpinDegreesPerSecond = 72f;
    [SerializeField, Range(0.5f, 1f)] private float farVisualScale = 0.62f;
    [SerializeField, Range(1f, 1.4f)] private float nearVisualScale = 1.12f;
    [SerializeField] private float dangerHeight = 4.5f;

    public int LaneIndex { get; private set; } = -1;

    private Transform visual;
    private ObstacleSpawner owner;
    private bool registered;
    private bool initialized;
    private float spawnHeight = 13.5f;
    private Vector3 baseVisualScale = Vector3.one;

    public float CurrentHeight => transform.position.y;
    public float CurrentDepth => transform.position.z;

    public void Initialize(ObstacleSpawner spawner, int laneIndex, float initialSpawnHeight)
    {
        owner = spawner;
        LaneIndex = laneIndex;
        spawnHeight = Mathf.Max(dangerHeight + 0.1f, initialSpawnHeight);
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
        }

        if (initialized)
        {
            Register();
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
