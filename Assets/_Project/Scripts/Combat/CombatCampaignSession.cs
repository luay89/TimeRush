using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Session rules for the optional combat campaign. Campaign missions reuse the existing
/// stage progression and results flow while keeping the classic lane runner available.</summary>
public static class CombatCampaignSession
{
    private const string AchievementPrefix = "TimeRush.Combat.Achievement.";
    public static bool IsActive { get; private set; }
    public static int MissionIndex { get; private set; }
    public static int Kills { get; private set; }
    public static bool ObjectiveComplete => Kills >= RequiredKills;
    public static int RequiredKills => 6 + Mathf.Clamp(MissionIndex, 0, 9) * 2;

    public static void BeginCampaign()
    {
        IsActive = true;
        Kills = 0;
        MissionIndex = ResolveNextMission();
        StageSession.SelectStage(MissionIndex);
    }

    public static void PrepareStage(int index)
    {
        IsActive = true;
        Kills = 0;
        MissionIndex = Mathf.Clamp(index, 0, StageCatalog.Count - 1);
    }

    public static void LeaveCampaign()
    {
        IsActive = false;
        Kills = 0;
    }

    public static void RegisterKill()
    {
        if (!IsActive) return;
        Kills++;
        if (Kills == 1) UnlockAchievement("first_kill");
        if (Kills >= RequiredKills) UnlockAchievement("ace_pilot");
    }

    public static bool HasAchievement(string id) => PlayerPrefs.GetInt(AchievementPrefix + id, 0) == 1;

    private static void UnlockAchievement(string id)
    {
        if (HasAchievement(id)) return;
        PlayerPrefs.SetInt(AchievementPrefix + id, 1);
        PlayerPrefs.Save();
    }

    private static int ResolveNextMission()
    {
        StageProgressModel progress = StageProgress.Load();
        for (int i = 0; i < StageCatalog.Count; i++)
        {
            if (progress.IsUnlocked(i) && progress.GetStars(i) == 0) return i;
        }
        return StageCatalog.Count - 1;
    }
}

/// <summary>Hooks the existing Game scene only while a campaign mission is active.</summary>
public sealed class CombatSceneInstaller : MonoBehaviour
{
    private static bool enteredCombatScene;
    private bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Subscribe()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.MenuHub && enteredCombatScene)
        {
            enteredCombatScene = false;
            CombatCampaignSession.LeaveCampaign();
        }
        if (scene.name != SceneNames.Game || !CombatCampaignSession.IsActive) return;
        enteredCombatScene = true;
        if (FindObjectOfType<CombatSceneInstaller>()) return;
        var root = new GameObject("CombatMissionRuntime");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<CombatSceneInstaller>();
    }

    private void Start()
    {
        if (installed) return;
        installed = true;

        // The project now has a unified space-combat runtime (see SpaceCombat/CombatModeInstaller).
        // Campaign missions simply flag the session state + stage selection; the combat installer
        // decides whether to boot combat based on GameBalanceConfig.enableSpaceCombat.
        var gc = GameController.Instance ? GameController.Instance : FindObjectOfType<GameController>();
        if (gc && gc.BalanceConfig && gc.BalanceConfig.enableSpaceCombat)
        {
            // New combat runtime handles player/enemy setup; nothing to do here.
            Destroy(gameObject);
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) player = FindObjectOfType<PlayerController>()?.gameObject;
        if (!player)
        {
            Debug.LogError("Combat mission could not find the player ship.");
            return;
        }

        Disable<PlayerController>(player);
        Disable<PlayerInputSource>(player);
        Disable<PlayerBlaster>(player);
        Disable<NearMissDetector>(player);
        Disable<PlayerShieldBubble>(player);

        var spawner = FindObjectOfType<ObstacleSpawner>();
        if (spawner) spawner.enabled = false;
        var powerups = FindObjectOfType<PowerUpSpawner>();
        if (powerups) powerups.enabled = false;
        var legacyGuns = FindObjectOfType<EnemyGunnery>();
        if (legacyGuns) legacyGuns.enabled = false;
        foreach (var obstacle in FindObjectsOfType<ObstacleLaneMarker>()) Destroy(obstacle.gameObject);
        var ground = GameObject.Find("Ground");
        if (ground) ground.SetActive(false);

        player.transform.position = new Vector3(0f, 0f, 0f);
        player.AddComponent<CombatPlayerHealth>();
        player.AddComponent<CombatFlightController>();

        Debug.LogWarning("CombatSceneInstaller: legacy combat runtime is disabled; enable GameBalanceConfig.enableSpaceCombat to use the new space-combat mode.");
    }

    private static void Disable<T>(GameObject target) where T : Behaviour
    {
        var component = target.GetComponent<T>();
        if (component) component.enabled = false;
    }
}

