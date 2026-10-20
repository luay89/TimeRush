using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Living 3D universe behind the start screen and the results screen: a nebula sky, a ringed
/// gas giant with a small moon, asteroids streaming past, warp-speed star streaks and the space
/// battle from the game raging in the distance. On the start screen the player's ship (in the
/// selected colour) hovers in the middle of it and blasts off when a run starts; on the results
/// screen the same universe turns to embers and drifting wreckage.
/// Purely cosmetic: never touches gameplay, scoring or fairness. Every runtime asset it creates is
/// freed in OnDestroy.
/// </summary>
[DisallowMultipleComponent]
public sealed class CosmosBackdrop : MonoBehaviour
{
    public enum Mood
    {
        Menu,
        Results
    }

    private const string SkyShaderPath = "Shaders/TimeRushSpaceSky";
    private const string PropShaderPath = "Shaders/TimeRushSpaceProp";
    private const string GlowShaderPath = "Shaders/TimeRushAdditiveGlow";
    private const string HeroShipPath = "Models/HeroShip";

    // Same anchor as the game camera, so the distant battle (laid out around it) frames the same way.
    private static readonly Vector3 ViewOrigin = new Vector3(0f, 8.5f, -18f);
    private const float CameraPitch = 14f;
    private const float FieldOfView = 60f;
    private const int RockCount = 9;

    public static CosmosBackdrop Active { get; private set; }

    private readonly List<Object> ownedAssets = new List<Object>(32);
    private readonly List<Rock> rocks = new List<Rock>(RockCount);

    private Mood mood;
    private Camera viewCamera;
    private Quaternion baseCameraRotation;
    private Transform planet;
    private Transform planetRing;
    private Transform moon;
    private float planetRadius;
    private float layoutAspect = -1f;
    private ParticleSystem streaks;
    private ParticleSystem.MainModule streaksMain;
    private Material glowMaterial;
    private Material rockMaterial;
    private Material emberRockMaterial;

    private Transform shipPivot;
    private Transform shipHolder;
    private float launchTime = -1f;
    private AudioSource audioSource;
    private AudioClip launchClip;

    private sealed class Rock
    {
        public Transform Transform;
        public Vector3 Spin;
        public float Speed;
        public float Lateral;
        public float Vertical;
        public float Depth;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    /// <summary>True for scenes that get this backdrop (it installs itself when they load).</summary>
    public static bool CoversScene(string sceneName)
    {
        return sceneName == SceneNames.MenuHub || sceneName == SceneNames.Results;
    }

    private static void TryInstall(Scene scene)
    {
        Mood? sceneMood = scene.name == SceneNames.MenuHub ? Mood.Menu
            : scene.name == SceneNames.Results ? Mood.Results
            : (Mood?)null;
        if (sceneMood == null || FindObjectOfType<CosmosBackdrop>())
        {
            return;
        }

        var go = new GameObject("CosmosBackdrop");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<CosmosBackdrop>().Build(sceneMood.Value);
    }

    private void Build(Mood sceneMood)
    {
        mood = sceneMood;
        Active = this;

        SetupCamera();
        SetupSkyAndLight();

        Shader glowShader = Resources.Load<Shader>(GlowShaderPath);
        if (glowShader)
        {
            glowMaterial = Own(new Material(glowShader) { mainTexture = SoftParticleMaterial.SharedTexture });
        }

        BuildPlanet();
        BuildRocks();
        BuildStreaks();

        // The game's distant dogfight, battle station and cruiser keep fighting behind the menus.
        // (Start screen only: behind the busy results panel it just added clutter.)
        if (mood == Mood.Menu)
        {
            gameObject.AddComponent<SpaceBattleBackdrop>();
        }

        if (mood == Mood.Menu)
        {
            BuildHeroShip();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            launchClip = Own(CreateLaunchClip());
        }
    }

    private void OnDestroy()
    {
        if (Active == this)
        {
            Active = null;
        }

        for (int i = 0; i < ownedAssets.Count; i++)
        {
            if (ownedAssets[i])
            {
                Destroy(ownedAssets[i]);
            }
        }

        ownedAssets.Clear();
    }

    private T Own<T>(T asset) where T : Object
    {
        if (asset)
        {
            ownedAssets.Add(asset);
        }

        return asset;
    }

    // ---------------------------------------------------------------- public hooks

