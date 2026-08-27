using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives scoring, pacing, and transitions for a game run.
/// </summary>
public class GameController : MonoBehaviour
{
    private const string BestScoreKey = "BEST_SCORE";

    public static GameController Instance { get; private set; }

    [Header("Scoring")]
    [SerializeField] private float scorePerSecond = 10f;
    [SerializeField] private float uiUpdateInterval = 0.1f;

    [Header("Mastery Flow")]
    [SerializeField, Range(2, 5)] private int nearMissesPerFlowLevel = 3;
    [SerializeField, Range(2, 4)] private int maxFlowMultiplier = 4;
    [SerializeField, Range(3f, 10f)] private float flowRetentionSeconds = 6f;

    [Header("Debug")]
    [SerializeField] private bool showDifficultyDebug;
    [SerializeField] private TextMeshProUGUI difficultyDebugText;
    [SerializeField, Range(0.25f, 1f)] private float difficultyDebugInterval = 0.5f;

    [Header("Difficulty")]
    [SerializeField] private GameBalanceConfig gameBalanceConfig;

    [Header("Continue Settings")]
    [SerializeField, Tooltip("Seconds of invulnerability granted after a continue respawn.")]
    private float continueInvulnerabilitySeconds = 1f;
    [SerializeField, Tooltip("Seconds to ease the difficulty curve after continuing.")]
    private float continueDifficultyEaseSeconds = 3f;
    [SerializeField, Range(0.25f, 1f), Tooltip("Multiplier applied to alive time while the ease window is active.")]
    private float continueDifficultyEaseFactor = 0.6f;

    public int CurrentScore { get; private set; }
    public int BestScore { get; private set; }
    public event System.Action<string> FeedbackRaised;
    public float AliveTime => aliveTime;
    public bool IsGameOver => _gameOver;
    public bool HasContinuedThisRun => hasContinuedThisRun;
    public bool IsPlayerInvulnerable => invulnerabilityTimer > 0f;
    public bool IsInTrainingWindow => gameBalanceConfig != null && GetEffectiveAliveTime() < gameBalanceConfig.trainingDuration;
    public int NearMissChain { get; private set; }
    public int LastNearMissAward { get; private set; }
    public float FlowTimeRemaining => flowTimer;
    public int FlowMultiplier => CalculateFlowMultiplier(NearMissChain);

    // Static flag ensures we never queue multiple continue-driven scene reloads simultaneously.
    private static bool continueSceneLoadInProgress;

    private bool _gameOver;
    private bool resultsSceneVerified;
    // Prevents redundant transitions into the Results scene if multiple hazards report the same death.
    private bool resultsSceneLoadRequested;
    private bool hasContinuedThisRun;
    private bool runInitialized;

    private float scoreTimer;
    private float uiTimer;
    private float aliveTime;
    private float difficultyDebugTimer;
    private float invulnerabilityTimer;
    private float difficultyEaseTimer;
    private float flowTimer;
    private TextMeshProUGUI scoreText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"Duplicate GameController detected on {name}; destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!gameBalanceConfig)
        {
            Debug.LogError("GameController: GameBalanceConfig is required for TimeRush difficulty settings.", this);
            enabled = false;
            return;
        }

        uiUpdateInterval = Mathf.Max(0.01f, uiUpdateInterval);
        BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        InitializeRunState();
        EnsureResultsSceneInBuildSettings();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        TickRuntimeTimers();

        if (_gameOver)
        {
            return;
        }

