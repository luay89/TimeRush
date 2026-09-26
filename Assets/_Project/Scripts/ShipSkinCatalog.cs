using UnityEngine;

/// <summary>
/// Static catalog of purchasable ship color skins. Deliberately simple (color-only, no new
/// assets/textures needed) so it works with the runtime <see cref="ShipVisualTint"/> recolor
/// approach without requiring new imported models or materials.
/// </summary>
public static class ShipSkinCatalog
{
    public readonly struct Skin
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Color HullColor;
        public readonly Color AccentColor;
        public readonly int Price;

        public Skin(string id, string displayName, Color hullColor, Color accentColor, int price)
        {
            Id = id;
            DisplayName = displayName;
            HullColor = hullColor;
            AccentColor = accentColor;
            Price = price;
        }
    }

    public static readonly Skin[] Skins =
    {
        new Skin("cyan", "AZURE (افتراضي)", new Color(0.05f, 0.55f, 0.95f, 1f), new Color(0.85f, 0.95f, 1f, 1f), 0),
        new Skin("amber", "EMBER", new Color(1f, 0.45f, 0.05f, 1f), new Color(1f, 0.85f, 0.4f, 1f), 150),
        new Skin("magenta", "NOVA", new Color(0.85f, 0.1f, 0.65f, 1f), new Color(1f, 0.6f, 0.9f, 1f), 250),
        new Skin("emerald", "VIPER", new Color(0.05f, 0.85f, 0.45f, 1f), new Color(0.6f, 1f, 0.8f, 1f), 350),
        new Skin("gold", "LEGEND", new Color(0.95f, 0.75f, 0.15f, 1f), new Color(1f, 0.95f, 0.7f, 1f), 600),
    };

    public static int IndexOf(string id)
    {
        for (int i = 0; i < Skins.Length; i++)
        {
            if (Skins[i].Id == id)
            {
                return i;
            }
        }

        return 0;
    }
}
