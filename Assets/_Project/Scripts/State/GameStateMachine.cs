using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns legal game-state transitions and scene requests; Gameplay systems remain scene-local.
/// </summary>
public sealed class GameStateMachine : MonoBehaviour
{
    public static GameStateMachine Instance { get; private set; }
    public static bool HasInstance => Instance != null;
    public static bool IsGameplayInputAllowed => Instance == null || Instance.CurrentState == GameStateKind.Playing;

    public GameStateKind CurrentState => EnsureStateModel().Current;
    public event Action<GameStateKind, GameStateKind> StateChanged;

    private GameStateModel stateModel;
    private bool sceneLoadInProgress;
    private bool ownsSingleton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameStateMachine: duplicate bootstrap instance ignored.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ownsSingleton = true;
        EnsureStateModel();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (!ownsSingleton)
        {
            return;
        }

        EnsureStateModel();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        if (!ownsSingleton)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (stateModel != null)
        {
            stateModel.Changed -= HandleStateChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    public bool StartBootFlow()
    {
        return TryBeginSceneLoad(GameStateKind.MenuHub, SceneNames.MenuHub);
    }

    public bool StartRunFromMenu()
    {
        return TryBeginSceneLoad(GameStateKind.Playing, SceneNames.Game);
    }

    public bool RestartFromResults()
    {
        return TryBeginSceneLoad(GameStateKind.Playing, SceneNames.Game);
    }

    public bool ContinueFromResults()
    {
        return TryBeginSceneLoad(GameStateKind.Playing, SceneNames.Game);
    }

    public bool ReturnToMenu()
    {
        if (sceneLoadInProgress || !EnsureStateModel().TryTransition(GameStateKind.MenuHub))
        {
            return false;
        }

        Time.timeScale = 1f;
        sceneLoadInProgress = true;
        SceneManager.LoadScene(SceneNames.MenuHub);
        return true;
    }

    public bool ShowResults()
    {
        if (sceneLoadInProgress || !EnsureStateModel().TryTransition(GameStateKind.Results))
        {
            return false;
        }

        Time.timeScale = 1f;
        sceneLoadInProgress = true;
        SceneManager.LoadScene(SceneNames.Results);
        return true;
    }

    public bool TogglePause()
    {
        return CurrentState == GameStateKind.Playing ? Pause() : CurrentState == GameStateKind.Paused && Resume();
    }

    public bool Pause()
    {
        if (!EnsureStateModel().TryTransition(GameStateKind.Paused))
        {
            return false;
        }

        Time.timeScale = 0f;
        return true;
    }

    public bool Resume()
    {
        if (!EnsureStateModel().TryTransition(GameStateKind.Playing))
        {
            return false;
        }

        Time.timeScale = 1f;
        return true;
    }

    private bool TryBeginSceneLoad(GameStateKind destination, string sceneName)
    {
        if (sceneLoadInProgress || !EnsureStateModel().TryTransition(GameStateKind.Loading))
        {
            return false;
        }

        sceneLoadInProgress = true;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadInProgress = false;
        GameStateKind expectedState = ResolveSceneState(scene.name);

        if (expectedState == CurrentState)
        {
            return;
        }

        if (!EnsureStateModel().TryTransition(expectedState))
        {
            Debug.LogError($"GameStateMachine: scene '{scene.name}' cannot be entered from {CurrentState}.", this);
        }
    }

    private GameStateModel EnsureStateModel()
    {
        if (stateModel != null)
        {
            return stateModel;
        }

        stateModel = new GameStateModel(GameStateKind.Boot);
        stateModel.Changed += HandleStateChanged;
        return stateModel;
    }

    private static GameStateKind ResolveSceneState(string sceneName)
    {
        if (sceneName == SceneNames.MenuHub)
        {
            return GameStateKind.MenuHub;
        }

        if (sceneName == SceneNames.Game)
        {
            return GameStateKind.Playing;
        }

        if (sceneName == SceneNames.Results)
        {
            return GameStateKind.Results;
        }

        return GameStateKind.Boot;
    }

    private void HandleStateChanged(GameStateKind previous, GameStateKind current)
    {
        StateChanged?.Invoke(previous, current);
    }
}
