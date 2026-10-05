using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleLaneMarker : MonoBehaviour
{
    // Every live, enabled obstacle -- read by PlayerBlaster to pick a target. Cosmetic/combat
    // only; ObstacleSpawner keeps its own per-lane registry for fairness.
    private static readonly List<ObstacleLaneMarker> active = new List<ObstacleLaneMarker>(16);
    public static IReadOnlyList<ObstacleLaneMarker> Active => active;

    [Tooltip("Maximum bank angle (degrees) of the diving warship's slow side-to-side roll.")]
    [SerializeField] private float bankDegrees = 10f;
    [SerializeField, Range(0.5f, 1f)] private float farVisualScale = 0.58f;
    [SerializeField, Range(1f, 1.4f)] private float nearVisualScale = 1.18f;
    [SerializeField] private float dangerHeight = 4.5f;

    public int LaneIndex { get; private set; } = -1;

    private Transform visual;
    private ObstacleSpawner owner;
    private bool registered;
    private bool initialized;
    private bool warshipBuilt;
    private Quaternion baseVisualRotation = Quaternion.identity;
    private float bankPhase;
    private Rigidbody body;
    private float spawnHeight = 13.5f;
    private Vector3 baseVisualScale = Vector3.one;

    // Read from the Rigidbody's physics position when there is one: obstacles use render
    // interpolation for smooth motion, which makes transform.position lag the physics state by up
    // to one fixed step. Fairness must keep reading the real physics position, exactly as before.
    public float CurrentHeight => body ? body.position.y : transform.position.y;
    public float CurrentDepth => body ? body.position.z : transform.position.z;
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

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        active.Add(this);

        if (!visual)
        {
            visual = transform.Find("Visual");
        }

        if (visual)
        {
            BuildWarshipIfNeeded();
            baseVisualScale = visual.localScale;
        }

        if (initialized)
        {
            Register();
        }
    }

    // Swaps the plain cube for a diving enemy warship. The prefab's "Visual" child (cube mesh) is
    // switched off and the warship renderer takes over as the scaled/animated visual; the root
    // BoxCollider/Rigidbody are untouched, so hit box and fairness are exactly as before. Guarded
    // by warshipBuilt (per instance, not reset on disable) so it is built only once.
    private void BuildWarshipIfNeeded()
    {
        if (warshipBuilt || !visual)
        {
            return;
        }

        warshipBuilt = true;

        var cubeRenderer = visual.GetComponent<Renderer>();
        Material hullMaterial = cubeRenderer ? cubeRenderer.sharedMaterial : null;
        visual.gameObject.SetActive(false);

        visual = WarshipObstacleVisual.Create(transform, hullMaterial);
        visual.localScale = Vector3.one * 1.05f;
        baseVisualRotation = visual.localRotation;
        // Instance id, not gameplay RNG: only desynchronizes the cosmetic roll between ships.
        bankPhase = (GetInstanceID() & 0xFF) * 0.0245f;
    }

    private void Update()
    {
        if (visual && warshipBuilt)
        {
            float bank = Mathf.Sin(Time.time * 2.2f + bankPhase) * bankDegrees;
            visual.localRotation = baseVisualRotation * Quaternion.Euler(0f, 0f, bank);
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
        active.Remove(this);
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