        aliveTime += Time.deltaTime;
        TickScoreTimers();
        TickDifficultyDebug();
    }

    private void InitializeRunState()
    {
        if (runInitialized)
        {
            return;
        }

        continueSceneLoadInProgress = false;
        resultsSceneLoadRequested = false;

        // If a continue was requested from the Results screen, resume with the preserved payload.
        if (ScoreSnapshot.TryConsumeContinueRequest(out var payload))
        {
            ResumeFromContinue(payload);
        }
        else
        {
            // Fresh run (from boot or restart) starts with a clean score state.
            ResetScoreState();
        }

        runInitialized = true;
    }

    private void ResumeFromContinue(ScoreSnapshot.ContinuePayload payload)
    {
        // Restores the preserved score/best data and reapplies the one-time safety buffs.
        hasContinuedThisRun = true;
        _gameOver = false;
        CurrentScore = Mathf.Max(0, payload.score);
        BestScore = Mathf.Max(payload.best, PlayerPrefs.GetInt(BestScoreKey, 0));
        scoreTimer = 0f;
        uiTimer = 0f;
        aliveTime = 0f;
        invulnerabilityTimer = Mathf.Max(0f, continueInvulnerabilitySeconds);
        difficultyEaseTimer = Mathf.Max(0f, continueDifficultyEaseSeconds);
        ResetFlow();
        ScoreSnapshot.Clear();
        UpdateScoreText();
    }

    private void TickRuntimeTimers()
    {
        if (invulnerabilityTimer > 0f)
        {
            invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);
        }

        if (difficultyEaseTimer > 0f)
        {
            difficultyEaseTimer = Mathf.Max(0f, difficultyEaseTimer - Time.deltaTime);
        }

        if (flowTimer > 0f)
        {
            flowTimer = Mathf.Max(0f, flowTimer - Time.deltaTime);

            if (flowTimer <= 0f)
            {
                ResetFlow();
            }
        }
    }

    /// <summary>
    /// Accumulates score once per second and throttles HUD refreshes.
    /// </summary>
    private void TickScoreTimers()
    {
        if (_gameOver || scorePerSecond <= 0f)
        {
            return;
        }

        scoreTimer += Time.deltaTime;

        while (scoreTimer >= 1f)
        {
            AddScore(Mathf.FloorToInt(scorePerSecond));
            scoreTimer -= 1f;
        }

        uiTimer += Time.deltaTime;

        if (uiTimer >= uiUpdateInterval)
        {
            UpdateScoreText();
            uiTimer = 0f;
        }
    }

    /// <summary>
    /// Sole entry point for ending the run and transitioning to the Results scene.
    /// </summary>
    public void TriggerGameOver()
    {
        TriggerGameOverInternal(null);
    }

    /// <summary>
    /// Overload allowing callers to specify the triggering source for logging purposes.
    /// </summary>
    public void TriggerGameOver(Object source)
    {
        TriggerGameOverInternal(source);
    }

    public bool CanContinue()
    {
        return !_gameOver && !hasContinuedThisRun;
    }

    public static bool ContinueRun()
    {
        if (continueSceneLoadInProgress)
        {
            Debug.LogWarning("GameController: Continue run already in progress.");
            return false;
        }

        if (!ScoreSnapshot.CanContinue)
        {
            Debug.LogWarning("GameController: Continue requested but not available.");
            return false;
        }

        if (!ScoreSnapshot.TryQueueContinueRequest())
        {
            Debug.LogWarning("GameController: Continue already queued.");
            return false;
        }

        continueSceneLoadInProgress = true;
        // Re-enter the Game scene with normalized time scale so timers resume correctly.
        Time.timeScale = 1f;

        if (GameStateMachine.HasInstance)
        {
            if (GameStateMachine.Instance.ContinueFromResults())
            {
                return true;
            }

            ScoreSnapshot.CancelQueuedContinueRequest();
            continueSceneLoadInProgress = false;
            return false;
        }

        SceneManager.LoadScene(SceneNames.Game);
        return true;
    }

    private void TriggerGameOverInternal(Object source)
    {
        if (_gameOver)
        {
            Debug.LogWarning($"GameController: TriggerGameOver already processed; ignoring duplicate call from {DescribeSource(source)}.");
            return;
        }

        if (resultsSceneLoadRequested)
        {
            Debug.LogWarning($"GameController: Results scene load already requested; ignoring TriggerGameOver from {DescribeSource(source)}.");
            return;
        }

        if (!EnsureResultsSceneInBuildSettings())
        {
            return;
        }

        _gameOver = true;
        resultsSceneLoadRequested = true;

        if (CurrentScore > BestScore)
        {
            BestScore = CurrentScore;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
        }

        PlayerPrefs.Save();
        // Persist final state so the Results scene can decide whether continue is still allowed.
        ScoreSnapshot.Set(CurrentScore, BestScore, hasContinuedThisRun, true);

        Debug.Log($"GameOver triggered by {DescribeSource(source)}");

        Time.timeScale = 1f;

        if (GameStateMachine.HasInstance)
        {
            if (!GameStateMachine.Instance.ShowResults())
            {
                Debug.LogError("GameController: FSM rejected the Results transition.", this);
            }

            return;
        }

        SceneManager.LoadScene(SceneNames.Results);
    }

    private static string DescribeSource(Object source)
    {
        return source ? $"{source.GetType().Name} ({source.name})" : "UnknownSource";
    }

    public void RegisterScoreUI(TextMeshProUGUI text)
    {
        scoreText = text;
        UpdateScoreText();
    }

    public void AddScore(int amount, string reason = null)
    {
        if (_gameOver || amount <= 0)
        {
            return;
        }

        CurrentScore += amount;

        if (CurrentScore > BestScore)
        {
            BestScore = CurrentScore;
        }

        UpdateScoreText();

        if (!string.IsNullOrEmpty(reason))
        {
            FeedbackRaised?.Invoke(reason);
        }
    }

    /// <summary>
    /// Rewards precision only; ordinary survival score remains stable and readable.
    /// Consecutive near misses build a short-lived multiplier that resets on timeout.
    /// </summary>
    public void RegisterNearMiss(int baseBonus)
    {
        if (_gameOver || baseBonus <= 0)
        {
            return;
        }

        NearMissChain = Mathf.Min(NearMissChain + 1, MaxFlowChain);
        flowTimer = Mathf.Max(0.1f, flowRetentionSeconds);
        LastNearMissAward = baseBonus * FlowMultiplier;
        AddScore(LastNearMissAward, "NearMiss");
    }

    public float GetSpawnInterval()
    {
        if (!gameBalanceConfig)
        {
            return 1f;
        }

        return gameBalanceConfig.GetSpawnInterval(GetEffectiveAliveTime());
    }

    public float GetObstacleSpeed()
    {
        if (!gameBalanceConfig)
        {
            return 0f;
        }

        return gameBalanceConfig.GetFallSpeed(GetEffectiveAliveTime());
    }

    public float GetPaceMultiplier()
    {
        if (!gameBalanceConfig || gameBalanceConfig.startFallSpeed <= 0f)
        {
            return 1f;
        }

        return GetObstacleSpeed() / gameBalanceConfig.startFallSpeed;
    }

    public float GetDepthVariation()
    {
        return Mathf.Lerp(gameBalanceConfig ? gameBalanceConfig.startingDepthVariation : 0.65f, 1f, GetDifficultyProgress());
    }

    public float GetControlHintOpacity()
    {
        if (!gameBalanceConfig)
        {
            return 0f;
        }

        float fadeStart = Mathf.Max(0f, gameBalanceConfig.trainingDuration - 5f);
        float fadeEnd = gameBalanceConfig.trainingDuration + 3f;
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, fadeEnd, GetEffectiveAliveTime()));
        return 1f - fade;
    }

    private float GetDifficultyProgress()
    {
        if (!gameBalanceConfig)
        {
            return 0f;
        }

        return gameBalanceConfig.GetDifficultyProgress(GetEffectiveAliveTime());
    }

    private float GetEffectiveAliveTime()
    {
        if (difficultyEaseTimer <= 0f || continueDifficultyEaseSeconds <= 0f)
        {
            return aliveTime;
        }

        float normalized = Mathf.Clamp01(1f - (difficultyEaseTimer / continueDifficultyEaseSeconds));
        float multiplier = Mathf.Lerp(continueDifficultyEaseFactor, 1f, normalized);
        multiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        return aliveTime * multiplier;
    }

    private int MaxFlowChain => Mathf.Max(1, nearMissesPerFlowLevel) * Mathf.Max(1, maxFlowMultiplier - 1);

    private int CalculateFlowMultiplier(int chain)
    {
        if (chain <= 0)
        {
            return 1;
        }

        int step = Mathf.Max(1, nearMissesPerFlowLevel);
        int multiplier = 1 + (chain / step);
        return Mathf.Clamp(multiplier, 1, Mathf.Max(1, maxFlowMultiplier));
    }

    private void ResetFlow()
    {
        NearMissChain = 0;
        LastNearMissAward = 0;
        flowTimer = 0f;
    }

    private void ResetScoreState()
    {
        // Full run reset used when starting from boot or after the player selects Restart.
        CurrentScore = 0;
        scoreTimer = 0f;
        uiTimer = 0f;
        aliveTime = 0f;
        _gameOver = false;
        hasContinuedThisRun = false;
        invulnerabilityTimer = 0f;
        difficultyEaseTimer = 0f;
        ResetFlow();
        resultsSceneLoadRequested = false;
        ScoreSnapshot.Clear();
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (!scoreText)
        {
            return;
        }

        scoreText.text = $"Score: {CurrentScore}";
    }

    private void TickDifficultyDebug()
    {
        if (!showDifficultyDebug || !difficultyDebugText)
        {
            return;
        }

        difficultyDebugTimer += Time.deltaTime;

        if (difficultyDebugTimer < difficultyDebugInterval)
        {
            return;
        }

        difficultyDebugTimer = 0f;
        difficultyDebugText.SetText($"Spawn: {GetSpawnInterval():0.00}s\nSpeed: {GetObstacleSpeed():0.0}\n{(IsInTrainingWindow ? "LEARN" : "ARCADE")}");
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.Game)
        {
            return;
        }

        InitializeRunState();
    }

    private bool EnsureResultsSceneInBuildSettings()
    {
        if (resultsSceneVerified)
        {
            return true;
        }

        if (IsSceneInBuildSettings(SceneNames.Results))
        {
            resultsSceneVerified = true;
            return true;
        }

        Debug.LogError($"GameController: Scene '{SceneNames.Results}' is missing from Build Settings. GameOver cannot transition to results.");
        return false;
    }

    private static bool IsSceneInBuildSettings(string sceneName)
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);

            if (string.IsNullOrEmpty(scenePath))
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(scenePath);

            if (name == sceneName)
            {
                return true;
            }
        }

        return false;
    }
}
