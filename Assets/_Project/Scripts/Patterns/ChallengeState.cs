/// <summary>
/// The three intensity states of the challenge sequencing layer. This is a lightweight
/// sequencing signal only -- it never gates fairness and never appears in any new UI.
/// NORMAL is the ordinary pattern-selection difficulty; PRESSURE briefly nudges the
/// difficulty signal fed to <see cref="PatternDirector"/> upward so slightly more demanding,
/// already-authored pattern combinations surface a little earlier and a little more often;
/// RECOVERY nudges it back down afterward so a demanding stretch is always followed by a
/// calmer one. Every placement produced under any state still passes through the same
/// unmodified fairness pipeline.
/// </summary>
public enum ChallengeState
{
    Normal = 0,
    Pressure = 1,
    Recovery = 2,
}
