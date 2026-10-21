using System;

/// <summary>
/// The ten hand-tuned stages of Stage mode. A stage is a run with a finish line: survive
/// GoalSeconds to clear it. Later stages start further along the regular difficulty curve
/// (DifficultyHeadStart seconds of "virtual" alive time), so stage 10 opens near full intensity.
/// Stars: clearing earns one, and the run's score earns the second and third. Pure data and
/// maths so it is covered by EditMode tests.
/// </summary>
public static class StageCatalog
{
    public const int MaxStars = 3;

    public readonly struct StageDefinition
    {
        public readonly int Number;
        public readonly string Name;
        public readonly float GoalSeconds;
        public readonly float DifficultyHeadStart;
        public readonly int TwoStarScore;
        public readonly int ThreeStarScore;

        public StageDefinition(int number, string name, float goalSeconds, float difficultyHeadStart, int twoStarScore, int threeStarScore)
        {
            Number = number;
            Name = name;
            GoalSeconds = goalSeconds;
            DifficultyHeadStart = difficultyHeadStart;
            TwoStarScore = twoStarScore;
            ThreeStarScore = threeStarScore;
        }
    }

    public static readonly StageDefinition[] Stages =
    {
        Make(1, "FIRST CONTACT", 30f, 0f),
        Make(2, "PATROL ROUTE", 35f, 8f),
        Make(3, "CROSSFIRE", 40f, 18f),
        Make(4, "DEBRIS RUN", 45f, 28f),
        Make(5, "BLOCKADE", 50f, 40f),
        Make(6, "COMET WAKE", 55f, 52f),
        Make(7, "IRON CURTAIN", 60f, 64f),
        Make(8, "DEEP STRIKE", 65f, 78f),
        Make(9, "NEBULA STORM", 70f, 92f),
        Make(10, "LAST STAND", 80f, 110f),
    };

    public static int Count => Stages.Length;

    public static bool IsValidIndex(int index) => index >= 0 && index < Stages.Length;

    /// <summary>Stars earned by a finished stage run (0 when the stage was not cleared).</summary>
    public static int StarsFor(StageDefinition stage, bool cleared, int score)
    {
        if (!cleared)
        {
            return 0;
        }

        if (score >= stage.ThreeStarScore)
        {
            return 3;
        }

        return score >= stage.TwoStarScore ? 2 : 1;
    }

    // Survival alone pays 10 points a second; the extra stars ask for kills and near misses on top.
    private static StageDefinition Make(int number, string name, float goalSeconds, float headStart)
    {
        int survival = (int)Math.Round(goalSeconds * 10f);
        return new StageDefinition(number, name, goalSeconds, headStart, RoundTo10(survival * 1.25f), RoundTo10(survival * 1.6f));
    }

    private static int RoundTo10(float value) => (int)Math.Round(value / 10f) * 10;
}
