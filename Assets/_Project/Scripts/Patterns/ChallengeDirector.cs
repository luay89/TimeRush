/// <summary>
/// Small, data-driven sequencing layer that decides a per-beat challenge intensity
/// (<see cref="ChallengeState"/>) and uses it only to shift the same difficulty01 signal
/// already fed to <see cref="PatternDirector.TickBeat"/>. It never selects a pattern, never
/// places an obstacle, and never touches fairness: every existing eligibility, weight, and
/// anti-repetition rule inside <see cref="PatternSelector"/> keeps governing pattern choice
/// exactly as before, and every resulting placement still passes through the unmodified
/// fairness pipeline (<see cref="PatternFairnessProbe"/> / <see cref="FairnessValidator"/>).
///
/// State machine (data-driven via <see cref="ChallengeConfig"/>):
/// NORMAL -- (eligible + probability roll) --> PRESSURE (repeats, capped at MaxConsecutivePressureBeats)
/// PRESSURE -- (cap reached) --> RECOVERY (lasts RecoveryBeats)
/// RECOVERY -- (elapsed) --> NORMAL (with a guaranteed PressureCooldownBeats before PRESSURE can recur)
/// </summary>
public sealed class ChallengeDirector
{
    private readonly ChallengeConfig config;

    private ChallengeState state;
    private int consecutivePressureBeats;
    private int recoveryBeatsRemaining;
    private int cooldownBeatsRemaining;

    public ChallengeDirector(ChallengeConfig config)
    {
        this.config = config;
        state = ChallengeState.Normal;
    }

    public ChallengeState State => state;

    public int ConsecutivePressureBeats => consecutivePressureBeats;

    /// <summary>
    /// Advances the challenge state machine by one beat and returns the effective difficulty
    /// (clamped 0..1) to hand to <see cref="PatternDirector.TickBeat"/> this beat. Must be
    /// called exactly once per spawn beat, in beat order, to keep the sequence reproducible.
    /// </summary>
    public float Advance(float difficulty01, IRandomSource random)
    {
        float clamped = Clamp01(difficulty01);
        AdvanceState(clamped, random);
        return ApplyIntensity(clamped);
    }

    private void AdvanceState(float difficulty01, IRandomSource random)
    {
        switch (state)
        {
            case ChallengeState.Recovery:
                recoveryBeatsRemaining--;
                if (recoveryBeatsRemaining <= 0)
                {
                    state = ChallengeState.Normal;
                    cooldownBeatsRemaining = config.PressureCooldownBeats;
                }
                break;

            case ChallengeState.Pressure:
                if (consecutivePressureBeats >= config.MaxConsecutivePressureBeats)
                {
                    EnterRecovery();
                }
                else
                {
                    consecutivePressureBeats++;
                }
                break;

            default:
                if (cooldownBeatsRemaining > 0)
                {
                    cooldownBeatsRemaining--;
                }
                else if (difficulty01 >= config.MinDifficultyForPressure &&
                         random != null &&
                         random.NextFloat() < config.PressureChance)
                {
                    state = ChallengeState.Pressure;
                    consecutivePressureBeats = 1;
                }
                break;
        }
    }

    private void EnterRecovery()
    {
        state = ChallengeState.Recovery;
        consecutivePressureBeats = 0;
        recoveryBeatsRemaining = config.RecoveryBeats;
    }

    private float ApplyIntensity(float difficulty01)
    {
        switch (state)
        {
            case ChallengeState.Pressure:
                return Clamp01(difficulty01 + config.PressureDifficultyBoost);
            case ChallengeState.Recovery:
                return Clamp01(difficulty01 - config.RecoveryDifficultyCut);
            default:
                return difficulty01;
        }
    }

    private static float Clamp01(float value)
    {
        return value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
