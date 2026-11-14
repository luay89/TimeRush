using UnityEngine;

/// <summary>
/// Shared runtime materials/meshes for combat weapons and enemies.
/// Keeps allocations low and avoids needing author-time prefab assets.
/// </summary>
public static class CombatVfxLibrary
{
    private static Material cachedLineMaterial;
    private static Material cachedLaserMaterial;
    private static Material cachedPlasmaMaterial;
    private static Material cachedMissileMaterial;
    private static Material cachedRailMaterial;
    private static Material cachedBeamMaterial;

    public static Material LineMaterial => cachedLineMaterial ? cachedLineMaterial : cachedLineMaterial = CreateLineMaterial();
    public static Material LaserMaterial => cachedLaserMaterial ? cachedLaserMaterial : cachedLaserMaterial = CreateGlowStandard("Laser", new Color(0.35f, 1f, 1.1f, 1f), 1.6f);
    public static Material PlasmaMaterial => cachedPlasmaMaterial ? cachedPlasmaMaterial : cachedPlasmaMaterial = CreateGlowStandard("Plasma", new Color(0.95f, 0.35f, 1.2f, 1f), 1.8f);
    public static Material MissileMaterial => cachedMissileMaterial ? cachedMissileMaterial : cachedMissileMaterial = CreateGlowStandard("Missile", new Color(0.95f, 0.85f, 0.4f, 1f), 1.2f);
    public static Material RailMaterial => cachedRailMaterial ? cachedRailMaterial : cachedRailMaterial = CreateGlowStandard("Rail", new Color(0.65f, 0.95f, 1.6f, 1f), 2.2f);
    public static Material BeamMaterial => cachedBeamMaterial ? cachedBeamMaterial : cachedBeamMaterial = CreateGlowStandard("Beam", new Color(0.25f, 1.05f, 0.65f, 1f), 2.6f);

    private static Material CreateLineMaterial()
    {
        var material = new Material(Shader.Find("Sprites/Default"))
        {
            name = "CombatLine"
        };

        return material;
    }

    private static Material CreateGlowStandard(string name, Color color, float emissionBoost)
    {
        Shader standard = Shader.Find("Standard");
        if (!standard)
        {
            return new Material(Shader.Find("Sprites/Default")) { name = "Combat" + name };
        }

        var material = new Material(standard)
        {
            name = "Combat" + name,
            color = color,
        };
        material.SetFloat("_Metallic", 0.15f);
        material.SetFloat("_Glossiness", 0.7f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * emissionBoost);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return material;
    }
}

