/// <summary>
/// Recognizable obstacle pattern families. <see cref="Single"/> is the historic
/// one-obstacle-per-beat baseline; every other value describes a multi-placement shape.
/// Fairness is validated independently of the family a placement came from.
/// </summary>
public enum ObstaclePatternType
{
    Single = 0,
    Alternating = 1,
    DoubleLaneBlock = 2,
    Staggered = 3,
    DepthLaneCombo = 4
}
