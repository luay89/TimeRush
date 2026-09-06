using System;
using UnityEngine;

/// <summary>
/// One obstacle placement inside a pattern, expressed as data only.
/// <see cref="Lane"/> is a lane index (0..2). <see cref="DepthIndex"/> indexes the
/// track depth offsets, or -1 to let the spawner pick a free depth. <see cref="Beat"/>
/// is the relative spawn beat (0 = the trigger beat, 1 = the next spawn interval, ...).
/// </summary>
[Serializable]
public struct PatternPlacement
{
    [Min(0)] public int Lane;
    public int DepthIndex;
    [Min(0)] public int Beat;

    public PatternPlacement(int lane, int depthIndex, int beat)
    {
        Lane = lane;
        DepthIndex = depthIndex;
        Beat = beat;
    }
}

/// <summary>
/// Genuine configuration data for a single pattern family. Contains no behavior:
/// selection, scheduling, and fairness all live in dedicated logic classes.
/// </summary>
[Serializable]
public struct ObstaclePatternDefinition
{
    public string Name;
    public ObstaclePatternType Type;

    [Tooltip("Normalized difficulty (0..1) at or above which this pattern may be selected.")]
    [Range(0f, 1f)] public float MinDifficulty;

    [Tooltip("Relative selection weight. Zero disables the pattern.")]
    [Min(0f)] public float Weight;

    [Tooltip("Allow the selector to mirror lanes (0 <-> 2) for extra variety.")]
    public bool AllowMirror;

    public PatternPlacement[] Placements;

    public ObstaclePatternDefinition(string name, ObstaclePatternType type, float minDifficulty, float weight, bool allowMirror, PatternPlacement[] placements)
    {
        Name = name;
        Type = type;
        MinDifficulty = minDifficulty;
        Weight = weight;
        AllowMirror = allowMirror;
        Placements = placements;
    }
}
