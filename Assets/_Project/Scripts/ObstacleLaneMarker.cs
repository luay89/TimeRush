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
    private Transform impactMarker;
    private Material impactMaterial;

    // Shared by every obstacle: the ring mesh/texture are built once per process.
    private static Mesh impactMesh;
    private static Texture2D impactTexture;
    private static PlayerController cachedPlayer;

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
        // Kept close to the 1x1x1 hit box (wingspan ~1.1 at its largest) so neighbouring lanes
        // never visually merge into one pile of ships.
        visual.localScale = Vector3.one * 0.8f;
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
        UpdateImpactMarker();
    }

    // The chase camera looks down at the falling warships, so on screen a ship one depth step in
    // front of or behind the player can sit right on top of the player's ship and still miss --
    // it read as enemies flying through the player with no effect. This ring is drawn on the
    // player's own flight plane directly under the warship: it marks exactly where the ship will
    // cross that plane, so "the ring is under me" means a hit and anything else is a clean miss.
    // Purely visual; no collider, never read by gameplay or fairness.
    private void UpdateImpactMarker()
    {
        if (!cachedPlayer)
        {
            cachedPlayer = FindObjectOfType<PlayerController>();
        }

        if (!cachedPlayer)
        {
            if (impactMarker)
            {
                impactMarker.gameObject.SetActive(false);
            }

            return;
        }

        if (!impactMarker)
        {
            BuildImpactMarker();
        }

        Vector3 playerPosition = cachedPlayer.transform.position;
        Vector3 position = transform.position;
        float above = position.y - playerPosition.y;
        bool visible = above > -0.6f;
        if (impactMarker.gameObject.activeSelf != visible)
        {
            impactMarker.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        impactMarker.position = new Vector3(position.x, playerPosition.y - 0.35f, position.z);
        impactMarker.rotation = Quaternion.Euler(90f, 0f, 0f);

        float range = Mathf.Max(0.1f, spawnHeight - playerPosition.y);
        float closeness = Mathf.Clamp01(1f - above / range);
        bool onPlayer = Mathf.Abs(position.x - playerPosition.x) < 1f && Mathf.Abs(position.z - playerPosition.z) < 0.9f;
        float pulse = onPlayer ? 0.75f + Mathf.Sin(Time.time * 18f) * 0.25f : 1f;
        float alpha = Mathf.Lerp(0.18f, 0.95f, closeness * closeness) * pulse;
        impactMaterial.color = onPlayer ? new Color(1f, 0.15f, 0.1f, alpha) : new Color(1f, 0.55f, 0.15f, alpha * 0.8f);
        impactMarker.localScale = Vector3.one * Mathf.Lerp(1.8f, 1.15f, closeness);
    }

    private void BuildImpactMarker()
    {
        // Not parented: the obstacle root has a non-uniform scale that would skew the ring.
        // OnDisable hides it and OnDestroy removes it together with the obstacle.
        var go = new GameObject("ImpactMarker");
        go.AddComponent<MeshFilter>().sharedMesh = ResolveImpactMesh();
        var renderer = go.AddComponent<MeshRenderer>();
        impactMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = ResolveImpactTexture() };
        renderer.sharedMaterial = impactMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        impactMarker = go.transform;
    }

    private static Mesh ResolveImpactMesh()
    {
        if (impactMesh)
        {
            return impactMesh;
        }

        // Unit quad in the XY plane (rotated flat at runtime), built by hand so no collider exists.
        impactMesh = new Mesh { name = "ImpactMarkerQuad" };
        impactMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        impactMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        // Both windings so the ring is visible whichever way the quad ends up facing.
        impactMesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
        impactMesh.RecalculateBounds();
        return impactMesh;
    }

    private static Texture2D ResolveImpactTexture()
    {
        if (impactTexture)
        {
            return impactTexture;
        }

        const int size = 64;
        impactTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ImpactMarkerRing" };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // Bright outer ring, faint filled disc, small centre dot.
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.1f);
                float fill = r < 0.82f ? 0.18f : 0f;
                float dot = Mathf.Clamp01(1f - r / 0.12f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(ring, Mathf.Max(fill, dot))));
            }
        }

        impactTexture.SetPixels(pixels);
        impactTexture.Apply(false, true);
        return impactTexture;
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
        if (impactMarker)
        {
            impactMarker.gameObject.SetActive(false);
        }

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
        if (impactMarker)
        {
            Destroy(impactMarker.gameObject);
        }

        if (impactMaterial)
        {
            Destroy(impactMaterial);
        }

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