    /// <summary>Rebuilds the hovering ship so a newly chosen colour shows immediately.</summary>
    public void RefreshHeroShip()
    {
        if (mood != Mood.Menu)
        {
            return;
        }

        if (shipHolder)
        {
            Destroy(shipHolder.gameObject);
            shipHolder = null;
        }

        BuildShipModel();
    }

    /// <summary>Start of a run: the ship punches forward into the distance and the stars go to warp.</summary>
    public void PlayLaunch()
    {
        if (launchTime >= 0f)
        {
            return;
        }

        launchTime = Time.unscaledTime;
        if (audioSource && launchClip && FeedbackPreferences.IsAudioEnabled(null))
        {
            audioSource.PlayOneShot(launchClip, 0.7f);
        }
    }

    // ---------------------------------------------------------------- camera, sky, light

    private void SetupCamera()
    {
        viewCamera = Camera.main;
        if (!viewCamera)
        {
            viewCamera = FindObjectOfType<Camera>();
        }

        if (!viewCamera)
        {
            var cameraObject = new GameObject("CosmosCamera") { tag = "MainCamera" };
            SceneManager.MoveGameObjectToScene(cameraObject, gameObject.scene);
            viewCamera = cameraObject.AddComponent<Camera>();
        }

        baseCameraRotation = Quaternion.Euler(CameraPitch, 0f, 0f);
        viewCamera.transform.SetPositionAndRotation(ViewOrigin, baseCameraRotation);
        viewCamera.fieldOfView = FieldOfView;
        viewCamera.nearClipPlane = 0.1f;
        viewCamera.farClipPlane = 2400f;
    }

