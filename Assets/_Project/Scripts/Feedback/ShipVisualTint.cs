using UnityEngine;

/// <summary>
/// Recolors the player ship's imported model at runtime so it reads as a solid,
/// saturated color instead of the FBX importer's flat default gray/white
/// (which combined with Bloom made the ship look washed-out and pale).
/// Attached to the "Visual" holder above the ship model, not the model itself,
/// so it works no matter what mesh/material the ship ends up using.
/// Also applies whichever skin the player has selected via <see cref="ShipSkinManager"/>,
/// falling back to the inspector colors below if no skin system is available.
/// </summary>
public sealed class ShipVisualTint : MonoBehaviour
{
    [SerializeField] private Color hullColor = new Color(0.05f, 0.55f, 0.95f, 1f);
    [SerializeField] private Color accentColor = new Color(0.85f, 0.95f, 1f, 1f);
    [Range(0f, 3f)] [SerializeField] private float emissionBoost = 0.6f;

    private void Awake()
    {
        ApplySelectedSkin();
        ApplyTint();
    }

    private void ApplySelectedSkin()
    {
        string selectedId = ShipSkinManager.SelectedSkinId;
        int index = ShipSkinCatalog.IndexOf(selectedId);
        ShipSkinCatalog.Skin skin = ShipSkinCatalog.Skins[index];
        hullColor = skin.HullColor;
        accentColor = skin.AccentColor;
    }

    private void ApplyTint()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        int rendererIndex = 0;

        foreach (var meshRenderer in renderers)
        {
            // Alternate between hull and accent color across sub-meshes so the ship
            // doesn't read as one flat blob, while staying deterministic (no per-frame cost).
            Color baseColor = rendererIndex % 3 == 0 ? accentColor : hullColor;
            rendererIndex++;

            Material[] instancedMaterials = meshRenderer.materials;
            for (int i = 0; i < instancedMaterials.Length; i++)
            {
                Material material = instancedMaterials[i];

                if (material.HasProperty("_Color"))
                {
                    material.color = baseColor;
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", baseColor * emissionBoost);
                }
            }

            meshRenderer.materials = instancedMaterials;
        }
    }
}
