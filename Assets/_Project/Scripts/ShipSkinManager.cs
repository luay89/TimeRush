using UnityEngine;

/// <summary>
/// Tracks which ship skins the player has unlocked and which one is selected.
/// Persisted via PlayerPrefs, same pattern as <see cref="PlayerWallet"/> and
/// <see cref="FeedbackPreferences"/>. The first skin ("cyan") is always unlocked for free.
/// </summary>
public static class ShipSkinManager
{
    private const string SelectedKey = "TimeRush.Skins.Selected";
    private const string UnlockedPrefix = "TimeRush.Skins.Unlocked.";

    public static string SelectedSkinId
    {
        get
        {
            string id = PlayerPrefs.GetString(SelectedKey, ShipSkinCatalog.Skins[0].Id);
            return IsUnlocked(id) ? id : ShipSkinCatalog.Skins[0].Id;
        }
    }

    public static bool IsUnlocked(string skinId)
    {
        int index = ShipSkinCatalog.IndexOf(skinId);
        if (ShipSkinCatalog.Skins[index].Price <= 0)
        {
            return true;
        }

        return PlayerPrefs.GetInt(UnlockedPrefix + skinId, 0) == 1;
    }

    /// <summary>Attempts to buy and select a skin. Returns false if already-unlocked or insufficient coins.</summary>
    public static bool TryPurchase(string skinId)
    {
        if (IsUnlocked(skinId))
        {
            Select(skinId);
            return true;
        }

        int index = ShipSkinCatalog.IndexOf(skinId);
        int price = ShipSkinCatalog.Skins[index].Price;

        if (!PlayerWallet.TrySpend(price))
        {
            return false;
        }

        PlayerPrefs.SetInt(UnlockedPrefix + skinId, 1);
        PlayerPrefs.Save();
        Select(skinId);
        return true;
    }

    public static void Select(string skinId)
    {
        if (!IsUnlocked(skinId))
        {
            return;
        }

        PlayerPrefs.SetString(SelectedKey, skinId);
        PlayerPrefs.Save();
    }

    /// <summary>Moves selection to the next skin in the catalog, wrapping around. Locked skins are skipped
    /// unless the player has enough coins to auto-purchase them on the spot.</summary>
    public static string CycleToNext()
    {
        int currentIndex = ShipSkinCatalog.IndexOf(SelectedSkinId);
        int count = ShipSkinCatalog.Skins.Length;

        // Walk forward until a skin is owned or affordable, so an expensive locked skin no longer
        // makes the button look dead (previously it stopped at the first locked skin).
        for (int step = 1; step < count; step++)
        {
            string nextId = ShipSkinCatalog.Skins[(currentIndex + step) % count].Id;
            if (IsUnlocked(nextId))
            {
                Select(nextId);
                break;
            }

            if (TryPurchase(nextId))
            {
                break;
            }
        }

        return SelectedSkinId;
    }
}