    private void SetupSkyAndLight()
    {
        bool menu = mood == Mood.Menu;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = menu ? new Color(0.16f, 0.17f, 0.28f) : new Color(0.22f, 0.12f, 0.12f);

        Shader skyShader = Resources.Load<Shader>(SkyShaderPath);
        if (skyShader)
        {
            var sky = Own(new Material(skyShader));
            sky.SetColor("_Tint", menu ? new Color(0.02f, 0.018f, 0.06f) : new Color(0.05f, 0.012f, 0.02f));
            sky.SetColor("_NebulaColorA", menu ? new Color(0.55f, 0.12f, 0.7f) : new Color(0.75f, 0.16f, 0.08f));
            sky.SetColor("_NebulaColorB", menu ? new Color(0.05f, 0.42f, 0.7f) : new Color(0.42f, 0.06f, 0.24f));
            sky.SetFloat("_NebulaStrength", menu ? 0.8f : 0.7f);
            sky.SetFloat("_StarDensity", 0.75f);
            sky.SetFloat("_StarBrightness", 2.2f);
            RenderSettings.skybox = sky;
            viewCamera.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = menu ? new Color(0.01f, 0.01f, 0.04f) : new Color(0.03f, 0.008f, 0.015f);
        }

        Light sun = null;
        foreach (Light light in FindObjectsOfType<Light>())
        {
            if (light.type == LightType.Directional && light.gameObject.scene == gameObject.scene)
            {
                sun = light;
                break;
            }
        }

        if (!sun)
        {
            var sunObject = new GameObject("CosmosSun");
            sunObject.transform.SetParent(transform, false);
            sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        // Same sun as the battle backdrop: high on the left, slightly behind the camera.
        Vector3 toSun = new Vector3(-0.62f, 0.5f, -0.35f).normalized;
        sun.transform.rotation = Quaternion.LookRotation(-toSun);
        sun.color = menu ? new Color(1f, 0.93f, 0.85f) : new Color(1f, 0.62f, 0.45f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.None;
    }

    // Half the horizontal field of view in degrees -- a portrait phone sees far less to the sides.
    private float HalfHorizontalFov()
    {
        float aspect = viewCamera ? Mathf.Max(0.3f, viewCamera.aspect) : 0.5625f;
        return Mathf.Atan(Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
    }

    private Vector3 CameraPoint(float azimuth, float elevation, float distance)
    {
        return ViewOrigin + baseCameraRotation * Quaternion.Euler(-elevation, azimuth, 0f) * Vector3.forward * distance;
    }

    // ---------------------------------------------------------------- planet

    private void BuildPlanet()
    {
        Shader propShader = Resources.Load<Shader>(PropShaderPath);
        if (!propShader)
        {
            return;
        }

        bool menu = mood == Mood.Menu;
        Vector3 toSun = new Vector3(-0.62f, 0.5f, -0.35f).normalized;
        Mesh sphere = Own(SpaceBattleBackdrop.CreateUvSphereMesh(96, 56));

        var planetMaterial = Own(new Material(propShader));
        planetMaterial.SetColor("_Color", Color.white);
        planetMaterial.mainTexture = Own(menu ? CreateGasGiantTexture() : CreateMagmaTexture());
        planetMaterial.SetColor("_EmissionColor", menu ? Color.black : new Color(1.1f, 0.35f, 0.08f));
        planetMaterial.SetVector("_SunDir", toSun);
        planetMaterial.SetColor("_Ambient", menu ? new Color(0.05f, 0.04f, 0.09f) : new Color(0.06f, 0.025f, 0.025f));
        planetMaterial.SetColor("_RimColor", menu ? new Color(0.35f, 0.75f, 1.3f) : new Color(1.4f, 0.4f, 0.15f));
        planetMaterial.SetFloat("_RimPower", 2.4f);

        planet = new GameObject("Planet").transform;
        planet.SetParent(transform, false);
        planet.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
        var planetRenderer = planet.gameObject.AddComponent<MeshRenderer>();
        planetRenderer.sharedMaterial = planetMaterial;
        planetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        planet.rotation = Quaternion.Euler(18f, 30f, -12f);

        if (glowMaterial)
        {
            var ringMaterial = Own(new Material(glowMaterial.shader));
            ringMaterial.mainTexture = Own(CreateRingTexture());
            ringMaterial.SetColor("_Color", menu ? new Color(0.75f, 0.62f, 1.05f) : new Color(1.2f, 0.45f, 0.2f));
            planetRing = new GameObject("PlanetRing").transform;
            planetRing.SetParent(transform, false);
            planetRing.gameObject.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Ring();
            var ringRenderer = planetRing.gameObject.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var moonMaterial = Own(new Material(propShader));
        moonMaterial.SetColor("_Color", menu ? new Color(0.78f, 0.8f, 0.86f) : new Color(0.5f, 0.42f, 0.4f));
        moonMaterial.mainTexture = ProceduralRockMeshes.RockTexture();
        moonMaterial.SetColor("_EmissionColor", Color.black);
        moonMaterial.SetVector("_SunDir", toSun);
        moonMaterial.SetColor("_RimColor", menu ? new Color(0.2f, 0.3f, 0.5f) : new Color(0.5f, 0.15f, 0.1f));
        moon = new GameObject("Moon").transform;
        moon.SetParent(transform, false);
        moon.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
        var moonRenderer = moon.gameObject.AddComponent<MeshRenderer>();
        moonRenderer.sharedMaterial = moonMaterial;
        moonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        LayoutPlanet();
    }

    // Placed relative to the visible frame so it sits in the upper part of the screen in portrait
    // and in landscape alike (re-run when the phone rotates).
    private void LayoutPlanet()
    {
        if (!planet || !viewCamera)
        {
            return;
        }

        layoutAspect = viewCamera.aspect;
        float halfFov = HalfHorizontalFov();
        const float distance = 900f;
        bool menu = mood == Mood.Menu;
        // Kept small and up in a corner so it frames the screen instead of filling it.
        float angularRadius = Mathf.Clamp(halfFov * 0.22f, 4.5f, 7.5f);
        float azimuth = (halfFov - angularRadius * 0.6f) * (menu ? 1f : -1f);
        float elevation = FieldOfView * 0.5f - angularRadius * 0.4f;
        planetRadius = distance * Mathf.Tan(angularRadius * Mathf.Deg2Rad);

        planet.position = CameraPoint(azimuth, elevation, distance);
        planet.localScale = Vector3.one * planetRadius * 2f;

        if (planetRing)
        {
            planetRing.position = planet.position;
            planetRing.rotation = baseCameraRotation * Quaternion.AngleAxis(-20f, Vector3.forward) * Quaternion.AngleAxis(-14f, Vector3.right);
            planetRing.localScale = Vector3.one * planetRadius * 4.6f;
        }
    }

    // ---------------------------------------------------------------- asteroids

    private void BuildRocks()
    {
        Shader standard = Shader.Find("Standard");
        if (!standard)
        {
            return;
        }

        bool menu = mood == Mood.Menu;
        rockMaterial = Own(new Material(standard));
        rockMaterial.mainTexture = ProceduralRockMeshes.RockTexture();
        rockMaterial.color = menu ? new Color(0.58f, 0.55f, 0.6f) : new Color(0.32f, 0.26f, 0.25f);
        rockMaterial.SetFloat("_Glossiness", 0.08f);

        if (!menu)
        {
            // Scorched wreckage: hot glowing edges.
            emberRockMaterial = Own(new Material(standard));
            emberRockMaterial.mainTexture = ProceduralRockMeshes.RockTexture();
            emberRockMaterial.color = new Color(0.22f, 0.16f, 0.15f);
            emberRockMaterial.SetFloat("_Glossiness", 0.1f);
            emberRockMaterial.EnableKeyword("_EMISSION");
            emberRockMaterial.SetColor("_EmissionColor", new Color(0.9f, 0.25f, 0.05f));
        }

        var random = new System.Random(menu ? 31 : 77);
        for (int i = 0; i < RockCount; i++)
        {
            var go = new GameObject("CosmosRock");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Rock(i);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = emberRockMaterial && i % 3 == 0 ? emberRockMaterial : rockMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 3.4f, (float)random.NextDouble());
            go.transform.rotation = Random.rotation;

            var rock = new Rock
            {
                Transform = go.transform,
                Spin = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 70f,
                Speed = menu ? Mathf.Lerp(5f, 11f, (float)random.NextDouble()) : Mathf.Lerp(0.6f, 1.6f, (float)random.NextDouble()),
            };
            ResetRock(rock, random, Mathf.Lerp(8f, 70f, i / (float)(RockCount - 1)));
            rocks.Add(rock);
        }
    }

    private static void ResetRock(Rock rock, System.Random random, float depth)
    {
        // Keep them toward the screen edges so they frame the view instead of covering the ship.
        float side = random.NextDouble() < 0.5 ? -1f : 1f;
        rock.Lateral = side * Mathf.Lerp(0.6f, 1.15f, (float)random.NextDouble());
        rock.Vertical = Mathf.Lerp(-1.05f, 1.05f, (float)random.NextDouble());
        rock.Depth = depth;
    }

    private void UpdateRocks(float dt, float warp)
    {
        if (rocks.Count == 0 || !viewCamera)
        {
            return;
        }

        float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * Mathf.Max(0.3f, viewCamera.aspect);
        for (int i = 0; i < rocks.Count; i++)
        {
            Rock rock = rocks[i];
            rock.Depth -= rock.Speed * warp * dt;
            if (rock.Depth < 3f)
            {
                ResetRock(rock, new System.Random(Random.Range(0, int.MaxValue)), Random.Range(60f, 80f));
            }

            Vector3 local = new Vector3(rock.Lateral * tanH * rock.Depth, rock.Vertical * tanV * rock.Depth, rock.Depth);
            rock.Transform.position = ViewOrigin + baseCameraRotation * local;
            rock.Transform.Rotate(rock.Spin * dt, Space.Self);
        }
    }

    // ---------------------------------------------------------------- warp streaks / embers

    private void BuildStreaks()
    {
        if (!glowMaterial || !viewCamera)
        {
            return;
        }

        bool menu = mood == Mood.Menu;
        var go = new GameObject(menu ? "WarpStreaks" : "Embers");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(ViewOrigin + baseCameraRotation * new Vector3(0f, 0f, menu ? 90f : 20f), baseCameraRotation);
        streaks = go.AddComponent<ParticleSystem>();
        streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        streaksMain = streaks.main;
        streaksMain.loop = true;
        streaksMain.playOnAwake = true;
        streaksMain.simulationSpace = ParticleSystemSimulationSpace.World;
        streaksMain.maxParticles = 400;
        streaksMain.startLifetime = menu ? 1.5f : 9f;
        streaksMain.startSpeed = 0f;
        streaksMain.startSize = menu ? new ParticleSystem.MinMaxCurve(0.06f, 0.16f) : new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
        streaksMain.startColor = menu
            ? new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.85f, 1f, 0.75f), new Color(0.9f, 0.7f, 1f, 0.75f))
            : new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.25f, 0.08f, 0.9f));

