using System;
using UnityEngine;

/// <summary>
/// Best stars per stage. A stage unlocks once the one before it has been cleared (any stars).
/// The rules live in <see cref="StageProgressModel"/> (pure, tested); this class persists it.
/// </summary>
public static class StageProgress
{
    private const string KeyPrefix = "TimeRush.Stage.Stars.";

    public static StageProgressModel Load()
    {
        var stars = new int[StageCatalog.Count];
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i] = PlayerPrefs.GetInt(KeyPrefix + i, 0);
        }

        return new StageProgressModel(stars);
    }

    /// <summary>Stores a finished stage run; returns true when it beat the previous best stars.</summary>
    public static bool Record(int stageIndex, int stars)
    {
        StageProgressModel model = Load();
        if (!model.Record(stageIndex, stars))
        {
            return false;
        }

        PlayerPrefs.SetInt(KeyPrefix + stageIndex, model.GetStars(stageIndex));
        PlayerPrefs.Save();
        return true;
    }
}

public sealed class StageProgressModel
{
    private readonly int[] stars;

    public StageProgressModel(int[] stars)
    {
        this.stars = new int[StageCatalog.Count];
        if (stars != null)
        {
            Array.Copy(stars, this.stars, Math.Min(stars.Length, this.stars.Length));
        }

        for (int i = 0; i < this.stars.Length; i++)
        {
            this.stars[i] = Math.Max(0, Math.Min(StageCatalog.MaxStars, this.stars[i]));
        }
    }

    public int GetStars(int index) => StageCatalog.IsValidIndex(index) ? stars[index] : 0;

    public bool IsUnlocked(int index) => StageCatalog.IsValidIndex(index) && (index == 0 || stars[index - 1] > 0);

    public int TotalStars
    {
        get
        {
            int total = 0;
            foreach (int s in stars)
            {
                total += s;
            }

            return total;
        }
    }

    public int MaxTotalStars => stars.Length * StageCatalog.MaxStars;

    public bool Record(int index, int earned)
    {
        if (!StageCatalog.IsValidIndex(index))
        {
            return false;
        }

        earned = Math.Max(0, Math.Min(StageCatalog.MaxStars, earned));
        if (earned <= stars[index])
        {
            return false;
        }

        stars[index] = earned;
        return true;
    }
}
