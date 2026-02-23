using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives scoring, pacing, and transitions for a game run.
/// </summary>
public class GameController : MonoBehaviour
{
    [Header("Scoring")]
    public float scorePerSecond = 1f;

    [Header("Difficulty")]
    public float difficultyEverySeconds = 8f;
    public float spawnIntervalMultiplier = 0.9f;
    public float minSpawnInterval = 0.5f;

    [SerializeField] private ObstacleSpawner obstacleSpawner;

    private float scoreAccumulator;
    private float difficultyTimer;

    private void Start()
    {
        GameState.ResetRun();

        if (!obstacleSpawner)
        {
            obstacleSpawner = FindObjectOfType<ObstacleSpawner>();
        }
    }

    private void Update()
    {
        TickScore();
        TickDifficulty();
    }

    /// <summary>
    /// Adds score smoothly over time.
    /// </summary>
    private void TickScore()
    {
        if (scorePerSecond <= 0f)
        {
            return;
        }

        scoreAccumulator += scorePerSecond * Time.deltaTime;
        int wholePoints = Mathf.FloorToInt(scoreAccumulator);

        if (wholePoints <= 0)
        {
            return;
        }

        GameState.AddScore(wholePoints);
        scoreAccumulator -= wholePoints;
    }

    /// <summary>
    /// Gradually reduces spawn interval to ramp difficulty.
    /// </summary>
    private void TickDifficulty()
    {
        if (!obstacleSpawner || difficultyEverySeconds <= 0f)
        {
            return;
        }

        difficultyTimer += Time.deltaTime;

        if (difficultyTimer < difficultyEverySeconds)
        {
            return;
        }

        difficultyTimer -= difficultyEverySeconds;

        float current = obstacleSpawner.SpawnInterval;
        float next = Mathf.Max(minSpawnInterval, current * spawnIntervalMultiplier);

        if (!Mathf.Approximately(current, next))
        {
            obstacleSpawner.SpawnInterval = next;
        }
    }

    /// <summary>
    /// Centralized game-over transition.
    /// </summary>
    public void GameOver()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Results");
    }
}