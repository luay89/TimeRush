/// <summary>
/// Which mode the next run plays (endless, or a numbered stage) and how the last stage run
/// ended, carried across the Menu -> Game -> Results scene loads.
/// </summary>
public static class StageSession
{
    public static int SelectedIndex { get; private set; } = -1;

    public static bool IsStageRun => StageCatalog.IsValidIndex(SelectedIndex);

    public static StageCatalog.StageDefinition Current => StageCatalog.Stages[SelectedIndex];

    /// <summary>True when Results should present a stage outcome rather than an endless run.</summary>
    public static bool HasLastResult { get; private set; }
    public static int LastIndex { get; private set; } = -1;
    public static bool LastCleared { get; private set; }
    public static int LastStars { get; private set; }
    public static bool LastImprovedBest { get; private set; }

    public static bool HasNextStage => StageCatalog.IsValidIndex(LastIndex + 1);

    public static void SelectStage(int index)
    {
        SelectedIndex = StageCatalog.IsValidIndex(index) ? index : -1;
        HasLastResult = false;
    }

    public static void SelectEndless()
    {
        SelectedIndex = -1;
        HasLastResult = false;
    }

    /// <summary>Records the end of the current stage run (cleared or failed) and stores stars.</summary>
    public static void Finish(bool cleared, int score)
    {
        if (!IsStageRun)
        {
            HasLastResult = false;
            return;
        }

        LastIndex = SelectedIndex;
        LastCleared = cleared;
        LastStars = StageCatalog.StarsFor(Current, cleared, score);
        LastImprovedBest = StageProgress.Record(SelectedIndex, LastStars);
        HasLastResult = true;
    }
}
