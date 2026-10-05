using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a lightweight moving marker layer to the existing three-lane environment
/// to improve perceived speed/depth without changing gameplay timings.
/// </summary>
[DisallowMultipleComponent]
public sealed class RushTrackEnvironment : MonoBehaviour
{
    private const string GameSceneName = SceneNames.Game;
    private const string EnvironmentName = "Environment";
    private const float TrackCenterZ = 5f;
    // How much brighter than its base color the glowing vein/crystal/beacon material's emission
    // is driven -- purely cosmetic (bloom-only), read by the zone-color drift in Update().
    private const float VeinEmissionIntensity = 1.6f;
    private const string SpaceSkyShaderPath = "Shaders/TimeRushSpaceSky";
    private const string SkyTintProperty = "_Tint";
    // Zone sky tints are authored for the old atmospheric sky; space itself stays much darker.
    private const float SpaceSkyTintScale = 0.45f;
    private const string GroundObjectName = "Ground";

    [Header("Accessibility")]
    [SerializeField] private FeedbackConfig feedbackConfig;

    [Header("Motion Bands")]
    // Scenery streams toward the camera along -Z (the direction the ship flies), from deep in the
    // fog to behind the camera, and grows in from nothing at the far end -- so nothing ever pops
    // into or out of view. (It used to fall straight down in Y beside the track, which read as
    // stacks of blocks sliding down a staircase.)
    [SerializeField, Range(6, 28)] private int asteroidsPerSide = 12;
    [SerializeField] private float sceneryNearZ = -16f;
    [SerializeField] private float sceneryFarZ = 78f;
    [SerializeField, Range(2f, 30f)] private float sceneryFadeLength = 14f;

    [Header("Near Speed Streaks")]
    [SerializeField, Range(4, 20)] private int streakRows = 8;
    [SerializeField] private float streakNearZ = -22f;
    [SerializeField] private float streakFarZ = 36f;

    [Header("Ambient Pace Escalation")]
    [Tooltip("Brightness ceiling for the shared cyan/purple glow materials at max pace -- a slow continuous escalation, never an oscillation.")]
    [SerializeField, Range(1f, 2.5f)] private float maxAmbientGlowBoost = 1.9f;

    [Header("Space Zone Transition")]
    [Tooltip("How quickly the asteroid field's rock/vein/debris colors drift toward the current distance zone (deep void/asteroid belt/ice field/nebula core). Purely cosmetic, driven only by GameController.CurrentScore.")]
    [SerializeField, Range(0.05f, 2f)] private float zoneTransitionSpeed = 0.35f;

    [Header("Depth Markers")]
    [Tooltip("Matches TrackLayoutConfig.SafeDepthRange -- the player's own late-game reachable depth range. Purely a fixed visual reference; never reads player/gameplay state.")]
    [SerializeField] private float depthMarkerReachRange = 2f;

    [Header("Speed Dust")]
    [Tooltip("Fine glowing dust streaking past the ship -- an extra nearest-band speed cue on top of the cube speed streaks. Purely cosmetic.")]
    [SerializeField, Range(20f, 120f)] private float dustEmissionRate = 55f;
    [SerializeField, Range(0.4f, 1.6f)] private float dustLifetime = 0.85f;

    [Header("Ambient Asteroid Flyby")]
    [Tooltip("Occasional very light camera jolt suggesting a large asteroid just swept past close by -- purely cosmetic flavor on a soft timer, never derived from real collision/near-miss detection or gameplay state.")]
    [SerializeField, Range(3f, 15f)] private float ambientJoltMinInterval = 5f;
    [SerializeField, Range(4f, 22f)] private float ambientJoltMaxInterval = 9.5f;

    [Header("Track Presentation")]
    [SerializeField] private float trackHalfWidth = 5.4f;
    [SerializeField] private float trackLength = 64f;
    [SerializeField] private float sideRailX = 6.35f;
    [SerializeField] private float outerPillarX = 9.8f;

    [Header("Fallback Column Geometry")]

    private MovingElement[] sidePanels = System.Array.Empty<MovingElement>();
    private MovingElement[] speedStreaks = System.Array.Empty<MovingElement>();
    private Transform visualRoot;
    private Transform sidePanelRoot;
    private Transform streakRoot;
    private Material trackSurfaceMaterial;
    private Material laneGlowMaterial;
    private Material structuralMaterial;
    private Material energyAccentMaterial;
    private Material asteroidBodyMaterial;
    private Material asteroidVeinMaterial;
    private Material debrisMaterial;
    private Material skyboxMaterial;
    private Material laneFlowMaterial;
    private Color laneGlowBaseColor;
    private Color energyAccentBaseColor;
    private Color asteroidBodyColor;
    private Color asteroidVeinColor;
    private Color debrisColor;
    private Color skyboxTintColor;
    private float ambientGlowBrightness = 1f;
    private ParticleSystem speedDustSystem;
    private ParticleSystem starfieldSystem;
    private ParticleSystem nebulaSystem;
    private float ambientJoltTimer;
    // Scrolling offset for the procedural track-grid texture, advanced every frame by the same
    // markerSpeed every other band already uses -- so the ground itself visibly streams past
    // instead of looking like a static painted floor.
    private float trackScrollY;
    // Same idea for the lane-guidance lines' flowing energy-pulse texture.
    private float laneFlowScrollY;
    private bool usesSpaceSky;
    private bool built;

    // Distance/score-driven space palette so the passing asteroid field visibly changes
    // character over a long run instead of repeating the same rock forever -- the same
    // threshold pattern SceneMoodController already uses for the light/fog mood, so the two
    // reinforce each other. Geometry (shape) variety is separate and handled per-cluster in
    // BuildAsteroidCluster; this only drives the shared rock/vein/debris material colors.
    private readonly struct SpaceZone
    {
        public readonly int ScoreThreshold;
        public readonly Color BodyColor;
        public readonly Color AccentColor;
        public readonly Color RoofColor;
        // Skybox tint for this zone -- kept as its own color (rather than reusing BodyColor) so the
        // backdrop can stay dark/space-like while still drifting hue with the same zone timing as
        // the rock/vein/debris palette, reinforcing "the ship is somewhere different now."
        public readonly Color SkyTint;

        public SpaceZone(int scoreThreshold, Color bodyColor, Color accentColor, Color roofColor, Color skyTint)
        {
            ScoreThreshold = scoreThreshold;
            BodyColor = bodyColor;
            AccentColor = accentColor;
            RoofColor = roofColor;
            SkyTint = skyTint;
        }
    }