        var emission = streaks.emission;
        emission.rateOverTime = menu ? 70f : 26f;

        var shape = streaks.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = menu ? new Vector3(140f, 90f, 4f) : new Vector3(40f, 34f, 20f);

        var velocity = streaks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        if (menu)
        {
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-70f, -55f);
        }
        else
        {
            // Embers drift up and sideways like sparks over a burning wreck.
            velocity.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-1.5f, 0.5f);
        }

        var fade = streaks.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (menu)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.09f;
            renderer.lengthScale = 2f;
        }

        streaks.Play();
    }

    // ---------------------------------------------------------------- hero ship

    private void BuildHeroShip()
    {
        shipPivot = new GameObject("HeroShipPivot").transform;
        shipPivot.SetParent(transform, false);
        BuildShipModel();
    }

    private void BuildShipModel()
    {
        var prefab = Resources.Load<GameObject>(HeroShipPath);
        if (!prefab || !shipPivot)
        {
            return;
        }

        // Same holder/model arrangement as the in-game ship so ShipDetailing/ShipThruster line up.
        var holder = new GameObject("Visual");
        holder.transform.SetParent(shipPivot, false);
        holder.transform.localScale = Vector3.one * 1.18f;
        GameObject model = Instantiate(prefab, holder.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * 0.35f;
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            Destroy(collider);
        }

        holder.AddComponent<ShipVisualTint>();
        holder.AddComponent<ShipThruster>();
        holder.AddComponent<ShipDetailing>();
        shipHolder = holder.transform;
    }

    private void UpdateHeroShip(float time, float launch)
    {
        if (!shipPivot || !viewCamera)
        {
            return;
        }

        // Sized so the ship spans about half the screen width on a portrait phone and stays a
        // sensible size in landscape; sits just below the middle of the screen.
        float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * Mathf.Max(0.3f, viewCamera.aspect);
        const float shipWidth = 1.25f;
        float widthFraction = viewCamera.aspect < 1f ? 0.5f : 0.24f;
        float distance = shipWidth / (2f * tanH * widthFraction);
        float bob = Mathf.Sin(time * 1.3f) * 0.06f * distance / 6f;

        Vector3 local = new Vector3(0f, -tanV * distance * 0.06f + bob, distance);
        float yaw = 28f + Mathf.Sin(time * 0.37f) * 16f;
        float roll = -Mathf.Sin(time * 0.37f) * 9f + Mathf.Sin(time * 1.1f) * 2.5f;
        float pitch = -6f + Mathf.Sin(time * 0.8f) * 2f;

        if (launch > 0f)
        {
            // Blast-off: a quick wind-up, then straight ahead into the distance.
            float surge = launch * launch * 160f;
            local.z += surge;
            local.y += launch * 1.2f;
            yaw = Mathf.Lerp(yaw, 0f, Mathf.Clamp01(launch * 3f));
            roll = Mathf.Lerp(roll, 0f, Mathf.Clamp01(launch * 3f));
        }

        shipPivot.SetPositionAndRotation(
            ViewOrigin + baseCameraRotation * local,
            baseCameraRotation * Quaternion.Euler(pitch, yaw, roll));
    }

    // ---------------------------------------------------------------- per frame

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;
        float launch = launchTime >= 0f ? Mathf.Clamp01((time - launchTime) / 0.7f) : 0f;
        float warp = 1f + launch * 6f;

        if (viewCamera && !Mathf.Approximately(viewCamera.aspect, layoutAspect))
        {
            LayoutPlanet();
        }

        if (planet)
        {
            planet.Rotate(Vector3.up, dt * 1.6f, Space.Self);
        }

        if (moon && planet)
        {
            float orbit = time * 0.05f;
            Vector3 offset = baseCameraRotation * new Vector3(Mathf.Cos(orbit) * 2.4f, Mathf.Sin(orbit) * 0.35f - 0.3f, -Mathf.Sin(orbit) * 2.4f) * planetRadius;
            moon.position = planet.position + offset;
            moon.localScale = Vector3.one * planetRadius * 0.36f;
        }

        UpdateRocks(dt, warp);
        UpdateHeroShip(time, launch);

        if (streaks)
        {
            streaksMain.simulationSpeed = warp;
        }

        if (viewCamera)
        {
            // A slow, barely-there drift so the scene feels like it is floating, plus a kick on launch.
            float driftX = (Mathf.PerlinNoise(time * 0.07f, 0.3f) - 0.5f) * 1.4f;
            float driftY = (Mathf.PerlinNoise(0.7f, time * 0.07f) - 0.5f) * 1f;
            float shake = launch > 0f && launch < 1f ? (1f - launch) * 0.25f : 0f;
            viewCamera.transform.rotation = baseCameraRotation
                * Quaternion.Euler(driftY + Random.Range(-shake, shake), driftX + Random.Range(-shake, shake), 0f);
            viewCamera.fieldOfView = FieldOfView + launch * 14f;
        }
    }

    // ---------------------------------------------------------------- generated textures / sound

    private static Texture2D CreateGasGiantTexture()
    {
        const int width = 256;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "GasGiant", wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[width * height];
        var deep = new Color(0.16f, 0.07f, 0.34f);
        var teal = new Color(0.12f, 0.55f, 0.72f);
        var magenta = new Color(0.72f, 0.22f, 0.62f);
        var pale = new Color(0.86f, 0.78f, 0.95f);
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)height;
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                // Bands warped by noise so they swirl like storms rather than ruler-straight stripes.
                float warp = (Mathf.PerlinNoise(u * 6f, v * 9f) - 0.5f) * 0.09f;
                float band = Mathf.Sin((v + warp) * 34f) * 0.5f + 0.5f;
                float band2 = Mathf.Sin((v + warp * 1.6f) * 11f + 1.3f) * 0.5f + 0.5f;
                Color color = Color.Lerp(deep, teal, band2);
                color = Color.Lerp(color, magenta, band * 0.45f);
                float storm = Mathf.PerlinNoise(u * 22f + 3f, v * 30f);
                color = Color.Lerp(color, pale, Mathf.Clamp01(storm - 0.62f) * 1.6f);
                // A great storm eye.
                float eye = Vector2.Distance(new Vector2(u * 2f, v), new Vector2(0.62f, 0.38f));
                color = Color.Lerp(color, new Color(0.95f, 0.5f, 0.75f), Mathf.Clamp01(1f - eye / 0.05f) * 0.7f);
                color.a = 0f;
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    // Dark crust with glowing lava cracks in the emission mask (alpha).
    private static Texture2D CreateMagmaTexture()
    {
        const int width = 256;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "MagmaWorld", wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)height;
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                float n = Mathf.PerlinNoise(u * 14f, v * 10f);
                float ridge = 1f - Mathf.Abs(n - 0.5f) * 2f;
                float crack = Mathf.Clamp01((ridge - 0.93f) * 10f);
                float crust = 0.25f + Mathf.PerlinNoise(u * 40f + 7f, v * 30f) * 0.25f;
                pixels[y * width + x] = new Color(crust * 1.1f, crust * 0.8f, crust * 0.75f, crack);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    // Ring bands across the radius (v), in the alpha the glow shader masks with.
    private static Texture2D CreateRingTexture()
    {
        const int width = 4;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "PlanetRingBands", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float bands = 0.35f + 0.65f * Mathf.PerlinNoise(v * 18f, 0.5f);
            float gap = Mathf.Abs(v - 0.62f) < 0.035f ? 0.1f : 1f;
            float edge = Mathf.Clamp01(v * 8f) * Mathf.Clamp01((1f - v) * 8f);
            float alpha = bands * gap * edge * 0.55f;
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // Rising engine roar: filtered noise sweeping up plus a low rumble.
    private static AudioClip CreateLaunchClip()
    {
        const int rate = 44100;
        const float seconds = 1.1f;
        int length = (int)(rate * seconds);
        var data = new float[length];
        var random = new System.Random(5);
        float low = 0f;
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float noise = (float)random.NextDouble() * 2f - 1f;
            float cutoff = Mathf.Lerp(0.02f, 0.35f, t);
            low += (noise - low) * cutoff;
            phase += 2f * Mathf.PI * Mathf.Lerp(55f, 140f, t) / rate;
            float envelope = Mathf.Min(1f, t * 6f) * (1f - Mathf.Pow(t, 3f));
            data[i] = (low * 0.8f + Mathf.Sin(phase) * 0.35f) * envelope * 0.6f;
        }

        var clip = AudioClip.Create("LaunchRoar", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
