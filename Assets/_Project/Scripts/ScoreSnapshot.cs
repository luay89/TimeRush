using UnityEngine;

public enum RunLossReason
{
    None = 0,
    ObstacleCollision = 1,
}

public static class ScoreSnapshot
{
    public readonly struct ContinuePayload
    {
        public readonly int score;
        public readonly int best;
        public readonly float aliveTime;

        public ContinuePayload(int score, int best, float aliveTime = 0f)
        {
            this.score = score;
            this.best = best;
            this.aliveTime = aliveTime;
        }
    }

    public static int LastScore { get; private set; }
    public static int LastBest { get; private set; }
    public static float LastAliveTime { get; private set; }
    public static bool LastRunHasContinued { get; private set; }
    public static bool LastRunCameFromGame { get; private set; }
    public static RunLossReason LastLossReason { get; private set; }
    public static bool LastRunSetNewBest { get; private set; }
    public static bool HasValue { get; private set; }
    public static bool ContinueRequested { get; private set; }

    public static bool CanContinue => HasValue && !LastRunHasContinued && LastRunCameFromGame && !ContinueRequested;

    public static void Set(
        int score,
        int best,
        bool hasContinuedThisRun,
        bool cameFromGameScene,
        RunLossReason lossReason = RunLossReason.None,
        bool setNewBest = false,
        float aliveTime = 0f)
    {
        LastScore = score;
        LastBest = best;
        LastAliveTime = Mathf.Max(0f, aliveTime);
        LastRunHasContinued = hasContinuedThisRun;
        LastRunCameFromGame = cameFromGameScene;
        LastLossReason = lossReason;
        LastRunSetNewBest = setNewBest;
        HasValue = true;
        ContinueRequested = false;
    }

    public static bool TryQueueContinueRequest()
    {
        if (!CanContinue)
        {
            return false;
        }

        ContinueRequested = true;
        return true;
    }

    public static void CancelQueuedContinueRequest()
    {
        ContinueRequested = false;
    }

    public static bool TryConsumeContinueRequest(out ContinuePayload payload)
    {
        if (!ContinueRequested || !HasValue)
        {
            payload = default;
            return false;
        }

        ContinueRequested = false;
        payload = new ContinuePayload(LastScore, LastBest, LastAliveTime);
        return true;
    }

    public static void Clear()
    {
        HasValue = false;
        LastScore = 0;
        LastBest = 0;
        LastAliveTime = 0f;
        LastRunHasContinued = false;
        LastRunCameFromGame = false;
        LastLossReason = RunLossReason.None;
        LastRunSetNewBest = false;
        ContinueRequested = false;
    }
}
