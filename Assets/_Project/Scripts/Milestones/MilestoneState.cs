using System;
using System.Collections.Generic;

/// <summary>
/// Pure, immutable set of unlocked milestones stored as a bitmask so completion is deterministic and
/// idempotent. Evaluating the same facts twice never re-unlocks a milestone, which is what keeps a
/// Continue (two Results visits for one logical run) from double-counting achievements.
/// </summary>
public readonly struct MilestoneState
{
    /// <summary>Bit <c>(int)MilestoneId</c> is set when that milestone is unlocked.</summary>
    public readonly int Mask;

    public MilestoneState(int mask)
    {
        Mask = mask;
    }

    public static MilestoneState Empty => new MilestoneState(0);

    public bool IsUnlocked(MilestoneId id)
    {
        return (Mask & BitFor(id)) != 0;
    }

    public int UnlockedCount
    {
        get
        {
            int count = 0;
            int bits = Mask;
            while (bits != 0)
            {
                bits &= bits - 1;
                count++;
            }

            return count;
        }
    }

    /// <summary>Result of an evaluation: the updated state plus any milestones unlocked this pass.</summary>
    public readonly struct Evaluation
    {
        public readonly MilestoneState State;
        public readonly MilestoneId[] NewlyUnlocked;

        public Evaluation(MilestoneState state, MilestoneId[] newlyUnlocked)
        {
            State = state;
            NewlyUnlocked = newlyUnlocked ?? Array.Empty<MilestoneId>();
        }

        public bool HasNewUnlocks => NewlyUnlocked.Length > 0;
    }

    /// <summary>
    /// Evaluates every catalog milestone against the supplied facts. Already-unlocked milestones are
    /// skipped, so the returned state only ever adds bits and the newly-unlocked list contains each
    /// milestone at most once, ever.
    /// </summary>
    public Evaluation Evaluate(MilestoneFacts facts)
    {
        int mask = Mask;
        List<MilestoneId> newly = null;

        MilestoneDefinition[] all = MilestoneCatalog.All;
        for (int i = 0; i < all.Length; i++)
        {
            MilestoneDefinition definition = all[i];
            int bit = BitFor(definition.Id);

            if ((mask & bit) != 0)
            {
                continue;
            }

            if (definition.IsSatisfied(facts))
            {
                mask |= bit;
                (newly ?? (newly = new List<MilestoneId>())).Add(definition.Id);
            }
        }

        MilestoneId[] result = newly == null ? Array.Empty<MilestoneId>() : newly.ToArray();
        return new Evaluation(new MilestoneState(mask), result);
    }

    private static int BitFor(MilestoneId id)
    {
        return 1 << (int)id;
    }
}
