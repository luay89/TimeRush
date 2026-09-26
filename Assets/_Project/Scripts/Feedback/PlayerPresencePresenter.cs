using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a lightweight, pace-reactive presence layer on top of the player's existing Visual
/// cube: controlled emission brightness, a subtle non-flashing pulse, and a couple of static
/// trim fins for a more readable silhouette. Purely additive presentation -- never reads or
/// writes PlayerController's movement/collision/gameplay state, and never touches the shared
/// M_Player material asset (uses a MaterialPropertyBlock so the change is per-instance only).
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerPresencePresenter : MonoBehaviour
{
    private const string GameSceneName = SceneNames.Game;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Accessibility")]
    [SerializeField] private FeedbackConfig feedbackConfig;

    [Header("Emission")]
    [SerializeField, Range(1f, 3f)] private float maxEmissionBoost = 1.8f;
    [SerializeField, Range(1.2f, 3f)] private float referencePaceForMaxBoost = 1.9f;

    [Header("Pulse")]
    [SerializeField, Range(0.2f, 3f)] private float pulseHz = 1.1f;
    [SerializeField, Range(0f, 0.15f)] private float pulseAmplitude = 0.06f;

    private Transform visual;
    private Renderer visualRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color baseEmissionColor;

    // Same RuntimeInitializeOnLoadMethod + SceneManager.sceneLoaded pattern as
    // RushTrackEnvironment: the player GameObject is scene-local and is destroyed/recreated
    // fresh on every Game.unity (re)load (Restart/Continue), so a one-time install would only
    // ever attach to the first load in the process.
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

        if (FindObjectOfType<PlayerPresencePresenter>() != null)
        {
            return;
        }

        var player = FindObjectOfType<PlayerController>();
        if (!player)
        {
            return;
        }

        player.gameObject.AddComponent<PlayerPresencePresenter>();
    }

    private void Awake()
    {
        visual = transform.Find("Visual");
        visualRenderer = visual ? visual.GetComponent<Renderer>() : null;

        if (!visualRenderer || !visualRenderer.sharedMaterial)
        {
            enabled = false;
            return;
        }

        propertyBlock = new MaterialPropertyBlock();
        baseEmissionColor = visualRenderer.sharedMaterial.GetColor(EmissionColorId);

        BuildPresenceFins();
    }

    private void Update()
    {
        if (!visualRenderer)
        {
            return;
        }

        bool gameplayActive = GameStateMachine.IsGameplayInputAllowed;
        var gc = GameController.Instance;
        float rawPace = gc ? gc.GetPaceMultiplier() : 1f;
        bool cameraShakeEnabled = FeedbackPreferences.IsCameraShakeEnabled(feedbackConfig);
        float effectivePace = SpeedPerceptionMath.ResolveEffectivePace(rawPace, gameplayActive, cameraShakeEnabled);

        float brightness = PlayerPresenceMath.ComputeEmissionBrightness(effectivePace, referencePaceForMaxBoost, maxEmissionBoost);
        bool reduceFlashing = FeedbackPreferences.IsReduceFlashingEnabled(feedbackConfig);
        float pulse = PlayerPresenceMath.ComputePulseMultiplier(Time.time, pulseHz, pulseAmplitude, reduceFlashing);

        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, baseEmissionColor * (brightness * pulse));
        visualRenderer.SetPropertyBlock(propertyBlock);
    }

    // Static trim fins parented under Visual -- they ride along with Visual's own existing
    // lane/depth tilt+scale animation (PlayerController.UpdateVisualMotion) for free, with zero
    // additional per-frame cost of their own, giving the cube a less plain, more readable
    // silhouette without any new asset pipeline.
    private void BuildPresenceFins()
    {
        Material finMaterial = CreateUnlitMaterial(new Color(0.62f, 0.92f, 1f));

        CreateFin("PresenceFinLeft", new Vector3(-0.62f, 0.05f, -0.2f), new Vector3(0.06f, 0.22f, 0.5f), finMaterial);
        CreateFin("PresenceFinRight", new Vector3(0.62f, 0.05f, -0.2f), new Vector3(0.06f, 0.22f, 0.5f), finMaterial);
        CreateFin("PresenceSpine", new Vector3(0f, 0.54f, 0f), new Vector3(0.5f, 0.05f, 0.9f), finMaterial);
    }

    private void CreateFin(string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.name = name;
        fin.transform.SetParent(visual, false);
        fin.transform.localPosition = localPosition;
        fin.transform.localScale = localScale;

        var collider = fin.GetComponent<Collider>();
        if (collider)
        {
            Destroy(collider);
        }

        var renderer = fin.GetComponent<Renderer>();
        if (renderer)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static Material CreateUnlitMaterial(Color color)
    {
        var material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        return material;
    }
}
