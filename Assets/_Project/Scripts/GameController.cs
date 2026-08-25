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

    [System.Serializable]
    private class DifficultyProfile
    {
        public float startSpawnInterval = 1.65f;
        public float minSpawnInterval = 0.85f;
        public float intervalDecayPerSecond = 0.0065f;
        public float startFallSpeed = 4.3f;
        public float maxFallSpeed = 8.8f;
        public float speedGainPerSecond = 0.04f;
    }

    [Header("Scoring")]
    [SerializeField] private float scorePerSecond = 10f;
    [SerializeField] private float uiUpdateInterval = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDifficultyDebug;
    [SerializeField] private TextMeshProUGUI difficultyDebugText;
    [SerializeField, Range(0.25f, 1f)] private float difficultyDebugInterval = 0.5f;

    [Header("Difficulty")]
    [SerializeField] private DifficultyProfile difficultyProfile = new DifficultyProfile();

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

    public float GetSpawnInterval()
    {
        if (difficultyProfile == null)
        {
            return 1f;
        }

        float interval = difficultyProfile.startSpawnInterval - difficultyProfile.intervalDecayPerSecond * GetEffectiveAliveTime();
        return Mathf.Max(difficultyProfile.minSpawnInterval, interval);
    }

    public float GetObstacleSpeed()
    {
        if (difficultyProfile == null)
        {
            return 0f;
        }

        float speed = difficultyProfile.startFallSpeed + difficultyProfile.speedGainPerSecond * GetEffectiveAliveTime();
        return Mathf.Min(difficultyProfile.maxFallSpeed, speed);
    }

    public float GetPaceMultiplier()
    {
        if (difficultyProfile == null || difficultyProfile.startFallSpeed <= 0f)
        {
            return 1f;
        }

        return GetObstacleSpeed() / difficultyProfile.startFallSpeed;
    }

    public float GetDepthVariation()
    {
        float progress = Mathf.InverseLerp(0f, 90f, GetEffectiveAliveTime());
        return Mathf.Lerp(0.7f, 1f, progress);
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
        difficultyDebugText.SetText("Spawn: {0:0.00}s\nSpeed: {1:0.0}", GetSpawnInterval(), GetObstacleSpeed());
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
