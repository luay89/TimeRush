using NUnit.Framework;

public sealed class GameStateModelTests
{
    [Test]
    public void InitialState_IsBoot()
    {
        var model = new GameStateModel(GameStateKind.Boot);

        Assert.That(model.Current, Is.EqualTo(GameStateKind.Boot));
    }

    [Test]
    public void ValidFlow_ReachesPlayingPauseResultsAndMenu()
    {
        var model = new GameStateModel(GameStateKind.Boot);

        Assert.That(model.TryTransition(GameStateKind.Loading), Is.True);
        Assert.That(model.TryTransition(GameStateKind.MenuHub), Is.True);
        Assert.That(model.TryTransition(GameStateKind.Loading), Is.True);
        Assert.That(model.TryTransition(GameStateKind.Playing), Is.True);
        Assert.That(model.TryTransition(GameStateKind.Paused), Is.True);
        Assert.That(model.TryTransition(GameStateKind.Playing), Is.True);
        Assert.That(model.TryTransition(GameStateKind.Results), Is.True);
        Assert.That(model.TryTransition(GameStateKind.MenuHub), Is.True);
        Assert.That(model.Current, Is.EqualTo(GameStateKind.MenuHub));
    }

    [Test]
    public void InvalidTransition_IsRejectedWithoutChangingCurrentState()
    {
        var model = new GameStateModel(GameStateKind.Playing);

        Assert.That(model.TryTransition(GameStateKind.MenuHub), Is.False);
        Assert.That(model.Current, Is.EqualTo(GameStateKind.Playing));
    }

    [Test]
    public void Results_CanStartAnotherRunThroughLoading()
    {
        var model = new GameStateModel(GameStateKind.Results);

        Assert.That(model.TryTransition(GameStateKind.Loading), Is.True);
        Assert.That(model.TryTransition(GameStateKind.Playing), Is.True);
        Assert.That(model.Current, Is.EqualTo(GameStateKind.Playing));
    }
}
