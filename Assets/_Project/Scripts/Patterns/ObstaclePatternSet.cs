using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authorable library of obstacle pattern families. Holds data only.
/// When no asset is wired into the spawner, <see cref="CreateDefault"/> supplies a
/// balanced built-in set so gameplay never depends on an editor-authored asset.
/// </summary>
[CreateAssetMenu(fileName = "ObstaclePatternSet", menuName = "TimeRush/Obstacle Pattern Set")]
public sealed class ObstaclePatternSet : ScriptableObject
{
    [SerializeField] private ObstaclePatternDefinition[] patterns;

    public IReadOnlyList<ObstaclePatternDefinition> Patterns => patterns;

    /// <summary>
    /// Built-in, difficulty-gated default families. Depth indices map to the track
    /// depth offsets (0 = near, 1 = center, 2 = far). Every non-baseline family is
    /// authored to leave at least one reachable survival action in its canonical form;
    /// the fairness authority still validates every placement before it can spawn.
    /// </summary>
    public static ObstaclePatternSet CreateDefault()
    {
        var set = CreateInstance<ObstaclePatternSet>();
        set.patterns = new[]
        {
            new ObstaclePatternDefinition(
                "Single", ObstaclePatternType.Single, 0f, 55f, false, System.Array.Empty<PatternPlacement>()),

            new ObstaclePatternDefinition(
                "Alternating Lanes", ObstaclePatternType.Alternating, 0.15f, 18f, true,
                new[]
                {
                    new PatternPlacement(0, 1, 0),
                    new PatternPlacement(2, 1, 1),
                    new PatternPlacement(1, 1, 2),
                }),

            new ObstaclePatternDefinition(
                "Double Lane Block", ObstaclePatternType.DoubleLaneBlock, 0.35f, 12f, true,
                new[]
                {
                    new PatternPlacement(0, 1, 0),
                    new PatternPlacement(1, 1, 0),
                }),

            new ObstaclePatternDefinition(
                "Staggered", ObstaclePatternType.Staggered, 0.5f, 9f, true,
                new[]
                {
                    new PatternPlacement(0, 0, 0),
                    new PatternPlacement(2, 2, 1),
                    new PatternPlacement(1, 1, 2),
                }),

            new ObstaclePatternDefinition(
                "Depth Lane Combo", ObstaclePatternType.DepthLaneCombo, 0.6f, 6f, true,
                new[]
                {
                    new PatternPlacement(1, 0, 0),
                    new PatternPlacement(1, 2, 0),
                }),
        };
        return set;
    }
}
