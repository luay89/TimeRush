public enum CombatGameMode
{
    Survival,
    Campaign,
    BossBattle,
    Stage
}

public static class CombatGameModeSession
{
    public static CombatGameMode Mode { get; private set; } = CombatGameMode.Survival;
    public static bool BossBattleCompleted { get; private set; }

    public static void BeginSurvival()
    {
        Mode = CombatGameMode.Survival;
        BossBattleCompleted = false;
        CombatCampaignSession.LeaveCampaign();
        StageSession.SelectEndless();
    }

    public static void BeginCampaign()
    {
        Mode = CombatGameMode.Campaign;
        BossBattleCompleted = false;
        CombatCampaignSession.BeginCampaign();
    }

    public static void BeginBossBattle()
    {
        Mode = CombatGameMode.BossBattle;
        BossBattleCompleted = false;
        CombatCampaignSession.LeaveCampaign();
        StageSession.SelectEndless();
    }

    public static void BeginStage(int index)
    {
        Mode = CombatGameMode.Stage;
        BossBattleCompleted = false;
        CombatCampaignSession.LeaveCampaign();
        StageSession.SelectStage(index);
    }

    public static void MarkBossBattleComplete()
    {
        if (Mode == CombatGameMode.BossBattle)
        {
            BossBattleCompleted = true;
        }
    }
}