    private static readonly SpaceZone[] SpaceZones =
    {
        // Deep Void -- the run's opening feel: cool basalt-grey rock, pale starlight veins, near-black indigo sky.
        new SpaceZone(0, new Color(0.16f, 0.17f, 0.22f), new Color(0.55f, 0.85f, 1f), new Color(0.5f, 0.55f, 0.62f), new Color(0.03f, 0.035f, 0.07f)),
        // Asteroid Belt -- sun-scorched rust rock, glowing amber mineral veins, burnt-orange debris, warm dark-rust sky glow.
        new SpaceZone(400, new Color(0.42f, 0.28f, 0.2f), new Color(1f, 0.65f, 0.25f), new Color(0.75f, 0.4f, 0.18f), new Color(0.08f, 0.035f, 0.02f)),
        // Ice Comet Field -- pale frozen rock, bright cyan crystal veins, frosted metal debris, dark icy-teal sky.
        new SpaceZone(900, new Color(0.62f, 0.72f, 0.82f), new Color(0.35f, 0.9f, 1f), new Color(0.72f, 0.83f, 0.9f), new Color(0.02f, 0.05f, 0.07f)),
        // Nebula Core -- a rare, hard-earned late-run payoff: violet glowing rock, hot pink-gold veins, deep violet sky.
        new SpaceZone(1600, new Color(0.4f, 0.22f, 0.5f), new Color(1f, 0.5f, 0.85f), new Color(0.85f, 0.55f, 0.75f), new Color(0.06f, 0.02f, 0.08f)),
    };

    private static SpaceZone ResolveSpaceZone(int score)
    {
        SpaceZone result = SpaceZones[0];
        for (int i = 0; i < SpaceZones.Length; i++)
        {
            if (score >= SpaceZones[i].ScoreThreshold)
            {
                result = SpaceZones[i];
            }
        }

        return result;
    }

    private struct MovingElement
    {
        public Transform Transform;
        public Vector3 BaseScale;
        public float SpeedMultiplier;
        // Travel range along local Z: elements move from MaxZ toward MinZ and wrap.
        public float MinZ;
        public float MaxZ;
        // Slow constant self-rotation, used only by the asteroid field so tumbling rocks/debris
        // reinforce "the ship is actually moving through space" rather than just drifting past
        // flat and static. Left at the struct default (zero) for the speed streaks.
        public Vector3 RotationAxis;
        public float RotationSpeed;
    }

    // RuntimeInitializeOnLoadMethod fires exactly once per process, but Game.unity's own
    // "Environment" GameObject is scene-local (not DontDestroyOnLoad) and is destroyed and
    // recreated fresh every time Game.unity (re)loads -- e.g. Restart or Continue after a
    // Results screen. Without also hooking SceneManager.sceneLoaded, only whichever Game.unity
    // load happened to be first in the process ever got this component attached; every later
    // reload silently lost the tunnel/environment presentation while gameplay kept running fine.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForGameScene()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        TryInstall(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (scene.name != GameSceneName)
        {
            return;
        }

        if (FindObjectOfType<RushTrackEnvironment>() != null)
        {
            return;
        }

        GameObject environment = GameObject.Find(EnvironmentName);
        if (!environment)
        {
            return;
        }

