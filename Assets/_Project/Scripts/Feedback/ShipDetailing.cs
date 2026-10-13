using UnityEngine;

/// <summary>
/// Adds small procedural cosmetic add-ons around the player ship's own imported model --
/// blinking wingtip navigation beacons, a glowing cockpit dome accent, and pulsing engine side
/// strips -- so the ship silhouette reads as a real detailed spacecraft instead of a single
/// plain-colored hull. Purely cosmetic: no colliders, never read by gameplay/fairness, and built
/// once in Awake() the same way ShipThruster already adds itself onto the "Visual" holder.
/// Uses the currently selected ship skin's accent color so every add-on always matches whatever
/// hull the player has equipped.
/// </summary>
public sealed class ShipDetailing : MonoBehaviour
{
    private const float BeaconBlinkSpeed = 3.2f;
    private const float EngineGlowPulseSpeed = 5.5f;

    private Material beaconMaterial;
    private Material cockpitMaterial;
    private Material engineGlowMaterial;
    private Color accentColor;

    private void Awake()
    {
        string selectedId = ShipSkinManager.SelectedSkinId;
        int index = ShipSkinCatalog.IndexOf(selectedId);
        accentColor = ShipSkinCatalog.Skins[index].AccentColor;

        BuildCockpitGlow();
        BuildWingtipBeacons();
        BuildEngineGlowStrips();
    }

    // The 3 materials built above are runtime "new Material(...)" instances -- Unity does not free
    // those automatically when this GameObject is destroyed, so without this every ship
    // respawn/skin change would leak 3 small materials for the rest of the process's lifetime.
    private void OnDestroy()
    {
        if (cockpitMaterial)
        {
            Destroy(cockpitMaterial);
        }

        if (beaconMaterial)
        {
            Destroy(beaconMaterial);
        }

        if (engineGlowMaterial)
        {
            Destroy(engineGlowMaterial);
        }
    }

    private void Update()
    {
        // Slow, constant flavor animation -- never derived from gameplay state, so it stays
        // identical whether Camera Shake/reduced-feedback accessibility options are on or off.
        if (beaconMaterial)
        {
            float blink = 0.4f + Mathf.Abs(Mathf.Sin(Time.time * BeaconBlinkSpeed)) * 0.9f;
            beaconMaterial.SetColor("_EmissionColor", accentColor * blink);
        }

        if (engineGlowMaterial)
        {
            float pulse = 0.7f + Mathf.Abs(Mathf.Sin(Time.time * EngineGlowPulseSpeed)) * 0.5f;
            engineGlowMaterial.SetColor("_EmissionColor", accentColor * pulse);
        }
    }

    private Material CreateEmissiveAccent(Color color, float intensity)
    {
        var material = new Material(Shader.Find("Standard"));
        material.color = color * 0.5f;
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetColor("_EmissionColor", color * intensity);
        material.SetFloat("_Metallic", 0.2f);
        material.SetFloat("_Glossiness", 0.6f);
        return material;
    }

    private static GameObject CreateCosmeticPrimitive(string name, Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        var collider = go.GetComponent<Collider>();
        if (collider)
        {
            DestroyImmediate(collider);
        }

        var renderer = go.GetComponent<Renderer>();
        if (renderer && material)
        {
            renderer.sharedMaterial = material;
        }

        return go;
    }

    // Small glowing dome just forward of center -- reads as a lit cockpit canopy without needing
    // an actual transparent-glass shader/material.
    private void BuildCockpitGlow()
    {
        cockpitMaterial = CreateEmissiveAccent(accentColor, 1.4f);
        CreateCosmeticPrimitive("CockpitGlow", transform, PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0.32f), new Vector3(0.22f, 0.14f, 0.28f), cockpitMaterial);
    }

    // A pair of small blinking beacons on either side of the hull -- the classic
    // "this is a real aircraft/spacecraft" nav-light cue, alternating brightness via Update().
    private void BuildWingtipBeacons()
    {
        beaconMaterial = CreateEmissiveAccent(accentColor, 1.6f);
        CreateCosmeticPrimitive("BeaconLeft", transform, PrimitiveType.Sphere, new Vector3(-0.42f, 0.05f, -0.05f), Vector3.one * 0.09f, beaconMaterial);
        CreateCosmeticPrimitive("BeaconRight", transform, PrimitiveType.Sphere, new Vector3(0.42f, 0.05f, -0.05f), Vector3.one * 0.09f, beaconMaterial);
    }

    // Thin glowing strips along the rear flanks of the hull, pulsing gently -- suggests active
    // engine intakes/exhaust vents rather than a plain, static-colored block.
    private void BuildEngineGlowStrips()
    {
        engineGlowMaterial = CreateEmissiveAccent(accentColor, 1.2f);
        CreateCosmeticPrimitive("EngineGlowLeft", transform, PrimitiveType.Cube, new Vector3(-0.2f, 0f, -0.4f), new Vector3(0.05f, 0.05f, 0.35f), engineGlowMaterial);
        CreateCosmeticPrimitive("EngineGlowRight", transform, PrimitiveType.Cube, new Vector3(0.2f, 0f, -0.4f), new Vector3(0.05f, 0.05f, 0.35f), engineGlowMaterial);
    }
}
