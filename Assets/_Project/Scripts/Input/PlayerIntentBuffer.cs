/// <summary>
/// Holds one lane command briefly so a valid fast input is not lost during a lane transition.
/// </summary>
public sealed class PlayerIntentBuffer
{
    private float expirySeconds;
    private bool hasBufferedLaneStep;
    private int bufferedLaneStep;
    private float bufferedAt;

    public PlayerIntentBuffer(float expirySeconds)
    {
        this.expirySeconds = expirySeconds;
    }

    public bool HasBufferedLaneStep => hasBufferedLaneStep;

    public void SetExpiry(float seconds)
    {
        expirySeconds = seconds;
    }

    public bool TryStoreLaneStep(int laneStep, float now)
    {
        if (hasBufferedLaneStep || laneStep == 0 || expirySeconds <= 0f)
        {
            return false;
        }

        bufferedLaneStep = laneStep;
        bufferedAt = now;
        hasBufferedLaneStep = true;
        return true;
    }

    public bool TryConsumeLaneStep(float now, out int laneStep)
    {
        laneStep = 0;

        if (!hasBufferedLaneStep)
        {
            return false;
        }

        if (now - bufferedAt > expirySeconds)
        {
            Clear();
            return false;
        }

        laneStep = bufferedLaneStep;
        Clear();
        return true;
    }

    public void Clear()
    {
        hasBufferedLaneStep = false;
        bufferedLaneStep = 0;
        bufferedAt = 0f;
    }
}