        environment.AddComponent<RushTrackEnvironment>();
    }

    private void Awake()
    {
        BuildIfNeeded();
    }

    // Every material/texture this component owns was created at runtime via "new Material(...)"/
    // "new Texture2D(...)" -- Unity does NOT automatically free those when the GameObject holding
    // them is destroyed (unlike imported/shared assets), so without this, every Restart/Continue
    // (which destroys and rebuilds "Environment" from scratch) would leak a full set of materials
    // and textures for the rest of the process's lifetime. Mobile devices have far less headroom
    // for that than a desktop Editor, so this matters far more here than it would look like it does.
    private void OnDestroy()
    {
        DestroyMaterialAndTexture(trackSurfaceMaterial);
        DestroyMaterialAndTexture(laneFlowMaterial);
        DestroyIfOwned(laneGlowMaterial);
        DestroyIfOwned(structuralMaterial);
        DestroyIfOwned(energyAccentMaterial);
        DestroyIfOwned(asteroidBodyMaterial);
        DestroyIfOwned(asteroidVeinMaterial);
        DestroyIfOwned(debrisMaterial);
        DestroyIfOwned(skyboxMaterial);

        DestroyParticleRendererMaterial(speedDustSystem);
        DestroyParticleRendererMaterial(starfieldSystem);
        DestroyParticleRendererMaterial(nebulaSystem);
    }

    private static void DestroyIfOwned(Object runtimeObject)
    {
        if (runtimeObject)
        {
            Destroy(runtimeObject);
        }
    }

    private static void DestroyMaterialAndTexture(Material material)
    {
        if (!material)
        {
            return;
        }

        if (material.mainTexture)
        {
            Destroy(material.mainTexture);
        }

        Destroy(material);
    }

    // SoftParticleMaterial.Create() hands back a fresh Material instance per call but always the
    // same cached, shared texture (also used by ShipThruster) -- so only the material is ours to
    // free here; the texture must never be destroyed from this component.
    private static void DestroyParticleRendererMaterial(ParticleSystem system)
    {
        if (!system)
        {
            return;
        }

        var particleRenderer = system.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer && particleRenderer.sharedMaterial)
        {
            Destroy(particleRenderer.sharedMaterial);
        }
    }

    private void OnValidate()
    {
        asteroidsPerSide = Mathf.Clamp(asteroidsPerSide, 6, 28);
        if (sceneryFarZ <= sceneryNearZ + 10f)
        {
            sceneryFarZ = sceneryNearZ + 10f;
        }
        streakRows = Mathf.Clamp(streakRows, 4, 20);
        if (streakFarZ <= streakNearZ + 10f)
        {
            streakFarZ = streakNearZ + 10f;
        }
        depthMarkerReachRange = Mathf.Max(0.5f, depthMarkerReachRange);
        trackHalfWidth = Mathf.Max(4f, trackHalfWidth);
        trackLength = Mathf.Max(40f, trackLength);
        sideRailX = Mathf.Max(trackHalfWidth + 0.4f, sideRailX);
        outerPillarX = Mathf.Max(sideRailX + 1.25f, outerPillarX);
    }

    private void Update()
    {
        if (!built)
        {
            return;
        }

        bool gameplayActive = GameStateMachine.IsGameplayInputAllowed;
        if (!gameplayActive)
        {
            return;
        }

        var gc = GameController.Instance;
        float obstacleSpeed = gc ? gc.GetObstacleSpeed() : 4.1f;
        float rawPace = gc ? gc.GetPaceMultiplier() : 1f;
        bool cameraShakeEnabled = FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig);

        // Same accessibility-gated effective pace the FOV/marker-speed cues already use, reused
        // here so the ambient glow escalation never bypasses the Camera Shake preference either.
        float effectivePace = SpeedPerceptionMath.ResolveEffectivePace(rawPace, gameplayActive, cameraShakeEnabled);
        float targetGlowBrightness = RushTrackPerceptionMath.ComputeAmbientGlowBrightness(effectivePace, maxAmbientGlowBoost);
        ambientGlowBrightness = Mathf.Lerp(ambientGlowBrightness, targetGlowBrightness, 2.5f * Time.deltaTime);
        laneGlowMaterial.color = laneGlowBaseColor * ambientGlowBrightness;
        energyAccentMaterial.color = energyAccentBaseColor * ambientGlowBrightness;
        if (laneFlowMaterial)
        {
            laneFlowMaterial.color = Color.white * Mathf.Clamp(ambientGlowBrightness, 0.6f, 2f);
        }

        // Slowly drift the asteroid field's shared rock/vein/debris colors toward whatever space
        // zone the current score falls into -- deep void, asteroid belt, ice field, nebula core --
        // so a long run visibly travels through changing space instead of looking identical
        // throughout, echoing the "the ship is actually going somewhere" ask directly.
        SpaceZone targetZone = ResolveSpaceZone(gc ? gc.CurrentScore : 0);
        float zoneT = Time.deltaTime * zoneTransitionSpeed;
        asteroidBodyColor = Color.Lerp(asteroidBodyColor, targetZone.BodyColor, zoneT);
        asteroidVeinColor = Color.Lerp(asteroidVeinColor, targetZone.AccentColor, zoneT);
        debrisColor = Color.Lerp(debrisColor, targetZone.RoofColor, zoneT);
        asteroidBodyMaterial.color = asteroidBodyColor;
        debrisMaterial.color = debrisColor;
        // Vein material is emissive (see CreateEmissiveMaterial) so both its dim base tint and its
        // glow color need updating together, or the crystal would drift in base color but keep
        // glowing the old zone's hue.
        asteroidVeinMaterial.color = asteroidVeinColor * 0.6f;
        asteroidVeinMaterial.SetColor("_EmissionColor", asteroidVeinColor * VeinEmissionIntensity);

        // Same zone-drift timing applied to the skybox backdrop so the atmosphere itself visibly
        // shifts hue alongside the rocks/lighting instead of staying a fixed, generic sky forever.
        if (skyboxMaterial)
        {
            skyboxTintColor = Color.Lerp(skyboxTintColor, targetZone.SkyTint, zoneT);
            if (usesSpaceSky)
            {
                skyboxMaterial.SetColor(SkyTintProperty, skyboxTintColor * SpaceSkyTintScale);
            }
            else
            {
                skyboxMaterial.SetColor("_SkyTint", skyboxTintColor);
                skyboxMaterial.SetColor("_GroundColor", skyboxTintColor * 0.35f);
            }
        }

        // Purely a flavor timer -- ticks whenever gameplay input is active, independent of the
        // marker-speed early-out below, so it can still fire during brief zero-speed moments.
        ambientJoltTimer -= Time.deltaTime;
        if (ambientJoltTimer <= 0f)
        {
            ambientJoltTimer = Random.Range(ambientJoltMinInterval, ambientJoltMaxInterval);
            GameFeedbackSignals.RaiseAmbientCameraJolt(Random.Range(0.5f, 1f));
        }

        float markerSpeed = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(obstacleSpeed, rawPace, gameplayActive, cameraShakeEnabled);

        if (speedDustSystem)
        {
            var dustVelocity = speedDustSystem.velocityOverLifetime;
            dustVelocity.z = new ParticleSystem.MinMaxCurve(-Mathf.Max(4f, markerSpeed * 2.4f));
        }

        // Scroll the procedural grid texture along the track direction so the ground reads as
        // actual moving terrain instead of a flat static color -- independent of the markerSpeed
        // early-out below so the very first frames after a restart still show correct scroll.
        if (trackSurfaceMaterial)
        {
            trackScrollY -= markerSpeed * Time.deltaTime * 0.12f;
            trackSurfaceMaterial.mainTextureOffset = new Vector2(0f, trackScrollY);
        }

        // Same scroll trick, faster, on the lane-guidance lines -- reads as energy actively
        // flowing down the corridor toward the ship rather than a static glowing stripe.
        if (laneFlowMaterial)
        {
            laneFlowScrollY -= markerSpeed * Time.deltaTime * 0.4f;
            laneFlowMaterial.mainTextureOffset = new Vector2(0f, laneFlowScrollY);
        }

        if (markerSpeed <= 0.01f)
        {
            return;
        }

        AdvanceElements(sidePanels, markerSpeed, Time.deltaTime);
        AdvanceElements(speedStreaks, markerSpeed, Time.deltaTime);
    }

    private void AdvanceElements(MovingElement[] elements, float baseSpeed, float deltaTime)
    {
        for (int i = 0; i < elements.Length; i++)
        {
            MovingElement element = elements[i];
            if (!element.Transform)
            {
                continue;
            }

            Vector3 local = element.Transform.localPosition;
            float moveDistance = baseSpeed * element.SpeedMultiplier * deltaTime;
            local.z = RushTrackPerceptionMath.WrapHeight(local.z - moveDistance, element.MinZ, element.MaxZ);
            element.Transform.localPosition = local;

            // Grow in from nothing at the far end (and shrink away at the near end) so the wrap
            // from near back to far is never visible as a pop.
            float fade = Mathf.Min(
                Mathf.Clamp01((element.MaxZ - local.z) / sceneryFadeLength),
                Mathf.Clamp01((local.z - element.MinZ) / 3f));
            element.Transform.localScale = element.BaseScale * fade;

            if (element.RotationSpeed != 0f)
            {
                element.Transform.Rotate(element.RotationAxis, element.RotationSpeed * deltaTime, Space.Self);
            }
        }
    }

    private void BuildIfNeeded()
    {
        if (built)
        {
            return;
        }

        BuildMaterials();

        visualRoot = new GameObject("RushTrackVisuals").transform;
        visualRoot.SetParent(transform, false);

        sidePanelRoot = new GameObject("SidePanels").transform;
        sidePanelRoot.SetParent(visualRoot, false);

        streakRoot = new GameObject("SpeedStreaks").transform;
        streakRoot.SetParent(visualRoot, false);

        BuildTrackShell();

        BuildAsteroidField();
        BuildSpeedStreaks();
        BuildSpeedDust();
        BuildStarfield();
        BuildNebulaClouds();
        ApplySpaceAtmosphere();
        gameObject.AddComponent<SpaceBattleBackdrop>();
        ambientJoltTimer = Random.Range(ambientJoltMinInterval, ambientJoltMaxInterval);
        built = true;
    }

    private void BuildMaterials()
    {
        // Procedurally textured instead of flat Unlit/Color -- a faint glowing grid line pattern
        // gives the ground actual visual detail and, combined with the scroll in Update(), a real
        // sense of the surface streaming past under the ship.
        // Semi-transparent "glass" deck (alpha on the base, near-opaque grid lines) so the space
        // battle shows through the track instead of the ship riding on a solid slab.
        trackSurfaceMaterial = CreateGridMaterial(new Color(0.03f, 0.05f, 0.09f, 0.62f), new Color(0.22f, 0.5f, 0.62f, 0.9f));
        laneGlowMaterial = CreateUnlitMaterial(new Color(0.2f, 0.9f, 1f));
        // Dedicated flowing-energy texture for the 3 lane-guidance lines specifically -- separate
        // from laneGlowMaterial (still used by markers/rails/depth markers) so only the lines the
        // player actually steers between get the "energy actively flowing toward you" cue.
        laneFlowMaterial = CreateFlowMaterial(new Color(0.05f, 0.22f, 0.26f), new Color(0.55f, 1f, 1f));
        laneGlowBaseColor = laneGlowMaterial.color;
        structuralMaterial = CreateUnlitMaterial(new Color(0.12f, 0.16f, 0.24f));
        // Dim purple, echoing the existing boundary identity (M_Boundary) -- the previous bright
        // orange (1, 0.46, 0.16) sat too close to the obstacle color/emission and read as a second
        // hazard at a glance.
        energyAccentMaterial = CreateUnlitMaterial(new Color(0.32f, 0.2f, 0.52f));
        energyAccentBaseColor = energyAccentMaterial.color;

        // Dedicated asteroid-field materials, deliberately separate from the track-shell/lane
        // materials above -- so the space zone drift (void/belt/ice/nebula) only ever recolors
        // the passing rocks and debris, never the flight-corridor rails, lane guidance, or the
        // pace-driven glow.
        //
        // Unlike the track-shell materials (which stay Unlit for flat, always-readable gameplay
        // guidance), the asteroid body and debris use a lit shader so SceneMoodController's
        // directional light actually shades their faces -- this is what makes a primitive
        // cube/sphere read as a solid rock with real volume instead of a flat-colored cutout. The
        // vein/crystal/beacon material stays bright regardless of light angle via emission, so it
        // still reads as glowing even on the shape's shadowed side.
        SpaceZone startZone = SpaceZones[0];
        asteroidBodyMaterial = CreateLitMaterial(startZone.BodyColor, metallic: 0.05f, smoothness: 0.18f);
        debrisMaterial = CreateLitMaterial(startZone.RoofColor, metallic: 0.55f, smoothness: 0.45f);
        asteroidVeinMaterial = CreateEmissiveMaterial(startZone.AccentColor);
        asteroidBodyColor = asteroidBodyMaterial.color;
        asteroidVeinColor = startZone.AccentColor;
        debrisColor = debrisMaterial.color;

        // Replaces Unity's stock default skybox (the previous, unmodified backdrop) with a tuned
        // dark-space procedural sky -- tiny/near-invisible sun, thin atmosphere, dark zone-tinted
        // sky/ground -- so the "atmosphere" itself finally has an authored look instead of the
        // engine default, and drifts per-zone in Update() alongside everything else.
        Shader spaceSkyShader = Resources.Load<Shader>(SpaceSkyShaderPath);
        if (spaceSkyShader)
        {
            // Deep-space backdrop (crisp stars + faint nebula) instead of an atmospheric sky --
            // space has no horizon glow. The camera is switched to draw it in ApplySpaceAtmosphere.
            skyboxMaterial = new Material(spaceSkyShader);
            skyboxMaterial.SetColor(SkyTintProperty, startZone.SkyTint * SpaceSkyTintScale);
            skyboxTintColor = startZone.SkyTint;
            usesSpaceSky = true;
            RenderSettings.skybox = skyboxMaterial;
            return;
        }

        Shader proceduralSkyShader = Shader.Find("Skybox/Procedural");
        if (proceduralSkyShader)
        {
            skyboxMaterial = new Material(proceduralSkyShader);
            skyboxMaterial.SetFloat("_SunSize", 0.015f);
            skyboxMaterial.SetFloat("_SunSizeConvergence", 8f);
            skyboxMaterial.SetFloat("_AtmosphereThickness", 0.35f);
            skyboxMaterial.SetFloat("_Exposure", 0.85f);
            skyboxMaterial.SetColor("_SkyTint", startZone.SkyTint);
            skyboxMaterial.SetColor("_GroundColor", startZone.SkyTint * 0.35f);
            skyboxTintColor = startZone.SkyTint;
            RenderSettings.skybox = skyboxMaterial;
        }
    }

    private Material CreateUnlitMaterial(Color color)
    {
        var material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        return material;
    }

    // Builds a small, tileable, repeat-wrapped grid-line texture at runtime (same SetPixels/Apply
    // approach as SoftParticleMaterial) and applies it via Sprites/Default -- unlike Unlit/Color,
    // this shader actually supports a _MainTex, which is what lets the track surface show a
    // pattern at all instead of one flat color. mainTextureScale tiles it across the real track
    // dimensions so grid cells read as a consistent physical size regardless of track width/length.
    private Material CreateGridMaterial(Color baseColor, Color lineColor)
    {
        const int size = 64;
        const int lineThickness = 3;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            name = "TrackGridTexture"
        };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            bool onHorizontalLine = y < lineThickness;
            for (int x = 0; x < size; x++)
            {
                bool onVerticalLine = x < lineThickness;
                pixels[y * size + x] = (onHorizontalLine || onVerticalLine) ? lineColor : baseColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        var material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = texture;
        material.color = Color.white;
        material.mainTextureScale = new Vector2(Mathf.Max(1f, trackHalfWidth * 2f / 2.2f), Mathf.Max(1f, trackLength / 2.2f));
        return material;
    }

    // A thin repeating "pulse" texture (bright dash, dark gap) for the lane-guidance lines --
    // combined with the scrolling offset in Update(), it reads as energy segments continuously
    // traveling down the corridor rather than one static glowing strip.
    private Material CreateFlowMaterial(Color baseColor, Color pulseColor)
    {
        const int size = 32;
        const int pulseBand = 7;
        var texture = new Texture2D(1, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            name = "LaneFlowTexture"
        };

        var pixels = new Color[size];
        for (int y = 0; y < size; y++)
        {
            pixels[y] = y < pulseBand ? pulseColor : baseColor;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        var material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = texture;
        material.color = Color.white;
        material.mainTextureScale = new Vector2(1f, Mathf.Max(1f, trackLength / 6f));
        return material;
    }

    // Lit (Standard shader) material -- picks up SceneMoodController's directional light so the
    // primitive rock/debris shapes actually shade across their faces instead of rendering as flat
    // single-tone silhouettes. Kept separate from the track-shell's Unlit materials, which need to
    // stay flat and equally bright at any light angle for gameplay legibility.
    private Material CreateLitMaterial(Color color, float metallic, float smoothness)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        return material;
    }

    // Self-lit material for veins/crystals/beacons: stays visibly glowing regardless of the
    // directional light's angle or color, so the "energy crack in the rock" reads clearly even on
    // a shape's shadowed side, and blooms softly under the existing post-processing bloom.
    private Material CreateEmissiveMaterial(Color color)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color * 0.6f;
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetColor("_EmissionColor", color * VeinEmissionIntensity);
        return material;
    }

    private void BuildTrackShell()
    {
        CreateStaticCube("TrackSurface", visualRoot, new Vector3(0f, 0f, TrackCenterZ), new Vector3(trackHalfWidth * 2f, 0.08f, trackLength), trackSurfaceMaterial);
        CreateStaticCube("TrackCenterSpine", visualRoot, new Vector3(0f, 0.045f, TrackCenterZ), new Vector3(0.2f, 0.05f, trackLength), structuralMaterial);

        CreateStaticCube("LaneGuidanceLeft", visualRoot, new Vector3(-2.5f, 0.055f, TrackCenterZ), new Vector3(0.12f, 0.03f, trackLength), laneFlowMaterial);
        CreateStaticCube("LaneGuidanceCenter", visualRoot, new Vector3(0f, 0.055f, TrackCenterZ), new Vector3(0.12f, 0.03f, trackLength), laneFlowMaterial);
        CreateStaticCube("LaneGuidanceRight", visualRoot, new Vector3(2.5f, 0.055f, TrackCenterZ), new Vector3(0.12f, 0.03f, trackLength), laneFlowMaterial);

        CreateStaticCube("LaneSeparatorLeft", visualRoot, new Vector3(-1.25f, 0.05f, TrackCenterZ), new Vector3(0.08f, 0.03f, trackLength), structuralMaterial);
        CreateStaticCube("LaneSeparatorRight", visualRoot, new Vector3(1.25f, 0.05f, TrackCenterZ), new Vector3(0.08f, 0.03f, trackLength), structuralMaterial);

        CreateStaticCube("SideRailLeft", visualRoot, new Vector3(-sideRailX, 0.35f, TrackCenterZ), new Vector3(0.26f, 0.7f, trackLength), structuralMaterial);
        CreateStaticCube("SideRailRight", visualRoot, new Vector3(sideRailX, 0.35f, TrackCenterZ), new Vector3(0.26f, 0.7f, trackLength), structuralMaterial);
        CreateStaticCube("SideRailAccentLeft", visualRoot, new Vector3(-sideRailX, 0.72f, TrackCenterZ), new Vector3(0.08f, 0.07f, trackLength), laneGlowMaterial);
        CreateStaticCube("SideRailAccentRight", visualRoot, new Vector3(sideRailX, 0.72f, TrackCenterZ), new Vector3(0.08f, 0.07f, trackLength), laneGlowMaterial);

        CreateStaticCube("EnergyAccentLeft", visualRoot, new Vector3(-sideRailX - 0.2f, 0.1f, TrackCenterZ), new Vector3(0.05f, 0.08f, trackLength), energyAccentMaterial);
        CreateStaticCube("EnergyAccentRight", visualRoot, new Vector3(sideRailX + 0.2f, 0.1f, TrackCenterZ), new Vector3(0.05f, 0.08f, trackLength), energyAccentMaterial);

        BuildDepthMarkers();
    }

    // The track now floats in open space: the scene's big lit Ground plane and the old grey
    // horizon walls hid the battle backdrop behind a washed-out floor. Only the Ground's renderer
    // is switched off -- its object/collider stay exactly as authored. Fog is pulled in so the far
    // end of the track dissolves into the dark instead of ending on a hard edge; it still starts
    // beyond the obstacle spawn distance so hazards stay fully readable. The backdrop shaders
    // ignore fog, so the stars/station/fighters stay crisp.
    private void ApplySpaceAtmosphere()
    {
        GameObject ground = GameObject.Find(GroundObjectName);
        if (ground && ground.transform.parent == null)
        {
            var groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer)
            {
                groundRenderer.enabled = false;
            }
        }

        if (RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance = 38f;
            RenderSettings.fogEndDistance = 72f;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera && usesSpaceSky)
        {
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, 1400f);
        }
    }

    // Fixed, fully static floor lines at the player's own back-limit / center / forward-limit
    // depth positions -- a direct visual reference so forward/back movement reads as "I crossed
    // this line" instead of being invisible against the scrolling environment. Zero Update cost;
    // reuses the existing lane-glow/energy-accent materials (also driven by the ambient pace-glow
    // escalation above, for free).
    private void BuildDepthMarkers()
    {
        float backZ = TrackCenterZ - depthMarkerReachRange;
        float forwardZ = TrackCenterZ + depthMarkerReachRange;
        float markerWidth = trackHalfWidth * 1.7f;

        CreateStaticCube("DepthMarkerBack", visualRoot, new Vector3(0f, 0.09f, backZ), new Vector3(markerWidth, 0.05f, 0.14f), energyAccentMaterial);
        CreateStaticCube("DepthMarkerCenter", visualRoot, new Vector3(0f, 0.09f, TrackCenterZ), new Vector3(markerWidth, 0.05f, 0.1f), laneGlowMaterial);
        CreateStaticCube("DepthMarkerForward", visualRoot, new Vector3(0f, 0.09f, forwardZ), new Vector3(markerWidth, 0.05f, 0.14f), energyAccentMaterial);
    }

    private static GameObject CreateStaticCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;

        var collider = cube.GetComponent<Collider>();
        if (collider)
        {
            DestroyImmediate(collider);
        }

        var renderer = cube.GetComponent<Renderer>();
        if (renderer && material)
        {
            renderer.sharedMaterial = material;
        }

        return cube;
    }

    // Rotated variant -- used for tilted solar panels/rings and jaggedly-angled rock shards where
    // a flat axis-aligned cube can't read as a sloped or tumbling surface.
    private static GameObject CreateStaticCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
    {
        GameObject cube = CreateStaticCube(name, parent, localPosition, localScale, material);
        cube.transform.localRotation = localRotation;
        return cube;
    }

    // Sphere variant -- used only for the distant ringed-planetoid silhouette, so it reads as a
    // round world rather than another rock block.
    private static GameObject CreateStaticSphere(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = localPosition;
        sphere.transform.localScale = localScale;

        var collider = sphere.GetComponent<Collider>();
        if (collider)
        {
            DestroyImmediate(collider);
        }

        var renderer = sphere.GetComponent<Renderer>();
        if (renderer && material)
        {
            renderer.sharedMaterial = material;
        }

        return sphere;
    }

    // Varied flanking asteroid field: replaces the old closed tunnel-ring frames. Nothing here
    // spans the track (no left-to-right beam), so the forward path always reads as open. Shape,
    // size, material, rotation and depth all vary by index so the sides read as a real field of
    // tumbling rock and drifting debris rather than a repeating, mechanical pattern -- exactly
    // the "ship is genuinely traveling" cue the space theme is built around.
    private void BuildAsteroidField()
    {
        int perSide = Mathf.Max(4, asteroidsPerSide);
        sidePanels = new MovingElement[perSide * 2];
        int index = 0;

        for (int row = 0; row < perSide; row++)
        {
            float t = perSide > 1 ? row / (float)(perSide - 1) : 0f;
            index = BuildAsteroidCluster(index, row, -1f, t);
            index = BuildAsteroidCluster(index, row, 1f, t);
        }
    }

    private int BuildAsteroidCluster(int index, int row, float side, float t)
    {
        // Deterministic pseudo-variety (no gameplay RNG involved): shape/material/offset/tilt all
        // derive from the row index so the pattern is stable but never uniform. Six archetypes --
        // rock shard, boulder, layered spire, antenna-studded rock, drifting satellite debris, and
        // a distant ringed planetoid -- so a single screenful already reads as a mixed field rather
        // than one repeating silhouette; the shared rock/vein/debris materials are then separately
        // drifted by score/zone in Update(), so the same shapes also change palette across a run.
        int shape = row % 6;
        float jitter = (row * 0.61803f) % 1f; // golden-ratio spacing avoids visible repeats
        // Spread evenly along the travel range (left and right sides offset by half a slot so
        // the two flanks never line up), at varied heights around the deck.
        float slot = (sceneryFarZ - sceneryNearZ) / Mathf.Max(4, asteroidsPerSide);
        float x = side * (outerPillarX + 1.4f + jitter * 2.6f);
        float y = Mathf.Lerp(-3.5f, 4.5f, (jitter * 3.7f) % 1f);
        float z = sceneryNearZ + slot * (row + (side < 0f ? 0.25f : 0.75f));

        Transform asteroid = new GameObject($"Asteroid_{row}_{(side < 0f ? "L" : "R")}").transform;
        asteroid.SetParent(sidePanelRoot, false);
        asteroid.localPosition = new Vector3(x, y, z);
        // A fixed per-instance tilt (derived from jitter, not gameplay RNG) so rocks read as
        // tumbling debris caught mid-spin rather than neatly axis-aligned blocks.
        asteroid.localRotation = Quaternion.Euler(jitter * 47f, jitter * 121f, jitter * 83f);

        // Every shape shares the same dedicated, lit, zone-driven asteroid body material so the
        // whole field both shifts color together as the run travels through deep space and shades
        // consistently under SceneMoodController's directional light.
        Material bodyMat = asteroidBodyMaterial;
        Material veinMat = asteroidVeinMaterial;
        Material metalMat = debrisMaterial;

        // Slow constant tumble, a little faster for the smaller/closer shapes so it stays readable
        // as background motion rather than becoming a distracting blur.
        Vector3 rotationAxis = new Vector3(Mathf.Sin(jitter * 6.28f), Mathf.Cos(jitter * 4.2f), Mathf.Sin(jitter * 2.5f)).normalized;
        float rotationSpeed = Mathf.Lerp(14f, 5f, t);

        switch (shape)
        {
            case 0: // elongated rock shard with a glowing mineral crack and a chipped corner
            {
                float h = Mathf.Lerp(1.6f, 3.4f, t);
                CreateStaticCube("Body", asteroid, Vector3.zero, new Vector3(0.5f, h, 0.5f), bodyMat);
                CreateStaticCube("Vein", asteroid, new Vector3(0f, h * 0.3f, 0.26f), new Vector3(0.08f, h * 0.5f, 0.02f), veinMat);
                // Small angled chunk offset from the main shaft -- extra face normals catch the
                // directional light differently from the body, so the shard reads as a broken,
                // faceted piece of rock instead of a single flat-shaded box.
                CreateStaticCube(
                    "Chip", asteroid,
                    new Vector3(0.22f, -h * 0.32f, 0.1f),
                    new Vector3(0.32f, h * 0.22f, 0.34f),
                    bodyMat,
                    Quaternion.Euler(14f, 33f, -21f));
                break;
            }
            case 1: // squat boulder with a bright mineral seam across the top and a broken-off chunk
            {
                float h = Mathf.Lerp(1f, 2f, t);
                CreateStaticCube("Body", asteroid, Vector3.zero, new Vector3(1.1f, h, 0.7f), bodyMat);
                CreateStaticCube("Seam", asteroid, new Vector3(0f, h * 0.5f + 0.03f, 0f), new Vector3(1.15f, 0.06f, 0.75f), veinMat);
                // Same reasoning as the rock shard's "Chip": a second, differently-angled mass
                // breaks up the single-box silhouette so shading reveals distinct facets.
                CreateStaticCube(
                    "Chip", asteroid,
                    new Vector3(-0.55f, -h * 0.28f, 0.28f),
                    new Vector3(0.5f, h * 0.42f, 0.42f),
                    bodyMat,
                    Quaternion.Euler(-11f, 26f, 17f));
                break;
            }
            case 2: // layered rock spire with two glowing crystal bands
            {
                float h = Mathf.Lerp(2.2f, 4.4f, t);
                CreateStaticCube("Body", asteroid, Vector3.zero, new Vector3(0.8f, h, 0.6f), bodyMat);
                CreateStaticCube("VeinLow", asteroid, new Vector3(0f, -h * 0.18f, 0.31f), new Vector3(0.62f, 0.1f, 0.02f), veinMat);
                CreateStaticCube("VeinHigh", asteroid, new Vector3(0f, h * 0.28f, 0.31f), new Vector3(0.62f, 0.1f, 0.02f), veinMat);
                break;
            }
            case 3: // rock with a broken satellite antenna embedded in it, distress beacon still lit
            {
                float h = Mathf.Lerp(1.4f, 2.6f, t);
                CreateStaticCube("Body", asteroid, Vector3.zero, new Vector3(0.7f, h, 0.7f), bodyMat);
                CreateStaticCube("Antenna", asteroid, new Vector3(0f, h * 0.5f + 0.5f, 0f), new Vector3(0.08f, 1f, 0.08f), metalMat);
                CreateStaticCube("Beacon", asteroid, new Vector3(0f, h * 0.5f + 1.02f, 0f), new Vector3(0.16f, 0.16f, 0.16f), veinMat);
                break;
            }
            case 4: // drifting broken satellite -- metal hull, support strut, tilted solar panel
            {
                float h = Mathf.Lerp(0.9f, 1.5f, t);
                CreateStaticCube("Hull", asteroid, Vector3.zero, new Vector3(0.9f, h, 0.6f), metalMat);
                CreateStaticCube("Strut", asteroid, new Vector3(0.36f, h * 0.5f + 0.22f, 0.36f), new Vector3(0.06f, 0.45f, 0.06f), veinMat);
                CreateStaticCube(
                    "SolarPanel", asteroid,
                    new Vector3(0f, h * 0.5f + 0.42f, 0.34f),
                    new Vector3(1.05f, 0.06f, 0.5f),
                    metalMat,
                    Quaternion.Euler(18f, 0f, 0f));
                break;
            }
            default: // distant ringed planetoid
            {
                float radius = Mathf.Lerp(0.55f, 1f, t);
                CreateStaticSphere("Body", asteroid, Vector3.zero, new Vector3(radius, radius, radius) * 2f, bodyMat);
                // Dull metal debris ring (was the glowing vein material, which read as a bright
                // white square against the new dark space backdrop).
                CreateStaticCube(
                    "Ring", asteroid,
                    Vector3.zero,
                    new Vector3(radius * 2.6f, 0.04f, radius * 2.6f),
                    metalMat,
                    Quaternion.Euler(78f, 0f, 18f));
                break;
            }
        }

        sidePanels[index] = new MovingElement
        {
            Transform = asteroid,
            BaseScale = Vector3.one,
            // NEAR-ish band: same read as the previous flanking scenery, just varied in shape now.
            SpeedMultiplier = 1.55f,
            MinZ = sceneryNearZ,
            MaxZ = sceneryFarZ,
            RotationAxis = rotationAxis,
            RotationSpeed = rotationSpeed
        };

        return index + 1;
    }

    // NEAREST band: thin, bright ground-level streaks just inside the track -- the classic
    // "motion streak" speed cue, scrolling the fastest of all four bands. Bounded count
    // (streakRows x 2), reuses the shared laneGlowMaterial (no new draw-call-heavy assets).
    private void BuildSpeedStreaks()
    {
        speedStreaks = new MovingElement[streakRows * 2];
        int index = 0;
        float streakX = trackHalfWidth * 0.55f;

        for (int row = 0; row < streakRows; row++)
        {
            float t = streakRows > 1 ? row / (float)(streakRows - 1) : 0f;
            float z = Mathf.Lerp(streakNearZ, streakFarZ, (t + (row % 2) * 0.07f) % 1f);

            index = BuildSpeedStreak(index, row, -streakX, z);
            index = BuildSpeedStreak(index, row, streakX, z);
        }
    }

    private int BuildSpeedStreak(int index, int row, float x, float z)
    {
        GameObject streak = CreateStaticCube($"Streak_{row}_{(x < 0f ? "L" : "R")}", streakRoot, new Vector3(x, 0.08f, z), new Vector3(0.06f, 0.04f, 1.6f), laneGlowMaterial);

        speedStreaks[index] = new MovingElement
        {
            Transform = streak.transform,
            BaseScale = streak.transform.localScale,
            SpeedMultiplier = 1.85f,
            MinZ = streakNearZ,
            MaxZ = streakFarZ
        };

        return index + 1;
    }

    // Fine glowing dust filling the volume above the deck, streaming toward the camera in the
    // same direction as every other band (Update() drives local -Z). Purely an extra near-speed cue layered on top of the existing cube streaks --
    // no collider, never read by gameplay/fairness. Speed is refreshed every frame in Update()
    // from the same markerSpeed the cube bands use, so it always tracks current pace.
    private void BuildSpeedDust()
    {
        var dustObject = new GameObject("SpeedDust");
        dustObject.transform.SetParent(visualRoot, false);
        dustObject.transform.localPosition = new Vector3(0f, 2.5f, TrackCenterZ + 8f);

        speedDustSystem = dustObject.AddComponent<ParticleSystem>();

        var main = speedDustSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = dustLifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = new Color(0.8f, 0.95f, 1f, 0.85f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 250;

        var emission = speedDustSystem.emission;
        emission.rateOverTime = dustEmissionRate;

        var shape = speedDustSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(trackHalfWidth * 1.6f, 5f, 36f);
        shape.randomDirectionAmount = 0f;

        // Explicit local-space velocity instead of relying on the shape's own emit direction --
        // unambiguous, and Update() rewrites the Z term every frame to track current pace.
        var velocityOverLifetime = speedDustSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-12f);

        var colorOverLifetime = speedDustSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var particleRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = SoftParticleMaterial.Create();
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
    }

    // A large, fully static field of distant points scattered through the whole visible volume --
    // built once and never touched again (no per-frame cost). Gives the backdrop actual depth and
    // texture instead of just an empty tinted sky behind the flanking asteroids, directly answering
    // the "atmosphere looks plain" complaint alongside the new procedural skybox.
    private void BuildStarfield()
    {
        var starsObject = new GameObject("Starfield");
        starsObject.transform.SetParent(visualRoot, false);
        starsObject.transform.localPosition = new Vector3(0f, 8f, TrackCenterZ + trackLength * 0.25f);

        var stars = starsObject.AddComponent<ParticleSystem>();
        starfieldSystem = stars;
        var main = stars.main;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = 999f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.85f, 1f, 1f), Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        // Mobile fill-rate budget: a one-time burst (zero per-frame emission cost either way), but
        // fewer/smaller sprites still means less overdraw on low-end GPUs. 260 is still plenty dense
        // for a backdrop nobody looks at closely.
        main.maxParticles = 260;

        var emission = stars.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 260) });

        var shape = stars.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(outerPillarX * 5f, 46f, trackLength * 2.4f);
        shape.randomDirectionAmount = 0f;

        var particleRenderer = starsObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = SoftParticleMaterial.Create();
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        stars.Play();
    }

    // A handful of large, very soft, low-alpha colored puffs scattered far behind the track --
    // built once and fully static, like the starfield. Gives the backdrop actual atmospheric
    // depth/color instead of just points-of-light on a flat tinted sky, without needing any new
    // imported art (reuses the same SoftParticleMaterial already used for dust/stars).
    private void BuildNebulaClouds()
    {
        var nebulaObject = new GameObject("NebulaClouds");
        nebulaObject.transform.SetParent(visualRoot, false);
        nebulaObject.transform.localPosition = new Vector3(0f, 10f, TrackCenterZ + trackLength * 0.3f);

        var nebula = nebulaObject.AddComponent<ParticleSystem>();
        nebulaSystem = nebula;
        var main = nebula.main;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = 999f;
        main.startSpeed = 0f;
        // Large soft alpha-blended quads are the single most expensive thing to overdraw on a
        // mobile GPU (fill-rate, not particle count, is the real cost) -- kept fewer and slightly
        // smaller than the first pass so this stays a cheap background tint rather than a
        // full-screen overdraw hazard on low-end Android hardware.
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.25f, 0.55f, 0.14f), new Color(0.22f, 0.42f, 0.58f, 0.1f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 10;

        var emission = nebula.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = nebula.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(outerPillarX * 6f, 30f, trackLength * 2f);
        shape.randomDirectionAmount = 0f;

        var particleRenderer = nebulaObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = SoftParticleMaterial.Create();
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        nebula.Play();
    }
}
