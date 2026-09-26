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

    [Header("Accessibility")]
    [SerializeField] private FeedbackConfig feedbackConfig;

    [Header("Motion Bands")]
    [SerializeField, Range(8, 40)] private int markerRows = 16;
    [SerializeField] private float markerTopY = 14f;
    [SerializeField] private float markerBottomY = -2.75f;
    [SerializeField, Range(6, 28)] private int frameRows = 12;
    [SerializeField] private float frameTopY = 18f;
    [SerializeField] private float frameBottomY = -3.25f;
    [SerializeField, Range(4, 24)] private int sidePanelRows = 10;
    [SerializeField] private float sidePanelTopY = 17f;
    [SerializeField] private float sidePanelBottomY = -3f;

    [Header("Approach Scaling")]
    [SerializeField, Range(0.5f, 1.4f)] private float farScaleMultiplier = 0.72f;
    [SerializeField, Range(1f, 2.2f)] private float nearScaleMultiplier = 1.32f;

    [Header("Near Speed Streaks")]
    [SerializeField, Range(4, 20)] private int streakRows = 8;
    [SerializeField] private float streakTopY = 11f;
    [SerializeField] private float streakBottomY = -2.2f;

    [Header("Ambient Pace Escalation")]
    [Tooltip("Brightness ceiling for the shared cyan/purple glow materials at max pace -- a slow continuous escalation, never an oscillation.")]
    [SerializeField, Range(1f, 2.5f)] private float maxAmbientGlowBoost = 1.9f;

    [Header("Roadside Zone Transition")]
    [Tooltip("How quickly the skyline's body/accent/roof colors drift toward the current distance zone (city/market/coastal/dawn). Purely cosmetic, driven only by GameController.CurrentScore.")]
    [SerializeField, Range(0.05f, 2f)] private float zoneTransitionSpeed = 0.35f;

    [Header("Depth Markers")]
    [Tooltip("Matches TrackLayoutConfig.SafeDepthRange -- the player's own late-game reachable depth range. Purely a fixed visual reference; never reads player/gameplay state.")]
    [SerializeField] private float depthMarkerReachRange = 2f;

    [Header("Track Presentation")]
    [SerializeField] private float trackHalfWidth = 5.4f;
    [SerializeField] private float trackLength = 64f;
    [SerializeField] private float sideRailX = 6.35f;
    [SerializeField] private float outerPillarX = 9.8f;

    [Header("Fallback Column Geometry")]
    [SerializeField] private float fallbackBoundaryX = 5f;
    [SerializeField] private float fallbackLaneLineX = 1.25f;
    [SerializeField] private float fallbackTrackZ = TrackCenterZ;

    private readonly List<MarkerColumn> columns = new List<MarkerColumn>(4);
    private MovingElement[] markers = System.Array.Empty<MovingElement>();
    private MovingElement[] frames = System.Array.Empty<MovingElement>();
    private MovingElement[] sidePanels = System.Array.Empty<MovingElement>();
    private MovingElement[] speedStreaks = System.Array.Empty<MovingElement>();
    private Transform visualRoot;
    private Transform markerRoot;
    private Transform frameRoot;
    private Transform sidePanelRoot;
    private Transform streakRoot;
    private Material trackSurfaceMaterial;
    private Material laneGlowMaterial;
    private Material structuralMaterial;
    private Material energyAccentMaterial;
    private Material horizonMaterial;
    private Material buildingBodyMaterial;
    private Material buildingAccentMaterial;
    private Material buildingRoofMaterial;
    private Color laneGlowBaseColor;
    private Color energyAccentBaseColor;
    private Color buildingBodyColor;
    private Color buildingAccentColor;
    private Color buildingRoofColor;
    private float ambientGlowBrightness = 1f;
    private bool built;

    // Distance/score-driven roadside palette so the passing skyline visibly changes character
    // over a long run instead of repeating the same city towers forever -- the same threshold
    // pattern SceneMoodController already uses for the light/fog mood, so the two reinforce
    // each other. Geometry (shape) variety is separate and handled per-building in
    // BuildSkylineBuilding; this only drives the shared body/accent/roof material colors.
    private readonly struct RoadsideZone
    {
        public readonly int ScoreThreshold;
        public readonly Color BodyColor;
        public readonly Color AccentColor;
        public readonly Color RoofColor;

        public RoadsideZone(int scoreThreshold, Color bodyColor, Color accentColor, Color roofColor)
        {
            ScoreThreshold = scoreThreshold;
            BodyColor = bodyColor;
            AccentColor = accentColor;
            RoofColor = roofColor;
        }
    }

    private static readonly RoadsideZone[] RoadsideZones =
    {
        // City -- the run's opening feel: cool structural blue-gray towers, cyan trim.
        new RoadsideZone(0, new Color(0.12f, 0.16f, 0.24f), new Color(0.2f, 0.9f, 1f), new Color(0.18f, 0.22f, 0.3f)),
        // Market -- warm adobe walls, amber trim, red-orange stall awnings.
        new RoadsideZone(400, new Color(0.5f, 0.3f, 0.2f), new Color(1f, 0.75f, 0.25f), new Color(0.82f, 0.26f, 0.2f)),
        // Coastal -- sun-bleached sandy huts, teal trim, deep blue thatch roofs.
        new RoadsideZone(900, new Color(0.72f, 0.62f, 0.44f), new Color(0.2f, 0.85f, 0.78f), new Color(0.12f, 0.34f, 0.44f)),
        // Dawn bazaar -- a rare, hard-earned late-run payoff: rose-violet stone, gold-pink trim, coral canopies.
        new RoadsideZone(1600, new Color(0.5f, 0.38f, 0.55f), new Color(1f, 0.55f, 0.78f), new Color(0.92f, 0.42f, 0.5f)),
    };

    private static RoadsideZone ResolveRoadsideZone(int score)
    {
        RoadsideZone result = RoadsideZones[0];
        for (int i = 0; i < RoadsideZones.Length; i++)
        {
            if (score >= RoadsideZones[i].ScoreThreshold)
            {
                result = RoadsideZones[i];
            }
        }

        return result;
    }

    private readonly struct MarkerColumn
    {
        public readonly float X;
        public readonly float Z;
        public readonly Vector3 Scale;
        public readonly Material Material;

        public MarkerColumn(float x, float z, Vector3 scale, Material material)
        {
            X = x;
            Z = z;
            Scale = scale;
            Material = material;
        }
    }

    private struct MovingElement
    {
        public Transform Transform;
        public Vector3 BaseScale;
        public float SpeedMultiplier;
        public float MinY;
        public float MaxY;
        public bool UsesDepthScale;
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

    private void OnValidate()
    {
        markerRows = Mathf.Clamp(markerRows, 8, 40);
        frameRows = Mathf.Clamp(frameRows, 6, 28);
        sidePanelRows = Mathf.Clamp(sidePanelRows, 4, 24);
        if (markerTopY <= markerBottomY + 1f)
        {
            markerTopY = markerBottomY + 1f;
        }
        if (frameTopY <= frameBottomY + 1f)
        {
            frameTopY = frameBottomY + 1f;
        }
        if (sidePanelTopY <= sidePanelBottomY + 1f)
        {
            sidePanelTopY = sidePanelBottomY + 1f;
        }
        nearScaleMultiplier = Mathf.Max(nearScaleMultiplier, farScaleMultiplier);
        streakRows = Mathf.Clamp(streakRows, 4, 20);
        if (streakTopY <= streakBottomY + 1f)
        {
            streakTopY = streakBottomY + 1f;
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

        // Slowly drift the skyline's shared body/accent/roof colors toward whatever roadside
        // zone the current score falls into -- city, market, coastal, dawn bazaar -- so a long
        // run visibly travels through changing terrain instead of looking identical throughout.
        RoadsideZone targetZone = ResolveRoadsideZone(gc ? gc.CurrentScore : 0);
        float zoneT = Time.deltaTime * zoneTransitionSpeed;
        buildingBodyColor = Color.Lerp(buildingBodyColor, targetZone.BodyColor, zoneT);
        buildingAccentColor = Color.Lerp(buildingAccentColor, targetZone.AccentColor, zoneT);
        buildingRoofColor = Color.Lerp(buildingRoofColor, targetZone.RoofColor, zoneT);
        buildingBodyMaterial.color = buildingBodyColor;
        buildingAccentMaterial.color = buildingAccentColor;
        buildingRoofMaterial.color = buildingRoofColor;

        float markerSpeed = RushTrackPerceptionMath.ResolveMarkerTravelSpeed(obstacleSpeed, rawPace, gameplayActive, cameraShakeEnabled);
        if (markerSpeed <= 0.01f)
        {
            return;
        }

        AdvanceElements(markers, markerSpeed, Time.deltaTime);
        AdvanceElements(frames, markerSpeed, Time.deltaTime);
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
            local.y = RushTrackPerceptionMath.WrapHeight(local.y - moveDistance, element.MinY, element.MaxY);
            element.Transform.localPosition = local;

            if (element.UsesDepthScale)
            {
                float heightT = Mathf.InverseLerp(element.MinY, element.MaxY, local.y);
                float scaleMultiplier = RushTrackPerceptionMath.ComputeApproachScale(heightT, nearScaleMultiplier, farScaleMultiplier);
                element.Transform.localScale = element.BaseScale * scaleMultiplier;
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

        markerRoot = new GameObject("SpeedMarkers").transform;
        markerRoot.SetParent(visualRoot, false);

        frameRoot = new GameObject("TunnelFrames").transform;
        frameRoot.SetParent(visualRoot, false);

        sidePanelRoot = new GameObject("SidePanels").transform;
        sidePanelRoot.SetParent(visualRoot, false);

        streakRoot = new GameObject("SpeedStreaks").transform;
        streakRoot.SetParent(visualRoot, false);

        BuildTrackShell();

        ResolveColumns();
        BuildMarkers();
        BuildSkyline();
        BuildSpeedStreaks();
        built = true;
    }

    private void BuildMaterials()
    {
        trackSurfaceMaterial = CreateUnlitMaterial(new Color(0.045f, 0.058f, 0.09f));
        laneGlowMaterial = CreateUnlitMaterial(new Color(0.2f, 0.9f, 1f));
        laneGlowBaseColor = laneGlowMaterial.color;
        structuralMaterial = CreateUnlitMaterial(new Color(0.12f, 0.16f, 0.24f));
        // Dim purple, echoing the existing boundary identity (M_Boundary) -- the previous bright
        // orange (1, 0.46, 0.16) sat too close to the obstacle color/emission and read as a second
        // hazard at a glance.
        energyAccentMaterial = CreateUnlitMaterial(new Color(0.32f, 0.2f, 0.52f));
        energyAccentBaseColor = energyAccentMaterial.color;
        // Close to the scene's own fog color so far structures fade into the horizon instead of popping.
        horizonMaterial = CreateUnlitMaterial(new Color(0.05f, 0.07f, 0.14f));

        // Dedicated skyline materials, deliberately separate from the track-shell/lane materials
        // above -- so the roadside zone drift (city/market/coastal/dawn) only ever recolors
        // passing buildings and never the track rails, lane guidance, or pace-driven glow.
        RoadsideZone startZone = RoadsideZones[0];
        buildingBodyMaterial = CreateUnlitMaterial(startZone.BodyColor);
        buildingAccentMaterial = CreateUnlitMaterial(startZone.AccentColor);
        buildingRoofMaterial = CreateUnlitMaterial(startZone.RoofColor);
        buildingBodyColor = buildingBodyMaterial.color;
        buildingAccentColor = buildingAccentMaterial.color;
        buildingRoofColor = buildingRoofMaterial.color;
    }

    private Material CreateUnlitMaterial(Color color)
    {
        var material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        return material;
    }

    private void BuildTrackShell()
    {
        CreateStaticCube("TrackSurface", visualRoot, new Vector3(0f, 0f, TrackCenterZ), new Vector3(trackHalfWidth * 2f, 0.08f, trackLength), trackSurfaceMaterial);
        CreateStaticCube("TrackCenterSpine", visualRoot, new Vector3(0f, 0.045f, TrackCenterZ), new Vector3(0.2f, 0.05f, trackLength), structuralMaterial);

        CreateStaticCube("LaneGuidanceLeft", visualRoot, new Vector3(-2.5f, 0.055f, TrackCenterZ), new Vector3(0.12f, 0.03f, trackLength), laneGlowMaterial);
        CreateStaticCube("LaneGuidanceCenter", visualRoot, new Vector3(0f, 0.055f, TrackCenterZ), new Vector3(0.12f, 0.03f, trackLength), laneGlowMaterial);
        CreateStaticCube("LaneGuidanceRight", visualRoot, new Vector3(2.5f, 0.055f, TrackCenterZ), new Vector3(0.12f, 0.03f, trackLength), laneGlowMaterial);

        CreateStaticCube("LaneSeparatorLeft", visualRoot, new Vector3(-1.25f, 0.05f, TrackCenterZ), new Vector3(0.08f, 0.03f, trackLength), structuralMaterial);
        CreateStaticCube("LaneSeparatorRight", visualRoot, new Vector3(1.25f, 0.05f, TrackCenterZ), new Vector3(0.08f, 0.03f, trackLength), structuralMaterial);

        CreateStaticCube("SideRailLeft", visualRoot, new Vector3(-sideRailX, 0.35f, TrackCenterZ), new Vector3(0.26f, 0.7f, trackLength), structuralMaterial);
        CreateStaticCube("SideRailRight", visualRoot, new Vector3(sideRailX, 0.35f, TrackCenterZ), new Vector3(0.26f, 0.7f, trackLength), structuralMaterial);
        CreateStaticCube("SideRailAccentLeft", visualRoot, new Vector3(-sideRailX, 0.72f, TrackCenterZ), new Vector3(0.08f, 0.07f, trackLength), laneGlowMaterial);
        CreateStaticCube("SideRailAccentRight", visualRoot, new Vector3(sideRailX, 0.72f, TrackCenterZ), new Vector3(0.08f, 0.07f, trackLength), laneGlowMaterial);

        CreateStaticCube("EnergyAccentLeft", visualRoot, new Vector3(-sideRailX - 0.2f, 0.1f, TrackCenterZ), new Vector3(0.05f, 0.08f, trackLength), energyAccentMaterial);
        CreateStaticCube("EnergyAccentRight", visualRoot, new Vector3(sideRailX + 0.2f, 0.1f, TrackCenterZ), new Vector3(0.05f, 0.08f, trackLength), energyAccentMaterial);

        CreateStaticCube("OuterSilhouetteLeft", visualRoot, new Vector3(-outerPillarX, 1.7f, TrackCenterZ + 8f), new Vector3(0.7f, 3.4f, trackLength * 0.7f), structuralMaterial);
        CreateStaticCube("OuterSilhouetteRight", visualRoot, new Vector3(outerPillarX, 1.7f, TrackCenterZ + 8f), new Vector3(0.7f, 3.4f, trackLength * 0.7f), structuralMaterial);

        BuildDepthMarkers();
        BuildHorizonSilhouettes();
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

    // Bounded, fully static distant layer (no Update cost) so the corridor reads as continuing
    // past what the camera can currently see instead of ending abruptly behind the near pillars.
    // Positioned further out in Z/X than OuterSilhouette and colored close to the scene fog color
    // so linear fog naturally fades them -- reuses the existing fog system, no new VFX/shader.
    private void BuildHorizonSilhouettes()
    {
        float farZ = TrackCenterZ + trackLength * 0.42f;
        float farX = outerPillarX + 3.2f;

        CreateStaticCube("HorizonSilhouetteLeft", visualRoot, new Vector3(-farX, 3.4f, farZ), new Vector3(1.1f, 7.2f, trackLength * 0.45f), horizonMaterial);
        CreateStaticCube("HorizonSilhouetteRight", visualRoot, new Vector3(farX, 3.4f, farZ), new Vector3(1.1f, 7.2f, trackLength * 0.45f), horizonMaterial);
        // Deliberately no beam spans the far horizon here: the forward view stays fully open
        // instead of reading as a closing gate/tunnel ring.
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
            Destroy(collider);
        }

        var renderer = cube.GetComponent<Renderer>();
        if (renderer && material)
        {
            renderer.sharedMaterial = material;
        }

        return cube;
    }

    // Rotated variant -- used only for pitched roofs/awnings (market stalls, coastal huts) where
    // a flat axis-aligned cube can't read as a sloped surface.
    private static GameObject CreateStaticCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
    {
        GameObject cube = CreateStaticCube(name, parent, localPosition, localScale, material);
        cube.transform.localRotation = localRotation;
        return cube;
    }

    private void ResolveColumns()
    {
        columns.Clear();

        TryAddColumn("LeftBoundary", true);
        TryAddColumn("LaneLineLeft", false);
        TryAddColumn("LaneLineRight", false);
        TryAddColumn("RightBoundary", true);

        if (columns.Count > 0)
        {
            return;
        }

        columns.Add(new MarkerColumn(-Mathf.Abs(fallbackBoundaryX), fallbackTrackZ, new Vector3(0.2f, 0.16f, 1.35f), null));
        columns.Add(new MarkerColumn(-Mathf.Abs(fallbackLaneLineX), fallbackTrackZ, new Vector3(0.12f, 0.13f, 1.1f), null));
        columns.Add(new MarkerColumn(Mathf.Abs(fallbackLaneLineX), fallbackTrackZ, new Vector3(0.12f, 0.13f, 1.1f), null));
        columns.Add(new MarkerColumn(Mathf.Abs(fallbackBoundaryX), fallbackTrackZ, new Vector3(0.2f, 0.16f, 1.35f), null));
    }

    private void TryAddColumn(string childName, bool boundary)
    {
        Transform child = transform.Find(childName);
        if (!child)
        {
            return;
        }

        var renderer = child.GetComponent<Renderer>();
        Material material = renderer ? renderer.sharedMaterial : null;
        Vector3 scale = boundary ? new Vector3(0.2f, 0.16f, 1.35f) : new Vector3(0.12f, 0.13f, 1.1f);
        Vector3 local = child.localPosition;

        columns.Add(new MarkerColumn(local.x, local.z, scale, material));
    }

    private void BuildMarkers()
    {
        int markerCount = markerRows * columns.Count;
        markers = new MovingElement[markerCount];

        int index = 0;
        for (int row = 0; row < markerRows; row++)
        {
            float rowT = markerRows > 1 ? row / (float)(markerRows - 1) : 0f;
            float y = Mathf.Lerp(markerBottomY, markerTopY, rowT);

            for (int col = 0; col < columns.Count; col++)
            {
                MarkerColumn column = columns[col];
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Marker_{row}_{col}";
                marker.transform.SetParent(markerRoot, false);
                marker.transform.localPosition = new Vector3(column.X, y, column.Z);
                marker.transform.localScale = column.Scale;

                var collider = marker.GetComponent<Collider>();
                if (collider)
                {
                    Destroy(collider);
                }

                var renderer = marker.GetComponent<Renderer>();
                if (renderer && column.Material)
                {
                    renderer.sharedMaterial = column.Material;
                }
                else if (renderer)
                {
                    renderer.sharedMaterial = laneGlowMaterial;
                }

                markers[index] = new MovingElement
                {
                    Transform = marker.transform,
                    BaseScale = column.Scale,
                    // FAR band: the boundary/lane-line wall markers span the whole track and read
                    // as the most distant layer -- slowest of the four bands.
                    SpeedMultiplier = 0.62f,
                    MinY = markerBottomY,
                    MaxY = markerTopY,
                    UsesDepthScale = true
                };

                index++;
            }
        }
    }

    // Varied roadside skyline: replaces the old closed tunnel-ring frames. Nothing here spans
    // the track (no left-to-right beam), so the forward path always reads as open. Shape, size,
    // material and depth all vary by index so the sides read as passing scenery instead of a
    // repeating, mechanical pattern.
    private void BuildSkyline()
    {
        int perSide = Mathf.Max(4, frameRows);
        sidePanels = new MovingElement[perSide * 2];
        int index = 0;

        for (int row = 0; row < perSide; row++)
        {
            float t = perSide > 1 ? row / (float)(perSide - 1) : 0f;
            float y = Mathf.Lerp(sidePanelBottomY, sidePanelTopY, t);

            index = BuildSkylineBuilding(index, row, -1f, y, t);
            index = BuildSkylineBuilding(index, row, 1f, y, t);
        }
    }

    private int BuildSkylineBuilding(int index, int row, float side, float y, float t)
    {
        // Deterministic pseudo-variety (no gameplay RNG involved): shape/material/offset all
        // derive from the row index so the pattern is stable but never uniform. Six shapes now
        // (was four) so a single screenful of scenery already reads as mixed -- towers next to
        // low blocks next to market stalls next to huts -- rather than one repeating silhouette;
        // the shared body/accent/roof materials are then separately drifted by score/zone in
        // Update(), so the same shapes also change palette as the run travels through terrain.
        int shape = row % 6;
        float jitter = (row * 0.61803f) % 1f; // golden-ratio spacing avoids visible repeats
        float depthSpan = Mathf.Lerp(3.5f, 13f, t);
        float x = side * (outerPillarX + 1.4f + jitter * 2.6f);
        float z = TrackCenterZ + Mathf.Lerp(4f, 11f, (t + jitter) % 1f) + depthSpan * 0.001f;

        Transform building = new GameObject($"Building_{row}_{(side < 0f ? "L" : "R")}").transform;
        building.SetParent(sidePanelRoot, false);
        building.localPosition = new Vector3(x, y, z);

        // Shape 2 keeps borrowing the fog-matched horizon material (it already tracks the scene's
        // own mood-driven fog color), every other shape uses the dedicated, zone-driven skyline
        // materials so the whole roadside shifts together as the run's terrain changes.
        Material bodyMat = shape == 2 ? horizonMaterial : buildingBodyMaterial;
        Material accentMat = buildingAccentMaterial;
        Material roofMat = buildingRoofMaterial;

        switch (shape)
        {
            case 0: // slim tower with a single glow stripe
            {
                float h = Mathf.Lerp(1.6f, 3.4f, t);
                CreateStaticCube("Body", building, Vector3.zero, new Vector3(0.5f, h, 0.5f), bodyMat);
                CreateStaticCube("Stripe", building, new Vector3(0f, h * 0.3f, 0.26f), new Vector3(0.08f, h * 0.5f, 0.02f), accentMat);
                break;
            }
            case 1: // wide low block
            {
                float h = Mathf.Lerp(1f, 2f, t);
                CreateStaticCube("Body", building, Vector3.zero, new Vector3(1.1f, h, 0.7f), bodyMat);
                CreateStaticCube("Roofline", building, new Vector3(0f, h * 0.5f + 0.03f, 0f), new Vector3(1.15f, 0.06f, 0.75f), accentMat);
                break;
            }
            case 2: // tall building with two window bands
            {
                float h = Mathf.Lerp(2.2f, 4.4f, t);
                CreateStaticCube("Body", building, Vector3.zero, new Vector3(0.8f, h, 0.6f), bodyMat);
                CreateStaticCube("BandLow", building, new Vector3(0f, -h * 0.18f, 0.31f), new Vector3(0.62f, 0.1f, 0.02f), accentMat);
                CreateStaticCube("BandHigh", building, new Vector3(0f, h * 0.28f, 0.31f), new Vector3(0.62f, 0.1f, 0.02f), accentMat);
                break;
            }
            case 3: // block with a rooftop spire/antenna
            {
                float h = Mathf.Lerp(1.4f, 2.6f, t);
                CreateStaticCube("Body", building, Vector3.zero, new Vector3(0.7f, h, 0.7f), bodyMat);
                CreateStaticCube("Spire", building, new Vector3(0f, h * 0.5f + 0.5f, 0f), new Vector3(0.08f, 1f, 0.08f), accentMat);
                CreateStaticCube("Beacon", building, new Vector3(0f, h * 0.5f + 1.02f, 0f), new Vector3(0.16f, 0.16f, 0.16f), accentMat);
                break;
            }
            case 4: // market stall -- low counter, tilted awning, support post
            {
                float h = Mathf.Lerp(0.9f, 1.5f, t);
                CreateStaticCube("Body", building, Vector3.zero, new Vector3(0.9f, h, 0.6f), bodyMat);
                CreateStaticCube("Post", building, new Vector3(0.36f, h * 0.5f + 0.22f, 0.36f), new Vector3(0.06f, 0.45f, 0.06f), accentMat);
                CreateStaticCube(
                    "Awning", building,
                    new Vector3(0f, h * 0.5f + 0.42f, 0.34f),
                    new Vector3(1.05f, 0.06f, 0.5f),
                    roofMat,
                    Quaternion.Euler(18f, 0f, 0f));
                break;
            }
            default: // coastal hut -- low walls under a pitched, two-sided roof
            {
                float h = Mathf.Lerp(0.8f, 1.3f, t);
                CreateStaticCube("Body", building, Vector3.zero, new Vector3(0.85f, h, 0.75f), bodyMat);
                CreateStaticCube(
                    "RoofLeft", building,
                    new Vector3(-0.22f, h * 0.5f + 0.18f, 0f),
                    new Vector3(0.6f, 0.08f, 0.85f),
                    roofMat,
                    Quaternion.Euler(0f, 0f, 26f));
                CreateStaticCube(
                    "RoofRight", building,
                    new Vector3(0.22f, h * 0.5f + 0.18f, 0f),
                    new Vector3(0.6f, 0.08f, 0.85f),
                    roofMat,
                    Quaternion.Euler(0f, 0f, -26f));
                CreateStaticCube("Doorway", building, new Vector3(0f, -h * 0.32f, 0.38f), new Vector3(0.26f, h * 0.36f, 0.03f), accentMat);
                break;
            }
        }

        sidePanels[index] = new MovingElement
        {
            Transform = building,
            BaseScale = Vector3.one,
            // NEAR-ish band: same read as the previous side panels, just varied in shape now.
            SpeedMultiplier = 1.55f,
            MinY = sidePanelBottomY,
            MaxY = sidePanelTopY,
            UsesDepthScale = false
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
            float y = Mathf.Lerp(streakBottomY, streakTopY, t);

            index = BuildSpeedStreak(index, row, -streakX, y);
            index = BuildSpeedStreak(index, row, streakX, y);
        }
    }

    private int BuildSpeedStreak(int index, int row, float x, float y)
    {
        GameObject streak = CreateStaticCube($"Streak_{row}_{(x < 0f ? "L" : "R")}", streakRoot, new Vector3(x, y, TrackCenterZ - 1.6f), new Vector3(0.06f, 0.06f, 1.6f), laneGlowMaterial);

        speedStreaks[index] = new MovingElement
        {
            Transform = streak.transform,
            BaseScale = streak.transform.localScale,
            SpeedMultiplier = 1.85f,
            MinY = streakBottomY,
            MaxY = streakTopY,
            UsesDepthScale = false
        };

        return index + 1;
    }
}
