using NUnit.Framework;
using UnityEngine;

public sealed class PlayerIntentTests
{
    [Test]
    public void KeyboardLaneInputs_ProduceTheSameDirectionalIntent()
    {
        PlayerIntent aIntent = PlayerIntent.FromKeyboard(true, false, false, false);
        PlayerIntent arrowIntent = PlayerIntent.FromKeyboard(true, false, false, false);
        PlayerIntent dIntent = PlayerIntent.FromKeyboard(false, true, false, false);
        PlayerIntent rightArrowIntent = PlayerIntent.FromKeyboard(false, true, false, false);

        Assert.That(aIntent.LaneStep, Is.EqualTo(-1));
        Assert.That(arrowIntent.LaneStep, Is.EqualTo(aIntent.LaneStep));
        Assert.That(dIntent.LaneStep, Is.EqualTo(1));
        Assert.That(rightArrowIntent.LaneStep, Is.EqualTo(dIntent.LaneStep));
    }

    [Test]
    public void KeyboardDepthInputs_ProduceTheSameDirectionalIntent()
    {
        PlayerIntent wIntent = PlayerIntent.FromKeyboard(false, false, true, false);
        PlayerIntent upArrowIntent = PlayerIntent.FromKeyboard(false, false, true, false);
        PlayerIntent sIntent = PlayerIntent.FromKeyboard(false, false, false, true);
        PlayerIntent downArrowIntent = PlayerIntent.FromKeyboard(false, false, false, true);

        Assert.That(wIntent.DepthAxis, Is.EqualTo(1f));
        Assert.That(upArrowIntent.DepthAxis, Is.EqualTo(wIntent.DepthAxis));
        Assert.That(sIntent.DepthAxis, Is.EqualTo(-1f));
        Assert.That(downArrowIntent.DepthAxis, Is.EqualTo(sIntent.DepthAxis));
    }

    [Test]
    public void Swipe_ProducesTheSameLaneAndDepthIntentContract()
    {
        Assert.That(PlayerIntent.FromSwipe(new Vector2(-100f, 0f), 32f).LaneStep, Is.EqualTo(-1));
        Assert.That(PlayerIntent.FromSwipe(new Vector2(100f, 0f), 32f).LaneStep, Is.EqualTo(1));
        Assert.That(PlayerIntent.FromSwipe(new Vector2(0f, 100f), 32f).DepthStep, Is.EqualTo(1));
        Assert.That(PlayerIntent.FromSwipe(new Vector2(0f, -100f), 32f).DepthStep, Is.EqualTo(-1));
    }

    [Test]
    public void Buffer_HoldsOnlyOneLaneCommandUntilItExpires()
    {
        var buffer = new PlayerIntentBuffer(0.12f);

        Assert.That(buffer.TryStoreLaneStep(1, 2f), Is.True);
        Assert.That(buffer.TryStoreLaneStep(-1, 2.01f), Is.False);
        Assert.That(buffer.TryConsumeLaneStep(2.1f, out int laneStep), Is.True);
        Assert.That(laneStep, Is.EqualTo(1));
        Assert.That(buffer.HasBufferedLaneStep, Is.False);

        Assert.That(buffer.TryStoreLaneStep(-1, 3f), Is.True);
        Assert.That(buffer.TryConsumeLaneStep(3.13f, out _), Is.False);
    }

    [Test]
    public void Buffer_ClearRemovesThePendingCommand()
    {
        var buffer = new PlayerIntentBuffer(0.12f);
        buffer.TryStoreLaneStep(1, 1f);
        buffer.Clear();

        Assert.That(buffer.TryConsumeLaneStep(1.01f, out _), Is.False);
    }

    [Test]
    public void BufferClearPolicy_CoversPauseResultsAndRunTransitions()
    {
        Assert.That(PlayerInputSource.RequiresBufferClear(GameStateKind.Paused), Is.True);
        Assert.That(PlayerInputSource.RequiresBufferClear(GameStateKind.Results), Is.True);
        Assert.That(PlayerInputSource.RequiresBufferClear(GameStateKind.Loading), Is.True);
        Assert.That(PlayerInputSource.RequiresBufferClear(GameStateKind.Playing), Is.False);
    }

    [Test]
    public void LaneBounds_CannotBeExceeded()
    {
        Assert.That(PlayerIntent.ClampLaneIndex(0, -1, 3), Is.EqualTo(0));
        Assert.That(PlayerIntent.ClampLaneIndex(2, 1, 3), Is.EqualTo(2));
        Assert.That(PlayerIntent.ClampLaneIndex(1, -1, 3), Is.EqualTo(0));
        Assert.That(PlayerIntent.ClampLaneIndex(1, 1, 3), Is.EqualTo(2));
    }
}
