using UnityEngine;

/// <summary>
/// Procedural boss model (large, detailed silhouette) until authored boss meshes are added.
/// </summary>
public static class CombatBossVisualFactory
{
    public static void Build(GameObject host)
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
        var armor = new Material(standard) { name = "BossArmor", color = new Color(0.14f, 0.14f, 0.18f) };
        armor.SetFloat("_Metallic", 0.85f);
        armor.SetFloat("_Glossiness", 0.5f);
        armor.EnableKeyword("_EMISSION");
        armor.SetColor("_EmissionColor", new Color(1.2f, 0.22f, 0.18f) * 2.0f);
        armor.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        var core = new Material(standard) { name = "BossCore", color = new Color(0.18f, 0.08f, 0.1f) };
        core.SetFloat("_Metallic", 0.35f);
        core.SetFloat("_Glossiness", 0.75f);
        core.EnableKeyword("_EMISSION");
        core.SetColor("_EmissionColor", new Color(1.6f, 0.35f, 0.25f) * 2.4f);
        core.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        AddPart(root.transform, CombatPrimitiveCache.Cylinder, armor, new Vector3(0f, 0f, -0.6f), new Vector3(4.4f, 1.2f, 6.4f), new Vector3(90f, 0f, 0f));
        AddPart(root.transform, CombatPrimitiveCache.Cylinder, armor, new Vector3(0f, 0.35f, 1.8f), new Vector3(3.1f, 0.9f, 3.6f), new Vector3(90f, 0f, 0f));
        AddPart(root.transform, CombatPrimitiveCache.Cylinder, armor, new Vector3(0f, -0.35f, 1.8f), new Vector3(3.1f, 0.9f, 3.6f), new Vector3(90f, 0f, 0f));

        // Side pylons.
        AddPart(root.transform, CombatPrimitiveCache.Cylinder, armor, new Vector3(5.1f, 0f, -0.4f), new Vector3(0.8f, 0.8f, 4.2f), new Vector3(90f, 0f, 10f));
        AddPart(root.transform, CombatPrimitiveCache.Cylinder, armor, new Vector3(-5.1f, 0f, -0.4f), new Vector3(0.8f, 0.8f, 4.2f), new Vector3(90f, 0f, -10f));
        AddPart(root.transform, CombatPrimitiveCache.Cylinder, armor, new Vector3(6.4f, 0.25f, -2.1f), new Vector3(0.45f, 0.45f, 2.6f), new Vector3(90f, 0f, 0f));
        AddPart(root.transform, CombatPrimitiveCache.Cylinder, armor, new Vector3(-6.4f, 0.25f, -2.1f), new Vector3(0.45f, 0.45f, 2.6f), new Vector3(90f, 0f, 0f));

        // Core / reactor dome.
        AddPart(root.transform, CombatPrimitiveCache.Sphere, core, new Vector3(0f, 0.55f, 0.5f), Vector3.one * 1.8f, Vector3.zero);
        AddPart(root.transform, CombatPrimitiveCache.Sphere, core, new Vector3(0f, -0.55f, 0.5f), Vector3.one * 1.8f, Vector3.zero);

        // Bridge / nose.
        AddPart(root.transform, CombatPrimitiveCache.Capsule, armor, new Vector3(0f, 0.2f, 4.2f), new Vector3(1.2f, 1.2f, 2.8f), new Vector3(90f, 0f, 0f));
        AddPart(root.transform, CombatPrimitiveCache.Capsule, armor, new Vector3(0f, -0.2f, 4.2f), new Vector3(1.2f, 1.2f, 2.8f), new Vector3(90f, 0f, 0f));

        // Make it big.
        root.transform.localScale = Vector3.one * 1.15f;
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
}

