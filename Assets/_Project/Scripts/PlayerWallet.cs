using UnityEngine;

/// <summary>
/// Simple persistent coin wallet for cosmetic purchases and the coin-based continue option.
/// Coins are purely cosmetic/convenience currency; they never affect difficulty or fairness.
/// Backed by PlayerPrefs so the balance survives app restarts, same pattern as FeedbackPreferences.
/// </summary>
public static class PlayerWallet
{
    private const string BalanceKey = "TimeRush.Wallet.Coins";
    private const string PendingAmountKey = "TimeRush.Wallet.PendingAmount";

    public static int Balance => Mathf.Max(0, PlayerPrefs.GetInt(BalanceKey, 0));

    /// <summary>Adds coins (e.g. earned at the end of a run) and returns the new balance.</summary>
    public static int Earn(int amount)
    {
        if (amount <= 0)
        {
            return Balance;
        }

        int newBalance = Balance + amount;
        PlayerPrefs.SetInt(BalanceKey, newBalance);
        PlayerPrefs.Save();
        return newBalance;
    }

    /// <summary>
    /// Earns coins for a run that has not yet reached its final death (mirrors
    /// ProgressionProfile.RecordRun/RollbackLastRun): granted immediately so the balance
    /// updates right away, but remembered so a Continue can undo it via
    /// <see cref="RollbackPendingEarn"/> and the eventual final death re-earns the full amount.
    /// </summary>
    public static int EarnPending(int amount)
    {
        int newBalance = Earn(amount);
        PlayerPrefs.SetInt(PendingAmountKey, Mathf.Max(0, amount));
        PlayerPrefs.Save();
        return newBalance;
    }

    /// <summary>Undoes the most recent EarnPending call. No-op if nothing is pending.</summary>
    public static void RollbackPendingEarn()
    {
        int pending = PlayerPrefs.GetInt(PendingAmountKey, 0);
        if (pending <= 0)
        {
            return;
        }

        int current = Balance;
        PlayerPrefs.SetInt(BalanceKey, Mathf.Max(0, current - pending));
        PlayerPrefs.SetInt(PendingAmountKey, 0);
        PlayerPrefs.Save();
    }

    /// <summary>Attempts to spend coins; returns false and changes nothing if the balance is insufficient.</summary>
    public static bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        int current = Balance;
        if (current < amount)
        {
            return false;
        }

        PlayerPrefs.SetInt(BalanceKey, current - amount);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Coins earned for a finished run. Simple, generous-enough-to-feel-rewarding curve.</summary>
    public static int CoinsForScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        return Mathf.Max(1, safeScore / 10);
    }
}
