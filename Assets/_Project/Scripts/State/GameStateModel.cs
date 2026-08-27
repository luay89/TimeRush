using System;

/// <summary>
/// Pure state graph so transition rules can be tested without loading Unity scenes.
/// </summary>
public sealed class GameStateModel
{
    public GameStateKind Current { get; private set; }
    public event Action<GameStateKind, GameStateKind> Changed;

    public GameStateModel(GameStateKind initialState)
    {
        Current = initialState;
    }

    public bool TryTransition(GameStateKind next)
    {
        if (!GameStateTransitions.IsAllowed(Current, next))
        {
            return false;
        }

        GameStateKind previous = Current;
        Current = next;
        Changed?.Invoke(previous, next);
        return true;
    }
}

public static class GameStateTransitions
{
    public static bool IsAllowed(GameStateKind from, GameStateKind to)
    {
        switch (from)
        {
            case GameStateKind.Boot:
                return to == GameStateKind.Loading;
            case GameStateKind.Loading:
                return to == GameStateKind.MenuHub || to == GameStateKind.Playing;
            case GameStateKind.MenuHub:
                return to == GameStateKind.Loading;
            case GameStateKind.Playing:
                return to == GameStateKind.Paused || to == GameStateKind.Results;
            case GameStateKind.Paused:
                return to == GameStateKind.Playing;
            case GameStateKind.Results:
                return to == GameStateKind.Loading || to == GameStateKind.MenuHub;
            default:
                return false;
        }
    }
}
