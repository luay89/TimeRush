using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Purely cosmetic space-battle backdrop behind the flight track: a huge armored battle station,
/// squadrons of fighters dogfighting across the sky, green/red laser bolts and distant explosions.
/// Everything lives far outside the track volume, has no colliders, and never reads or writes
/// gameplay state (score, obstacles, fairness, difficulty). Built once per Game scene load by
/// RushTrackEnvironment; all runtime materials/textures/meshes are freed in OnDestroy.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpaceBattleBackdrop : MonoBehaviour
{
    private const string PropShaderPath = "Shaders/TimeRushSpaceProp";
    private const string GlowShaderPath = "Shaders/TimeRushAdditiveGlow";

    // Camera anchor the backdrop is laid out around (Game.unity's Main Camera start pose). The
    // camera only moves a few units during play, so the far backdrop barely parallaxes.
    private static readonly Vector3 ViewOrigin = new Vector3(0f, 8.5f, -18f);
    private const float ViewPitchDegrees = 25f;

    [Header("Battle Station")]
    [SerializeField] private float stationDistance = 470f;
    [SerializeField] private float stationRadius = 135f;
    [SerializeField] private float stationAzimuth = -27f;
    [SerializeField] private float stationElevation = 14f;

    [Header("Dogfight")]
    [SerializeField, Range(1, 10)] private int dogfightPairs = 6;
    [SerializeField, Range(0, 8)] private int loneFighters = 4;
    [SerializeField, Range(8, 64)] private int boltPoolSize = 40;
    [SerializeField, Range(0f, 12f)] private float strayBoltsPerSecond = 6f;
    [SerializeField, Range(0f, 1f)] private float explosionChancePerPass = 0.45f;

    private const float FighterScale = 1.7f;
    private const float MinCrossingDistance = 72f;

    private static readonly Color AllyHull = new Color(0.78f, 0.8f, 0.84f);
    private static readonly Color EnemyHull = new Color(0.36f, 0.38f, 0.43f);
    private static readonly Color AllyEngine = new Color(4.2f, 0.9f, 2.1f);
    private static readonly Color EnemyEngine = new Color(0.6f, 1.6f, 4f);
    private static readonly Color GreenBolt = new Color(0.55f, 5.5f, 0.8f);
    private static readonly Color RedBolt = new Color(5.5f, 0.7f, 0.8f);

    private readonly List<Object> ownedAssets = new List<Object>(24);
    private readonly List<Fighter> fighters = new List<Fighter>(16);
    private readonly List<DogfightPath> paths = new List<DogfightPath>(12);
    private Bolt[] bolts = System.Array.Empty<Bolt>();
    private int nextBolt;

    private Transform root;
    private Transform station;
    private Material stationMaterial;
    private Material cruiserMaterial;
    private Material allyHullMaterial;
    private Material enemyHullMaterial;
    private Material allyEngineMaterial;
    private Material enemyEngineMaterial;
    private Material greenBoltMaterial;
    private Material redBoltMaterial;
    private ParticleSystem explosionSystem;
    private ParticleSystem flashSystem;
    private float strayBoltTimer;
    private float stationSparkTimer;
    private Transform cruiser;
    private List<MergedPart> starfighterTemplate;
    private List<MergedPart> interceptorTemplate;
    private float cruiserFireTimer;
    private Vector3 sunDirection = new Vector3(-0.62f, 0.5f, -0.35f).normalized;

    private sealed class Fighter
    {
        public Transform Transform;
        public bool Enemy;
        public float FireTimer;
    }

    private struct Bolt
    {
        public LineRenderer Line;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Length;
        public float Life;
    }

    // One pass of a fighter (or an enemy chasing an ally) across the view, from Start to End.
    private sealed class DogfightPath
    {
        public Fighter Leader;
        public Fighter Chaser;
        public Vector3 Start;
        public Vector3 End;
        public Vector3 WobbleAxis;
        public float Duration;
        public float Elapsed;
        public float Wobble;
        public float ChaseLag;
        public float ExplodeAt;
        public bool Exploded;
    }

    private void Awake()
    {
        Shader propShader = Resources.Load<Shader>(PropShaderPath);
        Shader glowShader = Resources.Load<Shader>(GlowShaderPath);
        if (!propShader || !glowShader)
        {
            // Without the backdrop shaders the scene still plays; it just keeps the plain sky.
            enabled = false;
            return;
        }

        root = new GameObject("SpaceBattleBackdrop").transform;
        root.SetParent(transform, false);

        Texture2D glowTexture = Own(CreateGlowTexture());
        Texture2D boltTexture = Own(CreateBoltTexture());

        stationMaterial = CreatePropMaterial(propShader, new Color(0.46f, 0.48f, 0.52f), Own(CreateStationTexture()), new Color(1.5f, 1.15f, 0.75f));
        stationMaterial.SetColor("_RimColor", new Color(0.18f, 0.24f, 0.36f));
        // A touch more ambient so the night side still reads as a solid disc against space.
        stationMaterial.SetColor("_Ambient", new Color(0.075f, 0.085f, 0.11f));
        cruiserMaterial = CreatePropMaterial(propShader, new Color(0.5f, 0.52f, 0.56f), Own(CreateHullStripTexture()), new Color(0.8f, 1.2f, 1.8f));
        allyHullMaterial = CreatePropMaterial(propShader, AllyHull, null, Color.black);
        enemyHullMaterial = CreatePropMaterial(propShader, EnemyHull, null, Color.black);
        allyEngineMaterial = CreateGlowMaterial(glowShader, AllyEngine, glowTexture);
        enemyEngineMaterial = CreateGlowMaterial(glowShader, EnemyEngine, glowTexture);
        greenBoltMaterial = CreateGlowMaterial(glowShader, GreenBolt, boltTexture);
        redBoltMaterial = CreateGlowMaterial(glowShader, RedBolt, boltTexture);

        BuildStation();
        BuildExplosionSystems(glowShader, glowTexture);
        BuildBoltPool();
        BuildFighters();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < ownedAssets.Count; i++)
        {
            if (ownedAssets[i])
            {
                Destroy(ownedAssets[i]);
            }
        }

        ownedAssets.Clear();
    }

    private void Update()
    {
        if (!root)
        {
            return;
        }

        // Backdrop keeps animating in menus/pause-free states too; it is scenery, not gameplay.
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        if (station)
        {
            station.Rotate(Vector3.up, 0.6f * dt, Space.Self);
        }

        for (int i = 0; i < paths.Count; i++)
        {
            AdvancePath(paths[i], dt);
        }

        AdvanceBolts(dt);

        strayBoltTimer -= dt * strayBoltsPerSecond;
        while (strayBoltTimer <= 0f)
        {
            strayBoltTimer += Random.Range(0.4f, 1.6f);
            FireStrayBolt();
        }

        cruiserFireTimer -= dt;
        if (cruiserFireTimer <= 0f)
        {
            cruiserFireTimer = Random.Range(0.35f, 1.1f);
            CruiserVolley();
        }

        stationSparkTimer -= dt;
        if (stationSparkTimer <= 0f)
        {
            stationSparkTimer = Random.Range(0.8f, 2.2f);
            SparkOnStation();
        }
    }

    // ---------------------------------------------------------------- layout helpers

    // World point at the given angles (degrees, relative to the camera's straight-ahead horizontal
    // view) and distance from the camera anchor. Elevation is measured from the camera's pitched
    // view axis, so 0 is the middle of the screen.
    private static Vector3 ViewPoint(float azimuth, float elevation, float distance)
    {
        Quaternion look = Quaternion.Euler(ViewPitchDegrees - elevation, azimuth, 0f);
        return ViewOrigin + look * Vector3.forward * distance;
    }

    // ---------------------------------------------------------------- battle station

    private void BuildStation()
    {
        station = new GameObject("BattleStation").transform;
        station.SetParent(root, false);
        station.position = ViewPoint(stationAzimuth, stationElevation, stationDistance);
        station.rotation = Quaternion.Euler(12f, 35f, -8f);

        // Unity's built-in sphere is too coarse at this screen size (its silhouette visibly
        // facets), so the hull gets its own denser UV sphere.
        var hull = new GameObject("Hull");
        hull.transform.SetParent(station, false);
        hull.transform.localScale = Vector3.one * stationRadius;
        hull.AddComponent<MeshFilter>().sharedMesh = Own(CreateUvSphereMesh(72, 40));
        hull.AddComponent<MeshRenderer>().sharedMaterial = stationMaterial;
        DisableShadows(hull);

        BuildCruiser();
    }

    // A long capital cruiser holding position on the right flank, trading fire with the station
    // side of the battle. Fills the otherwise empty half of the sky with a second big silhouette.
    private void BuildCruiser()
    {
        cruiser = new GameObject("CapitalCruiser").transform;
        cruiser.SetParent(root, false);
        cruiser.position = ViewPoint(33f, 12f, 420f);
        // Nose angled away and to the left, so the hull reads as a long wedge in 3/4 view.
        // Deck tilted toward the camera so its broad top and superstructure read, not a thin edge.
        Vector3 heading = ViewPoint(0f, 16f, 620f) - cruiser.position;
        Vector3 toCamera = (ViewOrigin - cruiser.position).normalized;
        cruiser.rotation = Quaternion.LookRotation(heading, Vector3.Lerp(Vector3.up, toCamera, 0.65f));

        const float length = 150f;
        AddPart(PrimitiveType.Cube, "Keel", cruiser, cruiserMaterial, Vector3.zero, new Vector3(46f, 8f, length), Quaternion.identity);
        AddPart(PrimitiveType.Cube, "Prow", cruiser, cruiserMaterial, new Vector3(0f, -1f, length * 0.58f), new Vector3(24f, 5f, length * 0.25f), Quaternion.identity);
        AddPart(PrimitiveType.Cube, "UpperDeck", cruiser, cruiserMaterial, new Vector3(0f, 6.5f, -length * 0.12f), new Vector3(30f, 5f, length * 0.6f), Quaternion.identity);
        AddPart(PrimitiveType.Cube, "Bridge", cruiser, cruiserMaterial, new Vector3(0f, 13f, -length * 0.3f), new Vector3(9f, 8f, 12f), Quaternion.identity);
        AddPart(PrimitiveType.Cube, "BridgeWing", cruiser, cruiserMaterial, new Vector3(0f, 17.5f, -length * 0.3f), new Vector3(22f, 2.2f, 5f), Quaternion.identity);
        for (int s = -1; s <= 1; s += 2)
        {
            AddPart(PrimitiveType.Cube, "SidePod", cruiser, cruiserMaterial, new Vector3(s * 26f, -1f, -length * 0.2f), new Vector3(10f, 7f, length * 0.45f), Quaternion.identity);
            AddGlow(cruiser, enemyEngineMaterial, new Vector3(s * 26f, -1f, -length * 0.43f), 12f);
        }

        AddGlow(cruiser, enemyEngineMaterial, new Vector3(0f, 0f, -length * 0.51f), 16f);
        MergeChildrenByMaterial(cruiser);
        cruiserFireTimer = 1f;
    }

    private void CruiserVolley()
    {
        if (!cruiser)
        {
            return;
        }

        // Broadside toward the station side of the sky.
        Vector3 origin = cruiser.position + cruiser.forward * Random.Range(-70f, 70f) + cruiser.up * Random.Range(0f, 10f);
        Vector3 target = ViewPoint(Random.Range(-45f, 5f), Random.Range(-25f, 20f), Random.Range(120f, 320f));
        FireBolt(origin, (target - origin).normalized, GreenBolt, 170f, 26f);
    }

    // Long thin hull texture: stripes of plating plus rows of lit portholes in alpha.
    private static Texture2D CreateHullStripTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            name = "CruiserHull"
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                uint h = Hash((uint)(x / 8), (uint)(y / 4));
                byte v = (byte)(150 + h % 70 - (x % 8 == 0 || y % 4 == 0 ? 50 : 0));
                byte emission = (byte)(y % 16 == 8 && x % 4 == 1 && h % 3 != 0 ? 255 : 0);
                pixels[y * size + x] = new Color32(v, v, v, emission);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, false);
        return texture;
    }

    // Equirectangular hull texture for Unity's sphere UVs: irregular armor plates, a deep
    // equatorial trench, and a sprinkling of window lights in the alpha (emission) channel.
    private static Texture2D CreateStationTexture()
    {
        const int width = 1024;
        const int height = 512;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 2,
            name = "BattleStationHull"
        };

        var random = new System.Random(4127);
        var pixels = new Color32[width * height];
        int trenchY = height / 2 + 30;

        for (int y = 0; y < height; y++)
        {
            // Plates get narrower toward the poles in texture space to keep them square on the sphere.
            const int plateH = 5;
            int row = y / plateH;
            // Wide latitude "decks" every ~40 rows give large-scale structure on top of the plates.
            float deck = (y / 40) % 2 == 0 ? 1f : 0.92f;
            for (int x = 0; x < width; x++)
            {
                int plateW = 7 + (int)(Hash((uint)row, 91u) % 9u);
                int shifted = x + (int)(Hash((uint)row, 7u) % 16u);
                int col = shifted / plateW;
                uint h = Hash((uint)col, (uint)row);
                float shade = (0.8f + (h % 1000) / 1000f * 0.2f) * deck;
                bool seam = shifted % plateW == 0 || y % plateH == 0;
                if (seam)
                {
                    shade *= 0.78f;
                }

                // Clusters of darker superstructure blocks spanning several plates.
                if (Hash((uint)(shifted / 40), (uint)(y / 18)) % 7u == 0u)
                {
                    shade *= 0.84f;
                }

                int trenchDistance = Mathf.Abs(y - trenchY);
                if (trenchDistance < 3)
                {
                    shade = 0.22f;
                }
                else if (trenchDistance < 5)
                {
                    shade *= 0.6f;
                }

                byte emission = 0;
                double lightRoll = random.NextDouble();
                if (trenchDistance < 3 && lightRoll < 0.22)
                {
                    emission = 255;
                }
                else if (!seam && lightRoll < 0.0035)
                {
                    emission = (byte)(140 + random.Next(115));
                }

                byte v = (byte)(Mathf.Clamp01(shade) * 255f);
                pixels[y * width + x] = new Color32(v, v, (byte)Mathf.Min(255, v + 6), emission);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, false);
        return texture;
    }

    // Unit-radius UV sphere; u wraps around longitude, v runs pole to pole (same layout as the
    // built-in sphere, so the equirectangular hull texture maps the same way).
    private static Mesh CreateUvSphereMesh(int longitudeSegments, int latitudeSegments)
    {
        int columns = longitudeSegments + 1;
        var vertices = new Vector3[columns * (latitudeSegments + 1)];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[longitudeSegments * latitudeSegments * 6];

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float v = lat / (float)latitudeSegments;
            float theta = v * Mathf.PI;
            float y = -Mathf.Cos(theta);
            float ring = Mathf.Sin(theta);
            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float u = lon / (float)longitudeSegments;
                float phi = u * Mathf.PI * 2f;
                int index = lat * columns + lon;
                vertices[index] = new Vector3(Mathf.Cos(phi) * ring, y, Mathf.Sin(phi) * ring);
                uvs[index] = new Vector2(1f - u, v);
            }
        }

        int t = 0;
        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int a = lat * columns + lon;
                int b = a + columns;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
        }

        var mesh = new Mesh { name = "BattleStationHull" };
        mesh.vertices = vertices;
        mesh.normals = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void SparkOnStation()
    {
        if (!station || !explosionSystem)
        {
            return;
        }

        // A tiny flash somewhere on the station's visible face -- turret fire / hits in the battle.
        Vector3 toCamera = (ViewOrigin - station.position).normalized;
        Vector3 offset = Random.insideUnitSphere;
        offset = (offset + toCamera * 1.3f).normalized * stationRadius * 1.01f;
        EmitExplosion(station.position + offset, 2.2f, 5, false);
    }

    // ---------------------------------------------------------------- fighters

    private void BuildFighters()
    {
        for (int i = 0; i < dogfightPairs; i++)
        {
            Fighter ally = CreateFighter(false, i);
            Fighter enemy = CreateFighter(true, i);
            var path = new DogfightPath { Leader = ally, Chaser = enemy };
            ResetPath(path, true);
            paths.Add(path);
        }

        for (int i = 0; i < loneFighters; i++)
        {
            bool enemy = i % 2 == 0;
            Fighter fighter = CreateFighter(enemy, dogfightPairs + i);
            var path = new DogfightPath { Leader = fighter };
            ResetPath(path, true);
            paths.Add(path);
        }
    }

    private Fighter CreateFighter(bool enemy, int index)
    {
        var body = new GameObject((enemy ? "Interceptor_" : "Starfighter_") + index).transform;
        body.SetParent(root, false);
        body.localScale = Vector3.one * FighterScale;

        List<MergedPart> template = enemy ? interceptorTemplate : starfighterTemplate;
        if (template == null)
        {
            // First fighter of this type: build it from primitives once, then merge the parts into
            // one mesh per material. Every later fighter reuses those meshes, so each ship costs
            // two or three draw calls instead of a dozen.
            if (enemy)
            {
                BuildInterceptor(body);
            }
            else
            {
                BuildStarfighter(body);
            }

            template = MergeChildrenByMaterial(body);
            if (enemy)
            {
                interceptorTemplate = template;
            }
            else
            {
                starfighterTemplate = template;
            }
        }
        else
        {
            InstantiateMerged(body, template);
        }

        var fighter = new Fighter { Transform = body, Enemy = enemy, FireTimer = Random.Range(0.2f, 1.2f) };
        fighters.Add(fighter);
        return fighter;
    }

    // Allied strike fighter: long nose, swept twin wings with wingtip engines glowing pink.
    private void BuildStarfighter(Transform body)
    {
        AddPart(PrimitiveType.Cube, "Fuselage", body, allyHullMaterial, new Vector3(0f, 0f, 0f), new Vector3(0.55f, 0.42f, 3.4f), Quaternion.identity);
        AddPart(PrimitiveType.Cube, "Nose", body, allyHullMaterial, new Vector3(0f, -0.04f, 2.1f), new Vector3(0.36f, 0.3f, 1.2f), Quaternion.identity);
        AddPart(PrimitiveType.Cube, "Canopy", body, enemyHullMaterial, new Vector3(0f, 0.25f, 0.6f), new Vector3(0.34f, 0.2f, 0.8f), Quaternion.identity);
        for (int s = -1; s <= 1; s += 2)
        {
            AddPart(PrimitiveType.Cube, "WingUpper", body, allyHullMaterial, new Vector3(s * 1.25f, 0.28f, -0.7f), new Vector3(2.3f, 0.07f, 1.1f), Quaternion.Euler(0f, s * -12f, s * 14f));
            AddPart(PrimitiveType.Cube, "WingLower", body, allyHullMaterial, new Vector3(s * 1.25f, -0.28f, -0.7f), new Vector3(2.3f, 0.07f, 1.1f), Quaternion.Euler(0f, s * -12f, s * -14f));
            AddPart(PrimitiveType.Cube, "Engine", body, allyHullMaterial, new Vector3(s * 0.5f, 0.22f, -1.3f), new Vector3(0.32f, 0.32f, 1.3f), Quaternion.identity);
            AddPart(PrimitiveType.Cube, "Engine", body, allyHullMaterial, new Vector3(s * 0.5f, -0.22f, -1.3f), new Vector3(0.32f, 0.32f, 1.3f), Quaternion.identity);
            AddGlow(body, allyEngineMaterial, new Vector3(s * 0.5f, 0.22f, -2.05f), 0.75f);
            AddGlow(body, allyEngineMaterial, new Vector3(s * 0.5f, -0.22f, -2.05f), 0.75f);
            AddPart(PrimitiveType.Cube, "Cannon", body, enemyHullMaterial, new Vector3(s * 2.35f, s * 0f, 0.2f), new Vector3(0.08f, 0.08f, 2.2f), Quaternion.identity);
        }
    }

    // Enemy interceptor: compact pod with two large angular blade wings and a blue-white drive.
    private void BuildInterceptor(Transform body)
    {
        AddPart(PrimitiveType.Sphere, "Pod", body, enemyHullMaterial, Vector3.zero, new Vector3(0.95f, 0.95f, 1.1f), Quaternion.identity);
        for (int s = -1; s <= 1; s += 2)
        {
            AddPart(PrimitiveType.Cube, "Strut", body, enemyHullMaterial, new Vector3(s * 0.75f, 0f, 0f), new Vector3(0.9f, 0.16f, 0.2f), Quaternion.identity);
            AddPart(PrimitiveType.Cube, "Blade", body, enemyHullMaterial, new Vector3(s * 1.25f, 0f, -0.15f), new Vector3(0.08f, 2.1f, 1.7f), Quaternion.Euler(0f, 0f, s * 12f));
            AddPart(PrimitiveType.Cube, "BladeEdge", body, allyHullMaterial, new Vector3(s * 1.3f, 0f, 0.72f), new Vector3(0.1f, 2f, 0.1f), Quaternion.Euler(0f, 0f, s * 12f));
        }

        AddGlow(body, enemyEngineMaterial, new Vector3(0f, 0f, -0.62f), 0.8f);
    }

    private readonly struct MergedPart
    {
        public readonly Mesh Mesh;
        public readonly Material Material;

        public MergedPart(Mesh mesh, Material material)
        {
            Mesh = mesh;
            Material = material;
        }
    }

    // Collapses every primitive part under body into one child renderer per material (in body's
    // local space) and destroys the individual parts.
    private List<MergedPart> MergeChildrenByMaterial(Transform body)
    {
        var filters = body.GetComponentsInChildren<MeshFilter>();
        var materials = new List<Material>(4);
        for (int i = 0; i < filters.Length; i++)
        {
            Material material = filters[i].GetComponent<Renderer>().sharedMaterial;
            if (!materials.Contains(material))
            {
                materials.Add(material);
            }
        }

        Matrix4x4 toBody = body.worldToLocalMatrix;
        var merged = new List<MergedPart>(materials.Count);
        for (int m = 0; m < materials.Count; m++)
        {
            var combine = new List<CombineInstance>(filters.Length);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].GetComponent<Renderer>().sharedMaterial == materials[m])
                {
                    combine.Add(new CombineInstance { mesh = filters[i].sharedMesh, transform = toBody * filters[i].transform.localToWorldMatrix });
                }
            }

            var mesh = Own(new Mesh { name = body.name + "_" + m });
            mesh.CombineMeshes(combine.ToArray(), true, true);
            merged.Add(new MergedPart(mesh, materials[m]));
        }

        for (int i = 0; i < filters.Length; i++)
        {
            Destroy(filters[i].gameObject);
        }

        InstantiateMerged(body, merged);
        return merged;
    }

    private static void InstantiateMerged(Transform body, List<MergedPart> parts)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            var go = new GameObject("Merged_" + i);
            go.transform.SetParent(body, false);
            go.AddComponent<MeshFilter>().sharedMesh = parts[i].Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = parts[i].Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void AddPart(PrimitiveType type, string name, Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        GameObject part = CreatePrimitive(type, name, parent, material);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
    }

    private void AddGlow(Transform parent, Material material, Vector3 position, float size)
    {
        GameObject glow = CreatePrimitive(PrimitiveType.Quad, "EngineGlow", parent, material);
        glow.transform.localPosition = position;
        glow.transform.localScale = Vector3.one * size;
        // Quads face along -Z by default; the engines point backwards, toward the chase camera
        // for fighters flying away, and a billboard-free quad is fine at these tiny sizes.
        glow.transform.localRotation = Quaternion.identity;
        var second = CreatePrimitive(PrimitiveType.Quad, "EngineGlowSide", parent, material);
        second.transform.localPosition = position;
        second.transform.localScale = Vector3.one * size;
        second.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
    }

    private void ResetPath(DogfightPath path, bool initial)
    {
        bool flyby = Random.value < 0.3f;
        if (flyby)
        {
            // Screams in from deep space and passes over/next to the camera.
            float az = Random.Range(-18f, 18f);
            path.Start = ViewPoint(az, Random.Range(-8f, 10f), Random.Range(260f, 340f));
            path.End = ViewPoint((az < 0f ? -1f : 1f) * Random.Range(34f, 46f), Random.Range(14f, 30f), Random.Range(34f, 48f));
            path.Duration = Random.Range(4.5f, 6.5f);
        }
        else
        {
            // Crosses the view from one side to the other, at a random depth.
            float side = Random.value < 0.5f ? -1f : 1f;
            // Never closer than the far end of the track, so nothing crosses in front of the lanes.
            float distance = Random.Range(MinCrossingDistance, 170f);
            path.Start = ViewPoint(side * Random.Range(50f, 62f), Random.Range(-30f, 22f), distance * Random.Range(1f, 1.2f));
            path.End = ViewPoint(-side * Random.Range(50f, 62f), Random.Range(-30f, 22f), distance * Random.Range(1f, 1.2f));
            path.Duration = Random.Range(4.5f, 8f) * Mathf.Lerp(0.8f, 1.3f, distance / 170f);
        }

        path.WobbleAxis = Random.onUnitSphere;
        path.Wobble = Random.Range(2f, 7f);
        path.ChaseLag = Random.Range(0.045f, 0.09f);
        path.Elapsed = initial ? Random.Range(0f, path.Duration * 0.8f) : 0f;
        path.Exploded = false;
        path.ExplodeAt = path.Chaser != null && Random.value < explosionChancePerPass ? Random.Range(0.45f, 0.8f) : 2f;

        path.Leader.Transform.gameObject.SetActive(true);
        if (path.Chaser != null)
        {
            path.Chaser.Transform.gameObject.SetActive(true);
        }
    }

    private void AdvancePath(DogfightPath path, float dt)
    {
        path.Elapsed += dt;
        float t = path.Elapsed / path.Duration;
        if (t >= 1f + (path.Chaser != null ? path.ChaseLag : 0f))
        {
            ResetPath(path, false);
            return;
        }

        PlaceOnPath(path.Leader, path, t, dt);
        if (path.Chaser != null)
        {
            PlaceOnPath(path.Chaser, path, t - path.ChaseLag, dt);

            if (!path.Exploded && t >= path.ExplodeAt && path.Chaser.Transform.gameObject.activeSelf)
            {
                path.Exploded = true;
                // Either the pursuer gets shot down, or it finally lands the kill on the leader.
                Fighter victim = Random.value < 0.6f ? path.Chaser : path.Leader;
                EmitExplosion(victim.Transform.position, 1f, 26, true);
                victim.Transform.gameObject.SetActive(false);
            }
        }
    }

    private void PlaceOnPath(Fighter fighter, DogfightPath path, float t, float dt)
    {
        if (!fighter.Transform.gameObject.activeSelf)
        {
            return;
        }

        float clamped = Mathf.Clamp01(t);
        Vector3 position = PathPoint(path, clamped);
        Vector3 ahead = PathPoint(path, Mathf.Clamp01(clamped + 0.01f));
        Vector3 forward = ahead - position;
        if (forward.sqrMagnitude < 1e-6f)
        {
            forward = path.End - path.Start;
        }

        float bank = Mathf.Sin(clamped * 9f + path.Wobble) * 35f;
        fighter.Transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized) * Quaternion.Euler(0f, 0f, bank));

        fighter.FireTimer -= dt;
        if (fighter.FireTimer <= 0f && t > 0.05f && t < 0.95f)
        {
            fighter.FireTimer = Random.Range(0.25f, 0.9f);
            Vector3 muzzle = position + fighter.Transform.forward * 2.2f * FighterScale;
            Vector3 aim = fighter.Transform.forward + Random.insideUnitSphere * 0.04f;
            FireBolt(muzzle, aim.normalized, fighter.Enemy ? GreenBolt : RedBolt, Random.Range(90f, 130f), 9f);
        }
    }

    private static Vector3 PathPoint(DogfightPath path, float t)
    {
        Vector3 straight = Vector3.Lerp(path.Start, path.End, t);
        float distance = Vector3.Distance(path.Start, path.End);
        Vector3 side = Vector3.Cross(path.End - path.Start, path.WobbleAxis).normalized;
        return straight + side * Mathf.Sin(t * Mathf.PI * 2.2f + path.Wobble) * distance * 0.06f;
    }

    // ---------------------------------------------------------------- bolts

    private void BuildBoltPool()
    {
        bolts = new Bolt[boltPoolSize];
        var boltRoot = new GameObject("LaserBolts").transform;
        boltRoot.SetParent(root, false);

        for (int i = 0; i < bolts.Length; i++)
        {
            var go = new GameObject("Bolt_" + i);
            go.transform.SetParent(boltRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = greenBoltMaterial;
            line.enabled = false;
            bolts[i] = new Bolt { Line = line };
        }
    }

    private void FireStrayBolt()
    {
        // Bolts from off-screen gunners slicing across the view, mostly green like the reference.
        float side = Random.value < 0.5f ? -1f : 1f;
        float distance = Random.Range(MinCrossingDistance, 220f);
        Vector3 start = ViewPoint(side * Random.Range(40f, 55f), Random.Range(-32f, 26f), distance);
        Vector3 end = ViewPoint(-side * Random.Range(10f, 55f), Random.Range(-32f, 26f), distance * Random.Range(1f, 1.3f));
        Color color = Random.value < 0.72f ? GreenBolt : RedBolt;
        FireBolt(start, (end - start).normalized, color, Random.Range(110f, 170f), Random.Range(6f, 10f) * distance / 120f);
    }

    private void FireBolt(Vector3 origin, Vector3 direction, Color color, float speed, float length)
    {
        if (bolts.Length == 0)
        {
            return;
        }

        Bolt bolt = bolts[nextBolt];
        nextBolt = (nextBolt + 1) % bolts.Length;
        bolt.Position = origin;
        bolt.Velocity = direction * speed;
        bolt.Length = Mathf.Max(2f, length);
        bolt.Life = 2.4f;
        float width = Mathf.Clamp(bolt.Length * 0.09f, 0.5f, 2f);
        bolt.Line.startWidth = width;
        bolt.Line.endWidth = width;
        bolt.Line.sharedMaterial = color == GreenBolt ? greenBoltMaterial : redBoltMaterial;
        bolt.Line.enabled = true;
        bolts[(nextBolt + bolts.Length - 1) % bolts.Length] = bolt;
        UpdateBoltLine(ref bolt);
    }

    private void AdvanceBolts(float dt)
    {
        for (int i = 0; i < bolts.Length; i++)
        {
            Bolt bolt = bolts[i];
            if (!bolt.Line.enabled)
            {
                continue;
            }

            bolt.Life -= dt;
            if (bolt.Life <= 0f)
            {
                bolt.Line.enabled = false;
                bolts[i] = bolt;
                continue;
            }

            bolt.Position += bolt.Velocity * dt;
            UpdateBoltLine(ref bolt);
            bolts[i] = bolt;
        }
    }

    private static void UpdateBoltLine(ref Bolt bolt)
    {
        Vector3 tail = bolt.Position - bolt.Velocity.normalized * bolt.Length;
        bolt.Line.SetPosition(0, tail);
        bolt.Line.SetPosition(1, bolt.Position);
    }

    // ---------------------------------------------------------------- explosions

    private void BuildExplosionSystems(Shader glowShader, Texture2D glowTexture)
    {
        explosionSystem = CreateBurstSystem("Explosions", glowShader, glowTexture, new Color(3.2f, 1.5f, 0.45f), 260);
        var main = explosionSystem.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 4.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.4f, 0.12f));

        var colorOverLifetime = explosionSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.35f), new GradientColorKey(new Color(0.35f, 0.08f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = explosionSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));

        flashSystem = CreateBurstSystem("ExplosionFlash", glowShader, glowTexture, new Color(4f, 3.2f, 2.4f), 12);
        var flashMain = flashSystem.main;
        flashMain.startLifetime = 0.22f;
        flashMain.startSpeed = 0f;
        flashMain.startSize = 12f;
        var flashFade = flashSystem.colorOverLifetime;
        flashFade.enabled = true;
        var flashGradient = new Gradient();
        flashGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        flashFade.color = flashGradient;
    }

    private ParticleSystem CreateBurstSystem(string name, Shader glowShader, Texture2D glowTexture, Color tint, int maxParticles)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.gravityModifier = 0f;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = CreateGlowMaterial(glowShader, tint, glowTexture);
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        system.Play();
        return system;
    }

    private void EmitExplosion(Vector3 position, float scale, int count, bool flash)
    {
        if (!explosionSystem)
        {
            return;
        }

        var emit = new ParticleSystem.EmitParams
        {
            position = position,
            applyShapeToPosition = true
        };

        for (int i = 0; i < count; i++)
        {
            emit.velocity = Random.insideUnitSphere * Random.Range(2f, 11f) * Mathf.Sqrt(scale);
            emit.startSize = Random.Range(1.2f, 4.2f) * scale;
            explosionSystem.Emit(emit, 1);
        }

        if (flash)
        {
            var flashParams = new ParticleSystem.EmitParams { position = position, startSize = 12f * scale, velocity = Vector3.zero };
            flashSystem.Emit(flashParams, 1);
        }
    }

    // ---------------------------------------------------------------- materials & helpers

    private Material CreatePropMaterial(Shader shader, Color color, Texture2D texture, Color emission)
    {
        var material = Own(new Material(shader));
        material.color = color;
        if (texture)
        {
            material.mainTexture = texture;
        }

        material.SetColor("_EmissionColor", emission);
        material.SetVector("_SunDir", sunDirection);
        return material;
    }

    private Material CreateGlowMaterial(Shader shader, Color color, Texture2D texture)
    {
        var material = Own(new Material(shader));
        material.SetColor("_Color", color);
        material.mainTexture = texture;
        return material;
    }

    private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        var collider = go.GetComponent<Collider>();
        if (collider)
        {
            Destroy(collider);
        }

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private static void DisableShadows(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private T Own<T>(T asset) where T : Object
    {
        ownedAssets.Add(asset);
        return asset;
    }

    private static Texture2D CreateGlowTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "BackdropGlow"
        };

        var pixels = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float core = Mathf.Clamp01(1f - d);
                float a = core * core * core + Mathf.Clamp01(1f - d * 2.2f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    // U runs along the bolt (bright head, fading tail), V across it (hot core, soft edge).
    private static Texture2D CreateBoltTexture()
    {
        const int width = 64;
        const int height = 16;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "LaserBolt"
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float across = 1f - Mathf.Abs((y + 0.5f) / height * 2f - 1f);
            float acrossA = across * across * 0.7f + (across > 0.72f ? 0.6f : 0f);
            for (int x = 0; x < width; x++)
            {
                float along = (x + 0.5f) / width;
                float alongA = Mathf.Clamp01(along * 1.3f) * Mathf.Clamp01((1f - along) * 12f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(acrossA * alongA) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static uint Hash(uint x, uint y)
    {
        uint h = x * 374761393u + y * 668265263u;
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }
}
