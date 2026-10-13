using UnityEngine;

/// <summary>
/// Builds the enemy-warship look for falling obstacles: a small diving gunship (hazard-orange hull,
/// dark gunmetal cockpit/cannons/engines). The mesh is built once per process from primitive cubes
/// merged into one two-submesh mesh, so every obstacle costs one renderer and no runtime colliders.
/// Purely cosmetic: the obstacle's root BoxCollider, Rigidbody and every fairness calculation are
/// untouched -- the ship is sized to sit inside the same 1x1x1 hit box the cube used to show.
/// </summary>
public static class WarshipObstacleVisual
{
    // Nose dives down toward the deck and toward the chase camera; the deck tilts up toward the
    // camera so the player reads the ship's top silhouette (wings, cockpit) rather than its belly.
    private static readonly Vector3 DiveDirection = new Vector3(0f, -0.55f, -0.83f).normalized;
    private static readonly Vector3 DiveUp = new Vector3(0f, 0.83f, -0.55f).normalized;

    private static Mesh cachedMesh;
    private static Material cachedDetailMaterial;

    public static Quaternion DiveRotation => Quaternion.LookRotation(DiveDirection, DiveUp);

    /// <summary>Creates the warship renderer under parent, using hullMaterial for the hull.</summary>
    public static Transform Create(Transform parent, Material hullMaterial)
    {
        var go = new GameObject("Warship");
        go.transform.SetParent(parent, false);
        go.transform.localRotation = DiveRotation;
        go.AddComponent<MeshFilter>().sharedMesh = ResolveMesh();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { hullMaterial, ResolveDetailMaterial() };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    private static Material ResolveDetailMaterial()
    {
        if (cachedDetailMaterial)
        {
            return cachedDetailMaterial;
        }

        cachedDetailMaterial = new Material(Shader.Find("Standard"))
        {
            name = "WarshipDetail",
            color = new Color(0.16f, 0.17f, 0.2f)
        };
        cachedDetailMaterial.SetFloat("_Metallic", 0.6f);
        cachedDetailMaterial.SetFloat("_Glossiness", 0.5f);
        return cachedDetailMaterial;
    }

    private static Mesh ResolveMesh()
    {
        if (cachedMesh)
        {
            return cachedMesh;
        }

        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cube = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);

        // Hull (submesh 0): fuselage, pointed nose, swept wings, twin tail fins.
        var hull = new[]
        {
            Part(cube, new Vector3(0f, 0f, 0f), new Vector3(0.4f, 0.28f, 1.15f), Quaternion.identity),
            Part(cube, new Vector3(0f, -0.02f, 0.66f), new Vector3(0.24f, 0.18f, 0.36f), Quaternion.Euler(0f, 0f, 45f)),
            Part(cube, new Vector3(-0.4f, 0f, -0.12f), new Vector3(0.6f, 0.06f, 0.42f), Quaternion.Euler(0f, 20f, -6f)),
            Part(cube, new Vector3(0.4f, 0f, -0.12f), new Vector3(0.6f, 0.06f, 0.42f), Quaternion.Euler(0f, -20f, 6f)),
            Part(cube, new Vector3(-0.17f, 0.2f, -0.44f), new Vector3(0.05f, 0.3f, 0.3f), Quaternion.Euler(0f, 0f, -12f)),
            Part(cube, new Vector3(0.17f, 0.2f, -0.44f), new Vector3(0.05f, 0.3f, 0.3f), Quaternion.Euler(0f, 0f, 12f)),
        };

        // Detail (submesh 1): cockpit canopy, wing cannons, engine blocks.
        var detail = new[]
        {
            Part(cube, new Vector3(0f, 0.17f, 0.22f), new Vector3(0.2f, 0.1f, 0.32f), Quaternion.identity),
            Part(cube, new Vector3(-0.6f, -0.02f, 0.12f), new Vector3(0.07f, 0.07f, 0.55f), Quaternion.identity),
            Part(cube, new Vector3(0.6f, -0.02f, 0.12f), new Vector3(0.07f, 0.07f, 0.55f), Quaternion.identity),
            Part(cube, new Vector3(-0.13f, 0f, -0.6f), new Vector3(0.16f, 0.16f, 0.26f), Quaternion.identity),
            Part(cube, new Vector3(0.13f, 0f, -0.6f), new Vector3(0.16f, 0.16f, 0.26f), Quaternion.identity),
        };

        var hullMesh = new Mesh();
        hullMesh.CombineMeshes(hull, true, true);
        var detailMesh = new Mesh();
        detailMesh.CombineMeshes(detail, true, true);

        cachedMesh = new Mesh { name = "WarshipObstacle" };
        cachedMesh.CombineMeshes(new[]
        {
            new CombineInstance { mesh = hullMesh, transform = Matrix4x4.identity },
            new CombineInstance { mesh = detailMesh, transform = Matrix4x4.identity },
        }, false, true);
        cachedMesh.RecalculateBounds();

        Object.Destroy(hullMesh);
        Object.Destroy(detailMesh);
        return cachedMesh;
    }

    private static CombineInstance Part(Mesh mesh, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        return new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) };
    }
}
