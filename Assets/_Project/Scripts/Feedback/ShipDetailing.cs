using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds a swept armored wing, paired cannons, engine nacelles, and cosmetic lighting around the
/// player's imported hull so it reads as a fighter rather than a plain speeder. These details are
/// purely cosmetic: they have no colliders, are never read by gameplay/fairness, and are built
/// once in Awake() on the same "Visual" holder as ShipThruster.
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
    private Material engineCasingMaterial;
    private Color accentColor;
    private Mesh wingArmorMesh;

    private void Awake()
    {
        string selectedId = ShipSkinManager.SelectedSkinId;
        int index = ShipSkinCatalog.IndexOf(selectedId);
        accentColor = ShipSkinCatalog.Skins[index].AccentColor;

        BuildCockpitGlow();
        BuildWingtipBeacons();
        BuildEngineGlowStrips();
    }

    // These materials are runtime "new Material(...)" instances -- Unity does not free
    // those automatically when this GameObject is destroyed, so without this every ship
    // respawn/skin change would leak them for the rest of the process's lifetime.
    private void OnDestroy()
    {
        DestroyOwned(cockpitMaterial);
        DestroyOwned(beaconMaterial);
        DestroyOwned(engineGlowMaterial);
        DestroyOwned(engineCasingMaterial);
        DestroyOwned(wingArmorMesh);
    }

    private static void DestroyOwned(Object asset)
    {
        if (!asset)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(asset);
        }
        else
        {
            DestroyImmediate(asset);
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
        cockpitMaterial = CreateEmissiveAccent(accentColor, 0.75f);
        CreateCosmeticPrimitive("CockpitGlow", transform, PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0.32f), new Vector3(0.18f, 0.11f, 0.25f), cockpitMaterial);
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

        engineCasingMaterial = new Material(Shader.Find("Standard"))
        {
            name = "PlayerEngineCasing",
            color = new Color(0.12f, 0.16f, 0.22f)
        };
        engineCasingMaterial.SetFloat("_Metallic", 0.72f);
        engineCasingMaterial.SetFloat("_Glossiness", 0.58f);

        BuildSweptWings();
        BuildForwardCannons();

        for (int side = -1; side <= 1; side += 2)
        {
            CreateCosmeticPrimitive(
                "EngineNacelle", transform, PrimitiveType.Cylinder,
                new Vector3(side * 0.19f, -0.015f, -0.43f),
                new Vector3(0.105f, 0.09f, 0.18f),
                engineCasingMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateCosmeticPrimitive(
                "EngineExhaust", transform, PrimitiveType.Cylinder,
                new Vector3(side * 0.19f, -0.015f, -0.58f),
                new Vector3(0.065f, 0.065f, 0.025f),
                engineGlowMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateCosmeticPrimitive(
                "WingArmorPanel", transform, PrimitiveType.Cube,
                new Vector3(side * 0.3f, 0.045f, -0.12f),
                new Vector3(0.16f, 0.018f, 0.24f),
                engineCasingMaterial).transform.localRotation = Quaternion.Euler(0f, side * -12f, 0f);
            CreateCosmeticPrimitive(
                "WingSignalStrip", transform, PrimitiveType.Cube,
                new Vector3(side * 0.3f, 0.058f, -0.12f),
                new Vector3(0.035f, 0.008f, 0.16f),
                engineGlowMaterial).transform.localRotation = Quaternion.Euler(0f, side * -12f, 0f);
            CreateCosmeticPrimitive(
                "WingtipFin", transform, PrimitiveType.Cube,
                new Vector3(side * 0.67f, 0.11f, -0.17f),
                new Vector3(0.075f, 0.19f, 0.2f),
                engineCasingMaterial).transform.localRotation = Quaternion.Euler(0f, side * -12f, side * -8f);
        }
    }

    private void BuildSweptWings()
    {
        var vertices = new List<Vector3>(16);
        var triangles = new List<int>(48);
        AddWing(vertices, triangles, -1);
        AddWing(vertices, triangles, 1);

        wingArmorMesh = new Mesh { name = "PlayerSweptArmorWings" };
        wingArmorMesh.SetVertices(vertices);
        wingArmorMesh.SetTriangles(triangles, 0);
        wingArmorMesh.RecalculateNormals();

        var wings = new GameObject("SweptArmorWings");
        wings.transform.SetParent(transform, false);
        wings.AddComponent<MeshFilter>().sharedMesh = wingArmorMesh;
        var renderer = wings.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = engineCasingMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void AddWing(List<Vector3> vertices, List<int> triangles, int side)
    {
        int first = vertices.Count;
        vertices.Add(new Vector3(side * 0.16f, 0.035f, 0.16f));
        vertices.Add(new Vector3(side * 0.78f, 0.025f, -0.015f));
        vertices.Add(new Vector3(side * 0.61f, 0.02f, -0.29f));
        vertices.Add(new Vector3(side * 0.19f, 0.045f, -0.36f));
        vertices.Add(new Vector3(side * 0.16f, 0.005f, 0.16f));
        vertices.Add(new Vector3(side * 0.78f, 0.005f, -0.015f));
        vertices.Add(new Vector3(side * 0.61f, 0.005f, -0.29f));
        vertices.Add(new Vector3(side * 0.19f, 0.005f, -0.36f));

        if (side > 0)
        {
            triangles.AddRange(new[]
            {
                first, first + 1, first + 2, first, first + 2, first + 3,
                first + 4, first + 6, first + 5, first + 4, first + 7, first + 6
            });
        }
        else
        {
            triangles.AddRange(new[]
            {
                first, first + 2, first + 1, first, first + 3, first + 2,
                first + 4, first + 5, first + 6, first + 4, first + 6, first + 7
            });
        }

        for (int edge = 0; edge < 4; edge++)
        {
            int next = (edge + 1) % 4;
            triangles.Add(first + edge);
            triangles.Add(first + next);
            triangles.Add(first + next + 4);
            triangles.Add(first + edge);
            triangles.Add(first + next + 4);
            triangles.Add(first + edge + 4);
        }
    }

    private void BuildForwardCannons()
    {
        for (int side = -1; side <= 1; side += 2)
        {
            CreateCosmeticPrimitive(
                "ForwardCannon", transform, PrimitiveType.Cylinder,
                new Vector3(side * 0.45f, 0.055f, 0.18f),
                new Vector3(0.045f, 0.15f, 0.045f),
                engineCasingMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateCosmeticPrimitive(
                "CannonMuzzle", transform, PrimitiveType.Sphere,
                new Vector3(side * 0.45f, 0.055f, 0.34f),
                Vector3.one * 0.065f,
                engineGlowMaterial);
        }
    }
}
