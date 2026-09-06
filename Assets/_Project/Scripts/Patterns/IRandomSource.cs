/// <summary>
/// Minimal random abstraction so pattern selection is deterministic under test
/// (via <see cref="DeterministicRandom"/>) while normal play stays fully random
/// (via <see cref="UnityRandomSource"/>).
/// </summary>
public interface IRandomSource
{
    int NextInt(int exclusiveMax);
    float NextFloat();
}

/// <summary>
/// Live gameplay random source backed by UnityEngine.Random. Never used in tests.
/// </summary>
public sealed class UnityRandomSource : IRandomSource
{
    public static readonly UnityRandomSource Shared = new UnityRandomSource();

    public int NextInt(int exclusiveMax)
    {
        return exclusiveMax <= 1 ? 0 : UnityEngine.Random.Range(0, exclusiveMax);
    }

    public float NextFloat()
    {
        return UnityEngine.Random.value;
    }
}