public sealed class CombatFlightController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float smoothing = 9f;
    [SerializeField] private Vector3 minimumBounds = new Vector3(-8f, -3.2f, -10f);
    [SerializeField] private Vector3 maximumBounds = new Vector3(8f, 7f, 20f);
    private Vector3 velocity;
    private Transform visual;
    private Vector2 touchAxis;
    private int controllingFinger = -1;

    private void Awake() => visual = transform.Find("Visual");

    private void Update()
    {
        if (!GameStateMachine.IsGameplayInputAllowed || RunCountdown.IsCounting) return;
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0f);
        input.x += touchAxis.x;
        input.y += touchAxis.y;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightShift)) input.z += 1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.RightControl)) input.z -= 1f;
        if (input.sqrMagnitude > 1f) input.Normalize();

        ReadTouch();
        input.x = Mathf.Clamp(input.x, -1f, 1f);
        input.y = Mathf.Clamp(input.y, -1f, 1f);
        Vector3 target = transform.position + input * moveSpeed * Time.deltaTime;
        target.x = Mathf.Clamp(target.x, minimumBounds.x, maximumBounds.x);
        target.y = Mathf.Clamp(target.y, minimumBounds.y, maximumBounds.y);
        target.z = Mathf.Clamp(target.z, minimumBounds.z, maximumBounds.z);
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, 1f / smoothing);

        if (visual)
        {
            Quaternion lean = Quaternion.Euler(-input.y * 12f, input.x * 10f, -input.x * 24f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, lean, Time.deltaTime * 8f);
        }
    }

    private void ReadTouch()
    {
        touchAxis = Vector2.zero;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && touch.position.x < Screen.width * 0.68f)
                controllingFinger = touch.fingerId;
            if (touch.fingerId != controllingFinger) continue;
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                controllingFinger = -1;
                continue;
            }
            touchAxis = new Vector2(touch.deltaPosition.x / Mathf.Max(1f, Screen.width * 0.08f), touch.deltaPosition.y / Mathf.Max(1f, Screen.height * 0.08f));
            touchAxis = Vector2.ClampMagnitude(touchAxis, 1f);
        }
    }
}

public sealed class CombatPlayerHealth : MonoBehaviour
{
    public const float MaximumHull = 100f;
    public const float MaximumShield = 100f;
    public float Hull { get; private set; } = MaximumHull;
    public float Shield { get; private set; } = MaximumShield;
    public float SpecialCooldown { get; private set; }
    public bool SpecialActive => specialTimer > 0f;
    private float specialTimer;
    private float shieldRegenDelay;
    private Transform bubble;
    private Material bubbleMaterial;

    private void Awake()
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "CombatShieldVisual";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * 1.8f;
        var collider = sphere.GetComponent<Collider>();
        if (collider) Destroy(collider);
        bubble = sphere.transform;
        bubbleMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.08f, 0.55f, 1f, 0.18f), renderQueue = 3000 };
        bubbleMaterial.SetFloat("_Mode", 3f);
        bubbleMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        bubbleMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        bubbleMaterial.SetInt("_ZWrite", 0);
        bubbleMaterial.EnableKeyword("_ALPHABLEND_ON");
        bubbleMaterial.DisableKeyword("_ALPHATEST_ON");
        bubbleMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        bubbleMaterial.EnableKeyword("_EMISSION");
        bubbleMaterial.SetColor("_EmissionColor", new Color(0.03f, 0.28f, 1f));
        sphere.GetComponent<Renderer>().sharedMaterial = bubbleMaterial;
    }

    private void OnDestroy() { if (bubbleMaterial) Destroy(bubbleMaterial); }

    private void Update()
    {
        if (SpecialCooldown > 0f) SpecialCooldown -= Time.deltaTime;
        if (specialTimer > 0f) specialTimer -= Time.deltaTime;
        if (shieldRegenDelay > 0f) shieldRegenDelay -= Time.deltaTime;
        else Shield = Mathf.Min(MaximumShield, Shield + 7f * Time.deltaTime);
        if (bubble) bubble.gameObject.SetActive(Shield > 0f || SpecialActive);
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.X)) ActivateSpecial();
    }

    public void Damage(float amount)
    {
        if (amount <= 0f || SpecialActive || GameController.Instance?.IsPlayerInvulnerable == true) return;
        shieldRegenDelay = 4f;
        float absorbed = Mathf.Min(Shield, amount);
        Shield -= absorbed;
        Hull = Mathf.Max(0f, Hull - (amount - absorbed));
        if (Hull <= 0f && GameController.Instance) GameController.Instance.TriggerGameOver();
    }

    public bool ActivateSpecial()
    {
        if (SpecialCooldown > 0f || !GameStateMachine.IsGameplayInputAllowed) return false;
        SpecialCooldown = 18f;
        specialTimer = 2.5f;
        Shield = Mathf.Min(MaximumShield, Shield + 25f);
        GameFeedbackSignals.RaiseAmbientCameraJolt(0.8f);
        return true;
    }
}

public enum CombatWeaponType { Laser, Plasma, HomingMissile, Railgun, SpreadShot, EnergyBeam }

public static class CombatWeaponCatalog
{
    public static readonly string[] Names = { "LASER", "PLASMA", "MISSILE", "RAILGUN", "SPREAD", "BEAM" };
    public static readonly Color[] Colors = { new Color(0.25f, 1f, 1f), new Color(1f, 0.45f, 0.12f), new Color(1f, 0.25f, 0.18f), new Color(0.75f, 0.9f, 1f), new Color(0.55f, 1f, 0.38f), new Color(0.85f, 0.35f, 1f) };
    public static float Damage(CombatWeaponType type, int level)
    {
        float baseDamage = type == CombatWeaponType.Plasma ? 28f : type == CombatWeaponType.HomingMissile ? 42f : type == CombatWeaponType.Railgun ? 52f : type == CombatWeaponType.EnergyBeam ? 5f : type == CombatWeaponType.SpreadShot ? 8f : 12f;
        return baseDamage * (1f + Mathf.Clamp(level, 0, 5) * 0.22f);
    }
    public static float Cooldown(CombatWeaponType type) => type == CombatWeaponType.Laser ? 0.14f : type == CombatWeaponType.Plasma ? 0.6f : type == CombatWeaponType.HomingMissile ? 1f : type == CombatWeaponType.Railgun ? 1.2f : type == CombatWeaponType.SpreadShot ? 0.48f : 0.08f;
}
