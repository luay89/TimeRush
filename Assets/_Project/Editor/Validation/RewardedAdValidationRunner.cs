#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RewardedAdValidationRunner
{
    private const string SessionActiveKey = "TimeRush.Phase10.ValidationActive";
    private const string SessionScenarioKey = "TimeRush.Phase10.ValidationScenario";
    private const string SessionStepKey = "TimeRush.Phase10.ValidationStep";
    private const string SessionContinuedLoadsKey = "TimeRush.Phase10.ContinuedLoads";
    private const string SessionProcessIdKey = "TimeRush.Phase10.ValidationProcessId";
    private const string SessionBatchModeKey = "TimeRush.Phase10.ValidationBatchMode";
    private const string SessionRunAllKey = "TimeRush.Phase10.RunAll";
    private const string SessionScenarioIndexKey = "TimeRush.Phase10.ScenarioIndex";
    private const string SessionSingleScenarioKey = "TimeRush.Phase10.SingleScenario";
    private const string SessionAnyFailKey = "TimeRush.Phase10.AnyFail";
    private const string SessionAwaitingNextKey = "TimeRush.Phase10.AwaitingNext";

    private enum Scenario
    {
        RewardGranted,
        DoubleClick,
        ClosedWithoutReward,
        Failed,
        Unavailable
    }

    private enum Step
    {
        NotStarted,
        WaitForMenu,
        WaitForGame,
        WaitForResults,
        WaitAfterContinue,
        WaitForRestartedGame,
        WaitForResultsAfterRestart,
        WaitForMenuAfterFailure,
        Completed,
        Failed
    }

    private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
    private const float StepTimeoutSeconds = 12f;
    private const int PreservedScore = 37;
    private const int PreservedBest = 52;
    private const string ScenarioEnvVar = "TIMERUSH_PHASE10_SCENARIO";
    private const string ReportFileName = "phase10-runtime-validation.txt";

    // Full validation queue, executed in this exact order in a single batch invocation.
    private static readonly Scenario[] ScenarioQueue =
    {
        Scenario.RewardGranted,
        Scenario.ClosedWithoutReward,
        Scenario.Unavailable,
        Scenario.Failed,
        Scenario.DoubleClick
    };

    private static Scenario scenario;
    private static Step step;
    private static float stepStartedAt;
    private static int continuedGameLoads;
    private static bool sawRuntimeError;
    private static string failureReason;
    private static bool validationSessionActive;
    private static bool validationRequestedInBatchMode;

    static RewardedAdValidationRunner()
    {
        RestoreSessionState();

        if (validationSessionActive)
        {
            RegisterCallbacks();
            return;
        }

        ClearSessionState();
        UnregisterCallbacks();
    }

    [MenuItem("TimeRush/Validation/Run Rewarded Ad Validation")]
    public static void RunFromMenu()
    {
        Start(batchMode: false);
    }

    public static void RunPhase10MockValidationBatch()
    {
        Start(batchMode: true);
    }

    private static void Start(bool batchMode)
    {
        string scenarioName = Environment.GetEnvironmentVariable(ScenarioEnvVar);
        bool hasExplicit = Enum.TryParse(scenarioName, true, out Scenario explicitScenario);
        bool runAll = !hasExplicit;

        validationSessionActive = true;
        validationRequestedInBatchMode = batchMode;

        SessionState.SetBool(SessionActiveKey, true);
        SessionState.SetBool(SessionRunAllKey, runAll);
        SessionState.SetInt(SessionSingleScenarioKey, (int)(hasExplicit ? explicitScenario : Scenario.RewardGranted));
        SessionState.SetInt(SessionProcessIdKey, System.Diagnostics.Process.GetCurrentProcess().Id);
        SessionState.SetBool(SessionBatchModeKey, batchMode);
        SessionState.SetBool(SessionAnyFailKey, false);
        SessionState.SetBool(SessionAwaitingNextKey, false);
        SessionState.SetInt(SessionScenarioIndexKey, 0);

        InitReport();

        StartScenarioAtIndex(0);
    }

    private static void StartScenarioAtIndex(int index)
    {
        bool runAll = SessionState.GetBool(SessionRunAllKey, false);
        scenario = runAll
            ? ScenarioQueue[index]
            : (Scenario)SessionState.GetInt(SessionSingleScenarioKey, (int)Scenario.RewardGranted);

        step = Step.NotStarted;
        continuedGameLoads = 0;
        sawRuntimeError = false;
        failureReason = string.Empty;

        SessionState.SetInt(SessionScenarioKey, (int)scenario);
        SessionState.SetInt(SessionScenarioIndexKey, index);
        SessionState.SetInt(SessionStepKey, (int)step);
        SessionState.SetInt(SessionContinuedLoadsKey, continuedGameLoads);

        RegisterCallbacks();

        EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        Time.timeScale = 1f;

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
        }

        int total = runAll ? ScenarioQueue.Length : 1;
        Debug.Log("[PHASE10] Starting rewarded ad validation scenario: " + scenario + " (" + (index + 1) + "/" + total + ")");
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            step = Step.WaitForMenu;
            stepStartedAt = Time.realtimeSinceStartup;
            SessionState.SetInt(SessionStepKey, (int)step);
            return;
        }

        if (!validationSessionActive)
        {
            return;
        }

        if (change == PlayModeStateChange.EnteredEditMode)
        {
            if (step != Step.Completed && step != Step.Failed)
            {
                CancelValidationSession("Validation session ended before completion.");
            }
            return;
        }

        if (change == PlayModeStateChange.ExitingPlayMode && step != Step.Completed && step != Step.Failed)
        {
            ClearSessionState();
            UnregisterCallbacks();
            validationSessionActive = false;
            validationRequestedInBatchMode = false;
        }
    }

    private static void Update()
    {
        if (!validationSessionActive)
        {
            return;
        }

        // Between-scenario restart: a scenario finished, we dropped out of play mode,
        // and the next scenario in the queue is waiting to start.
        if (SessionState.GetBool(SessionAwaitingNextKey, false))
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(SessionAwaitingNextKey, false);
                StartScenarioAtIndex(SessionState.GetInt(SessionScenarioIndexKey, 0));
            }
            return;
        }

        if (scenario != (Scenario)SessionState.GetInt(SessionScenarioKey, (int)Scenario.RewardGranted))
        {
            RestoreSessionState();
        }

        if (EditorApplication.isPlaying && step == Step.NotStarted)
        {
            Advance(Step.WaitForMenu);
        }

        if (!EditorApplication.isPlaying || step == Step.NotStarted || step == Step.Completed || step == Step.Failed)
        {
            return;
        }

        if (Time.realtimeSinceStartup - stepStartedAt > StepTimeoutSeconds)
        {
            Fail("Timed out during step: " + step);
            return;
        }

        try
        {
            switch (step)
            {
                case Step.WaitForMenu:
                    if (SceneManager.GetActiveScene().name == SceneNames.MenuHub && GameStateMachine.HasInstance)
                    {
                        PlayerPrefs.SetInt("BEST_SCORE", PreservedBest);
                        PlayerPrefs.Save();
                        Assert(GameStateMachine.Instance.StartRunFromMenu(), "StartRunFromMenu returned false.");
                        Advance(Step.WaitForGame);
                    }
                    break;

                case Step.WaitForGame:
                    if (SceneManager.GetActiveScene().name == SceneNames.Game && GameController.Instance)
                    {
                        GameController.Instance.AddScore(PreservedScore);
                        GameController.Instance.TriggerGameOver();
                        Advance(Step.WaitForResults);
                    }
                    break;

                case Step.WaitForResults:
                    if (SceneManager.GetActiveScene().name == SceneNames.Results)
                    {
                        ConfigureAndTriggerContinue();
                    }
                    break;

                case Step.WaitAfterContinue:
                    ValidatePostContinueStep();
                    break;

                case Step.WaitForRestartedGame:
                    if (SceneManager.GetActiveScene().name == SceneNames.Game && GameController.Instance)
                    {
                        Assert(!GameController.Instance.HasContinuedThisRun, "Restart path incorrectly marked run as continued.");
                        Assert(GameController.Instance.CurrentScore == 0, "Restart path unexpectedly preserved score.");
                        GameController.Instance.TriggerGameOver();
                        Advance(Step.WaitForResultsAfterRestart);
                    }
                    break;

                case Step.WaitForResultsAfterRestart:
                    if (SceneManager.GetActiveScene().name == SceneNames.Results)
                    {
                        ResultsController controller = FindResultsController();
                        controller.GoToMenu();
                        Advance(Step.WaitForMenuAfterFailure);
                    }
                    break;

                case Step.WaitForMenuAfterFailure:
                    if (SceneManager.GetActiveScene().name == SceneNames.MenuHub)
                    {
                        Complete();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Fail(ex.ToString());
        }
    }

    private static void ConfigureAndTriggerContinue()
    {
        ResultsController controller = FindResultsController();
        MockRewardedAdService mock = UnityEngine.Object.FindObjectOfType<MockRewardedAdService>(true);

        switch (scenario)
        {
            case Scenario.RewardGranted:
                ConfigureMock(mock, "RewardGranted", 0.1f);
                InvokeContinue(controller, 1);
                Advance(Step.WaitAfterContinue);
                break;
            case Scenario.DoubleClick:
                ConfigureMock(mock, "RewardGranted", 0.2f);
                InvokeContinue(controller, 3);
                Advance(Step.WaitAfterContinue);
                break;
            case Scenario.ClosedWithoutReward:
                ConfigureMock(mock, "ClosedWithoutReward", 0.1f);
                InvokeContinue(controller, 1);
                Advance(Step.WaitAfterContinue);
                break;
            case Scenario.Failed:
                ConfigureMock(mock, "Failed", 0.1f);
                InvokeContinue(controller, 1);
                Advance(Step.WaitAfterContinue);
                break;
            case Scenario.Unavailable:
                if (mock)
                {
                    UnityEngine.Object.Destroy(mock);
                }
                InvokeContinue(controller, 1);
                Advance(Step.WaitAfterContinue);
                break;
        }
    }

    private static void ValidatePostContinueStep()
    {
        switch (scenario)
        {
            case Scenario.RewardGranted:
            case Scenario.DoubleClick:
                if (SceneManager.GetActiveScene().name != SceneNames.Game || !GameController.Instance)
                {
                    return;
                }

                Assert(continuedGameLoads == 1, "Expected exactly one continued Game scene load, got " + continuedGameLoads + ".");
                Assert(GameController.Instance.CurrentScore == PreservedScore, "Continue did not restore preserved score.");
                Assert(GameController.Instance.BestScore == PreservedBest, "Continue did not restore preserved best score.");
                Assert(GameController.Instance.HasContinuedThisRun, "Continue path did not mark the run as continued.");
                Assert(!ScoreSnapshot.HasValue, "Continue payload was not consumed/cleared.");
                GameController.Instance.TriggerGameOver();
                Advance(Step.WaitForResultsAfterRestart);
                break;

            case Scenario.ClosedWithoutReward:
            case Scenario.Failed:
                if (SceneManager.GetActiveScene().name != SceneNames.Results)
                {
                    return;
                }

                if (IsContinueRequestPending())
                {
                    return;
                }

                Assert(ScoreSnapshot.CanContinue, "Continue unexpectedly consumed on failed/closed ad flow.");
                FindResultsController().RestartGame();
                Advance(Step.WaitForRestartedGame);
                break;

            case Scenario.Unavailable:
                if (SceneManager.GetActiveScene().name != SceneNames.Results)
                {
                    return;
                }

                if (IsContinueRequestPending())
                {
                    return;
                }

                Assert(ScoreSnapshot.CanContinue, "Continue unexpectedly consumed when ad provider was unavailable.");
                FindResultsController().RestartGame();
                Advance(Step.WaitForRestartedGame);
                break;
        }
    }

    private static ResultsController FindResultsController()
    {
        ResultsController controller = UnityEngine.Object.FindObjectOfType<ResultsController>(true);
        Assert(controller != null, "ResultsController was not found in Results scene.");
        return controller;
    }

    private static void InvokeContinue(ResultsController controller, int attempts)
    {
        MethodInfo method = typeof(ResultsController).GetMethod("OnContinuePressed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(method != null, "ResultsController.OnContinuePressed was not found.");

        for (int i = 0; i < attempts; i++)
        {
            method.Invoke(controller, null);
        }
    }

    private static void ConfigureMock(MockRewardedAdService mock, string outcomeName, float delay)
    {
        Assert(mock != null, "MockRewardedAdService was not found on a persistent runtime object.");

        FieldInfo outcomeField = typeof(MockRewardedAdService).GetField("simulatedOutcome", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo delayField = typeof(MockRewardedAdService).GetField("simulatedDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(outcomeField != null, "MockRewardedAdService.simulatedOutcome field not found.");
        Assert(delayField != null, "MockRewardedAdService.simulatedDelay field not found.");

        outcomeField.SetValue(mock, Enum.Parse(outcomeField.FieldType, outcomeName));
        delayField.SetValue(mock, delay);
        mock.enabled = true;
    }

    private static bool IsContinueRequestPending()
    {
        ResultsController controller = FindResultsController();
        FieldInfo field = typeof(ResultsController).GetField("continueRequestInProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "ResultsController.continueRequestInProgress field not found.");
        return (bool)field.GetValue(controller);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (step == Step.WaitAfterContinue && scene.name == SceneNames.Game)
        {
            continuedGameLoads++;
            SessionState.SetInt(SessionContinuedLoadsKey, continuedGameLoads);
        }
    }

    private static void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
        {
            return;
        }

        if (condition.Contains("NullReferenceException") ||
            condition.Contains("MissingReferenceException") ||
            condition.Contains("Missing Script") ||
            condition.Contains("TypeLoadException") ||
            condition.Contains("cannot be entered") ||
            condition.Contains("feedback subscription", StringComparison.OrdinalIgnoreCase))
        {
            sawRuntimeError = true;
            if (string.IsNullOrEmpty(failureReason))
            {
                failureReason = condition + "\n" + stackTrace;
            }
        }
    }

    private static void Advance(Step nextStep)
    {
        step = nextStep;
        stepStartedAt = Time.realtimeSinceStartup;
        SessionState.SetInt(SessionStepKey, (int)step);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Complete()
    {
        if (sawRuntimeError)
        {
            Fail(failureReason);
            return;
        }

        step = Step.Completed;
        SessionState.SetInt(SessionStepKey, (int)step);
        FinishScenario(true, null);
    }

    private static void Fail(string reason)
    {
        if (step == Step.Failed)
        {
            return;
        }

        step = Step.Failed;
        failureReason = reason;
        SessionState.SetInt(SessionStepKey, (int)step);
        Debug.LogError("[PHASE10] Validation failed for scenario " + scenario + ": " + reason);
        FinishScenario(false, reason);
    }

    // Records the current scenario's verdict, then either advances to the next queued
    // scenario (dropping out of play mode) or finalizes the whole session.
    private static void FinishScenario(bool passed, string reason)
    {
        AppendScenarioResult(passed, reason);

        if (!passed)
        {
            SessionState.SetBool(SessionAnyFailKey, true);
        }

        bool runAll = SessionState.GetBool(SessionRunAllKey, false);
        int index = SessionState.GetInt(SessionScenarioIndexKey, 0);

        if (runAll && index + 1 < ScenarioQueue.Length)
        {
            SessionState.SetInt(SessionScenarioIndexKey, index + 1);
            SessionState.SetBool(SessionAwaitingNextKey, true);
            // Keep the session active; leaving play mode lets Update() start the next scenario
            // with a clean domain, preserving per-scenario isolation.
            EditorApplication.isPlaying = false;
            return;
        }

        bool overallPass = !SessionState.GetBool(SessionAnyFailKey, false);
        AppendFinal(overallPass);
        Shutdown(overallPass ? 0 : 1);
    }

    private static string ReportFullPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportFileName));
    }

    private static void InitReport()
    {
        string header =
            "PHASE10 RUNTIME VALIDATION" + Environment.NewLine +
            "==========================" + Environment.NewLine +
            "Started: " + DateTime.Now.ToString("u") + Environment.NewLine +
            Environment.NewLine;

        string path = ReportFullPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, header);
    }

    private static void AppendScenarioResult(bool passed, string reason)
    {
        string block =
            "Scenario: " + scenario + Environment.NewLine +
            "Result: " + (passed ? "PASS" : "FAIL") + Environment.NewLine;

        if (!passed && !string.IsNullOrEmpty(reason))
        {
            string flat = reason.Replace("\r", " ").Replace("\n", " | ");
            block += "Reason: " + flat + Environment.NewLine;
        }

        block += Environment.NewLine;
        AppendReport(block);
    }

    private static void AppendFinal(bool overallPass)
    {
        AppendReport("FINAL: " + (overallPass ? "PASS" : "FAIL") + Environment.NewLine);
    }

    private static void AppendReport(string text)
    {
        File.AppendAllText(ReportFullPath(), text);
    }

    private static void Shutdown(int exitCode)
    {
        ClearSessionState();
        UnregisterCallbacks();
        validationSessionActive = false;
        bool shouldExitEditor = validationRequestedInBatchMode && Application.isBatchMode;
        validationRequestedInBatchMode = false;
        EditorApplication.isPlaying = false;

        if (shouldExitEditor)
        {
            EditorApplication.Exit(exitCode);
        }
    }

    private static void RegisterCallbacks()
    {
        Application.logMessageReceived -= HandleLogMessage;
        Application.logMessageReceived += HandleLogMessage;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private static void UnregisterCallbacks()
    {
        Application.logMessageReceived -= HandleLogMessage;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        EditorApplication.update -= Update;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
    }

    private static void RestoreSessionState()
    {
        if (!SessionState.GetBool(SessionActiveKey, false))
        {
            validationSessionActive = false;
            validationRequestedInBatchMode = false;
            return;
        }

        int storedProcessId = SessionState.GetInt(SessionProcessIdKey, -1);
        if (storedProcessId != System.Diagnostics.Process.GetCurrentProcess().Id)
        {
            ClearSessionState();
            validationSessionActive = false;
            validationRequestedInBatchMode = false;
            return;
        }

        scenario = (Scenario)SessionState.GetInt(SessionScenarioKey, (int)Scenario.RewardGranted);
        step = (Step)SessionState.GetInt(SessionStepKey, (int)Step.NotStarted);
        continuedGameLoads = SessionState.GetInt(SessionContinuedLoadsKey, 0);
        validationRequestedInBatchMode = SessionState.GetBool(SessionBatchModeKey, false);
        validationSessionActive = true;
    }

    private static void CancelValidationSession(string reason)
    {
        ClearSessionState();
        UnregisterCallbacks();
        validationSessionActive = false;
        validationRequestedInBatchMode = false;
        Debug.LogWarning("[PHASE10] Validation session cancelled: " + reason);
    }

    private static void ClearSessionState()
    {
        SessionState.EraseBool(SessionActiveKey);
        SessionState.EraseInt(SessionScenarioKey);
        SessionState.EraseInt(SessionStepKey);
        SessionState.EraseInt(SessionContinuedLoadsKey);
        SessionState.EraseInt(SessionProcessIdKey);
        SessionState.EraseBool(SessionBatchModeKey);
        SessionState.EraseBool(SessionRunAllKey);
        SessionState.EraseInt(SessionScenarioIndexKey);
        SessionState.EraseInt(SessionSingleScenarioKey);
        SessionState.EraseBool(SessionAnyFailKey);
        SessionState.EraseBool(SessionAwaitingNextKey);
    }
}
#endif
