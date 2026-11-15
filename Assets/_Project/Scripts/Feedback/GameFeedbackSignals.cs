using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent access point for the typed feedback hub. It owns no gameplay rule or presentation effect.
/// </summary>
public sealed class GameFeedbackSignals : MonoBehaviour
{
    public static GameFeedbackSignals Instance { get; private set; }
    public static bool HasInstance => Instance != null;
    public FeedbackEventHub Events { get; } = new FeedbackEventHub();

    private const string BootSceneName = "Boot";

    // Boot.unity is the only scene that owns the hub. Pressing Play directly in Game.unity (the
    // usual way to iterate in the Editor) therefore had no hub at all: no lane/near-miss/collision
    // event was ever raised, so every feedback sound stayed silent. Before the first scene wakes,
    // create a bare hub unless that scene is Boot (which brings its own), so the Game scene's own
    // presenters can subscribe in OnEnable as usual.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureHubWhenNotStartingFromBoot()
    {
        if (HasInstance || SceneManager.GetActiveScene().name == BootSceneName)
        {
            return;
        }

        new GameObject("GameFeedbackSignals").AddComponent<GameFeedbackSignals>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void RaisePlayerLaneChanged(PlayerLaneChangedFeedback payload) => Instance?.Events.RaisePlayerLaneChanged(payload);
    public static void RaisePlayerDepthChanged(PlayerDepthChangedFeedback payload) => Instance?.Events.RaisePlayerDepthChanged(payload);
    public static void RaiseNearMiss(NearMissFeedback payload) => Instance?.Events.RaiseNearMiss(payload);
    public static void RaiseObstacleCollision(ObstacleCollisionFeedback payload) => Instance?.Events.RaiseObstacleCollision(payload);
    public static void RaiseGameOver() => Instance?.Events.RaiseGameOver();
    public static void RaiseRunStarted() => Instance?.Events.RaiseRunStarted();
    public static void RaiseRunPaused() => Instance?.Events.RaiseRunPaused();
    public static void RaiseRunResumed() => Instance?.Events.RaiseRunResumed();
    public static void RaisePaceMilestone(PaceMilestoneFeedback payload) => Instance?.Events.RaisePaceMilestone(payload);
    public static void RaiseChallengeStateChanged(ChallengeStateChangedFeedback payload) => Instance?.Events.RaiseChallengeStateChanged(payload);
    public static void RaiseAmbientCameraJolt(float strength) => Instance?.Events.RaiseAmbientCameraJolt(strength);
}
