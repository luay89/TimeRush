/// <summary>
/// Small deterministic random source for tests and reproducible spawn diagnostics only.
/// </summary>
public sealed class DeterministicRandom
{
    private uint state;

    public DeterministicRandom(uint seed)
    {
        state = seed == 0u ? 0x6D2B79F5u : seed;
    }

    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 1)
        {
            return 0;
        }

        return (int)(NextUInt() % (uint)exclusiveMax);
    }

    public float NextFloat()
    {
        return (NextUInt() & 0x00FFFFFFu) / 16777216f;
    }

    private uint NextUInt()
    {
        uint value = state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        return value;
    }
}
