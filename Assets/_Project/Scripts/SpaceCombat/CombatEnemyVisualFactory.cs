using UnityEngine;

/// <summary>
/// Generates distinct enemy ship silhouettes from primitives (no placeholder cubes-only look).
/// This is a bridge solution until hand-authored models are added.
/// </summary>
public static class CombatEnemyVisualFactory
{
    public static void Build(GameObject host, CombatEnemyArchetype archetype)
    {
        Transform existing = host.transform.Find("Visual");
        if (existing)
        {
            return;
        }

        var root = new GameObject("Visual");
        root.transform.SetParent(host.transform, false);
        root.transform.localPosition = Vector3.zero;

        Shader standard = Shader.Find("Standard");
        var hull = new Material(standard) { name = "EnemyHull" + archetype };
        hull.color = ResolveHullColor(archetype);
        hull.SetFloat("_Metallic", 0.7f);
        hull.SetFloat("_Glossiness", 0.55f);
        hull.EnableKeyword("_EMISSION");
        hull.SetColor("_EmissionColor", ResolveGlowColor(archetype) * 1.8f);
        hull.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        switch (archetype)
        {
            case CombatEnemyArchetype.FastInterceptor:
                BuildInterceptor(root.transform, hull);
                break;
            case CombatEnemyArchetype.HeavyFighter:
                BuildHeavyFighter(root.transform, hull);
                break;
            case CombatEnemyArchetype.LongRangeShooter:
                BuildSniper(root.transform, hull);
                break;
            case CombatEnemyArchetype.ArmoredDestroyer:
                BuildDestroyer(root.transform, hull);
                break;
            case CombatEnemyArchetype.Elite:
                BuildElite(root.transform, hull);
                break;
            default:
                BuildHeavyFighter(root.transform, hull);
                break;
        }
    }

    private static Color ResolveHullColor(CombatEnemyArchetype archetype)
    {
        return archetype switch
        {
            CombatEnemyArchetype.FastInterceptor => new Color(0.18f, 0.22f, 0.28f),
            CombatEnemyArchetype.HeavyFighter => new Color(0.24f, 0.18f, 0.18f),
            CombatEnemyArchetype.LongRangeShooter => new Color(0.16f, 0.22f, 0.2f),
            CombatEnemyArchetype.ArmoredDestroyer => new Color(0.22f, 0.22f, 0.26f),
            CombatEnemyArchetype.Elite => new Color(0.14f, 0.12f, 0.22f),
            _ => new Color(0.2f, 0.2f, 0.24f),
        };
    }

    private static Color ResolveGlowColor(CombatEnemyArchetype archetype)
    {
        return archetype switch
        {
            CombatEnemyArchetype.FastInterceptor => new Color(0.25f, 1f, 0.85f),
            CombatEnemyArchetype.HeavyFighter => new Color(1f, 0.35f, 0.2f),
            CombatEnemyArchetype.LongRangeShooter => new Color(0.55f, 1f, 0.35f),
            CombatEnemyArchetype.ArmoredDestroyer => new Color(0.9f, 0.75f, 0.25f),
            CombatEnemyArchetype.Elite => new Color(0.85f, 0.35f, 1f),
            _ => new Color(0.35f, 0.8f, 1f),
        };
    }

    private static void AddPart(Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, Vector3 euler)
    {
        var go = new GameObject(mesh.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void BuildInterceptor(Transform root, Material material)
    {
        AddPart(root, CombatPrimitiveCache.Capsule, material, new Vector3(0f, 0f, 0.05f), new Vector3(0.45f, 0.45f, 1.1f), new Vector3(90f, 0f, 0f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0.62f, -0.05f, 0.1f), new Vector3(0.1f, 0.02f, 0.7f), new Vector3(0f, 0f, -20f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(-0.62f, -0.05f, 0.1f), new Vector3(0.1f, 0.02f, 0.7f), new Vector3(0f, 0f, 20f));
        AddPart(root, CombatPrimitiveCache.Sphere, material, new Vector3(0f, 0.12f, 0.35f), Vector3.one * 0.28f, Vector3.zero);
    }

    private static void BuildHeavyFighter(Transform root, Material material)
    {
        AddPart(root, CombatPrimitiveCache.Capsule, material, new Vector3(0f, 0f, -0.05f), new Vector3(0.6f, 0.6f, 1.35f), new Vector3(90f, 0f, 0f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0.0f, -0.22f, -0.2f), new Vector3(0.75f, 0.1f, 0.75f), Vector3.zero);
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0.8f, 0.0f, -0.1f), new Vector3(0.22f, 0.22f, 0.55f), new Vector3(0f, 0f, 90f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(-0.8f, 0.0f, -0.1f), new Vector3(0.22f, 0.22f, 0.55f), new Vector3(0f, 0f, 90f));
    }

    private static void BuildSniper(Transform root, Material material)
    {
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0f, 0f, -0.1f), new Vector3(0.5f, 0.5f, 1.6f), new Vector3(90f, 0f, 0f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0f, 0.16f, 0.65f), new Vector3(0.18f, 0.18f, 0.95f), new Vector3(90f, 0f, 0f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0.95f, 0f, 0.15f), new Vector3(0.06f, 0.02f, 1.0f), new Vector3(0f, 0f, -10f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(-0.95f, 0f, 0.15f), new Vector3(0.06f, 0.02f, 1.0f), new Vector3(0f, 0f, 10f));
    }

    private static void BuildDestroyer(Transform root, Material material)
    {
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0f, -0.05f, -0.25f), new Vector3(0.95f, 0.95f, 1.8f), new Vector3(90f, 0f, 0f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0f, 0.35f, 0.15f), new Vector3(0.55f, 0.55f, 0.9f), new Vector3(90f, 0f, 0f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(1.15f, -0.1f, -0.15f), new Vector3(0.22f, 0.08f, 1.05f), new Vector3(0f, 0f, -18f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(-1.15f, -0.1f, -0.15f), new Vector3(0.22f, 0.08f, 1.05f), new Vector3(0f, 0f, 18f));
    }

    private static void BuildElite(Transform root, Material material)
    {
        AddPart(root, CombatPrimitiveCache.Capsule, material, new Vector3(0f, 0.05f, -0.05f), new Vector3(0.7f, 0.7f, 1.45f), new Vector3(90f, 0f, 0f));
        AddPart(root, CombatPrimitiveCache.Sphere, material, new Vector3(0f, 0.38f, 0.15f), Vector3.one * 0.38f, Vector3.zero);
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0.85f, -0.05f, 0.25f), new Vector3(0.12f, 0.03f, 0.95f), new Vector3(0f, 0f, -28f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(-0.85f, -0.05f, 0.25f), new Vector3(0.12f, 0.03f, 0.95f), new Vector3(0f, 0f, 28f));
        AddPart(root, CombatPrimitiveCache.Cylinder, material, new Vector3(0f, -0.28f, -0.15f), new Vector3(0.9f, 0.08f, 0.9f), Vector3.zero);
    }
}

