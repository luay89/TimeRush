public static class ScoreSnapshot
{
    public readonly struct ContinuePayload
    {
        public readonly int score;
        public readonly int best;

        public ContinuePayload(int score, int best)
        {
            this.score = score;
            this.best = best;
        }
    }

    public static int LastScore { get; private set; }
    public static int LastBest { get; private set; }
    public static bool LastRunHasContinued { get; private set; }
    public static bool LastRunCameFromGame { get; private set; }
    public static bool HasValue { get; private set; }
    public static bool ContinueRequested { get; private set; }

    public static bool CanContinue => HasValue && !LastRunHasContinued && LastRunCameFromGame && !ContinueRequested;

    public static void Set(int score, int best, bool hasContinuedThisRun, bool cameFromGameScene)
    {
        LastScore = score;
        LastBest = best;
        LastRunHasContinued = hasContinuedThisRun;
        LastRunCameFromGame = cameFromGameScene;
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
        payload = new ContinuePayload(LastScore, LastBest);
        return true;
    }

    public static void Clear()
    {
        HasValue = false;
        LastScore = 0;
        LastBest = 0;
        LastRunHasContinued = false;
        LastRunCameFromGame = false;
        ContinueRequested = false;
    }
}
