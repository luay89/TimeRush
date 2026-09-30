#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dev-only visual check: boots the real game flow (Boot -> MenuHub -> Game) in Play Mode, waits
/// for a live run, then renders the gameplay camera to PNG files so the look of the Game scene
/// can be reviewed without a person pressing Play. Never part of a build (Editor folder).
/// Batch usage: Unity -batchmode -projectPath . -executeMethod GameSceneScreenshotRunner.RunBatch
/// Output folder comes from TIMERUSH_SHOT_DIR (defaults to Logs/Screenshots).
/// </summary>
[InitializeOnLoad]
public static class GameSceneScreenshotRunner
{
    private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
    private const string ActiveKey = "TimeRush.Shot.Active";
    private const string BatchKey = "TimeRush.Shot.Batch";
    private const string OutputDirEnvVar = "TIMERUSH_SHOT_DIR";
    private const string ScoreEnvVar = "TIMERUSH_SHOT_SCORE";
    private const float TimeoutSeconds = 90f;
    private const int Width = 1280;
    private const int Height = 720;

    // Seconds of live gameplay before each capture.
    private static readonly float[] CaptureTimes = { 1.5f, 3f, 4.2f };

    private static bool runRequested;
    private static float gameEnteredAt = -1f;
    private static float startedAt;
    private static int nextCapture;

    static GameSceneScreenshotRunner()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            startedAt = Time.realtimeSinceStartup;
        }
    }

    [MenuItem("TimeRush/Validation/Capture Game Scene Screenshots")]
    public static void RunFromMenu()
    {
        Start(false);
    }

    public static void RunBatch()
    {
        Start(true);
    }

    private static void Start(bool batch)
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(BatchKey, batch);
        EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        startedAt = Time.realtimeSinceStartup;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        if (Time.realtimeSinceStartup - startedAt > TimeoutSeconds)
        {
            Finish("Timed out before all screenshots were captured.", 1);
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == SceneNames.MenuHub && GameStateMachine.HasInstance && !runRequested)
        {
            runRequested = GameStateMachine.Instance.StartRunFromMenu();
            return;
        }

        if (gameEnteredAt >= 0f && sceneName == SceneNames.Results)
        {
            // The unattended run crashed into an obstacle; keep whatever was captured.
            Finish(nextCapture > 0 ? null : "Run ended before the first capture.", nextCapture > 0 ? 0 : 1);
            return;
        }

        if (sceneName != SceneNames.Game || !GameController.Instance)
        {
            return;
        }

        if (gameEnteredAt < 0f)
        {
            gameEnteredAt = Time.time;
            if (int.TryParse(Environment.GetEnvironmentVariable(ScoreEnvVar), out int score) && score > 0)
            {
                GameController.Instance.AddScore(score);
            }
        }

        if (nextCapture < CaptureTimes.Length && Time.time - gameEnteredAt >= CaptureTimes[nextCapture])
        {
            Capture(nextCapture);
            nextCapture++;
        }

        if (nextCapture >= CaptureTimes.Length)
        {
            Finish(null, 0);
        }
    }

    private static void Capture(int index)
    {
        Camera camera = Camera.main;
        if (!camera)
        {
            Debug.LogError("[Screenshot] No main camera in the Game scene.");
            return;
        }

        string directory = Environment.GetEnvironmentVariable(OutputDirEnvVar);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Screenshots");
        }

        Directory.CreateDirectory(directory);

        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;

        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        image.Apply();

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;

        string path = Path.Combine(directory, "game_" + index + ".png");
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        Debug.Log("[Screenshot] Saved " + path);
    }

    private static void Finish(string error, int exitCode)
    {
        bool batch = SessionState.GetBool(BatchKey, false);
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Update;
        runRequested = false;
        gameEnteredAt = -1f;
        nextCapture = 0;

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("[Screenshot] " + error);
        }

        if (batch)
        {
            EditorApplication.Exit(exitCode);
        }
        else
        {
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
