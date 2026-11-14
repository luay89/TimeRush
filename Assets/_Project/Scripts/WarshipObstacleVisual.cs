using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a compact faceted diving fighter for falling obstacles. The shape is assembled into one
/// mesh with three material sections; the obstacle root's collider and Rigidbody remain untouched.
/// </summary>
public static class WarshipObstacleVisual
{
    private static readonly Vector3 DiveDirection = new Vector3(0f, -0.55f, -0.83f).normalized;
    private static readonly Vector3 DiveUp = new Vector3(0f, 0.83f, -0.55f).normalized;

    private static Mesh cachedMesh;
    private static Material cachedDetailMaterial;
    private static Material cachedAccentMaterial;

    public static Quaternion DiveRotation => Quaternion.LookRotation(DiveDirection, DiveUp);

    /// <summary>Creates the warship renderer under parent, using hullMaterial for its armor.</summary>
    public static Transform Create(Transform parent, Material hullMaterial)
    {
        var go = new GameObject("Warship");
        go.transform.SetParent(parent, false);
        go.transform.localRotation = DiveRotation;
        go.AddComponent<MeshFilter>().sharedMesh = ResolveMesh();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { hullMaterial, ResolveDetailMaterial(), ResolveAccentMaterial() };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    private static Material ResolveDetailMaterial()
    {
        if (!cachedDetailMaterial)
        {
            cachedDetailMaterial = new Material(Shader.Find("Standard"))
            {
                name = "WarshipGunmetal",
                color = new Color(0.12f, 0.16f, 0.22f)
            };
            cachedDetailMaterial.SetFloat("_Metallic", 0.72f);
            cachedDetailMaterial.SetFloat("_Glossiness", 0.58f);
        }

        return cachedDetailMaterial;
    }

    private static Material ResolveAccentMaterial()
    {
        if (!cachedAccentMaterial)
        {
            cachedAccentMaterial = new Material(Shader.Find("Standard"))
            {
                name = "WarshipIonGlow",
                color = new Color(0.18f, 0.62f, 0.9f)
            };
            cachedAccentMaterial.EnableKeyword("_EMISSION");
            cachedAccentMaterial.SetColor("_EmissionColor", new Color(0.14f, 0.72f, 1.5f));
            cachedAccentMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            cachedAccentMaterial.SetFloat("_Metallic", 0.25f);
            cachedAccentMaterial.SetFloat("_Glossiness", 0.7f);
        }

        return cachedAccentMaterial;
    }

    private static Mesh ResolveMesh()
    {
        if (cachedMesh)
        {
            return cachedMesh;
        }

        Mesh hull = BuildHullMesh();
        Mesh details = BuildDetailMesh();
        Mesh accents = BuildAccentMesh();
        cachedMesh = new Mesh { name = "FacetedWarshipObstacle" };
        cachedMesh.CombineMeshes(new[]
        {
            new CombineInstance { mesh = hull, transform = Matrix4x4.identity },
            new CombineInstance { mesh = details, transform = Matrix4x4.identity },
            new CombineInstance { mesh = accents, transform = Matrix4x4.identity }
        }, false, true);
        cachedMesh.RecalculateBounds();
        DestroyTemporary(hull);
        DestroyTemporary(details);
        DestroyTemporary(accents);
        return cachedMesh;
    }

    private static Mesh BuildHullMesh()
    {
        var hull = new MeshData();
        const int sides = 8;
        float[] stations = { -0.68f, -0.38f, 0.02f, 0.42f, 0.82f, 0.98f };
        float[] widths = { 0.12f, 0.19f, 0.22f, 0.15f, 0.055f, 0.008f };
        float[] heights = { 0.09f, 0.13f, 0.15f, 0.11f, 0.045f, 0.008f };
        var rings = new Vector3[stations.Length][];
        for (int station = 0; station < stations.Length; station++)
        {
            rings[station] = new Vector3[sides];
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                rings[station][side] = new Vector3(
                    Mathf.Cos(angle) * widths[station],
                    Mathf.Sin(angle) * heights[station],
                    stations[station]);
            }
        }

        for (int station = 0; station < stations.Length - 1; station++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                hull.AddDoubleSidedTriangle(rings[station][side], rings[station][next], rings[station + 1][side]);
                hull.AddDoubleSidedTriangle(rings[station][next], rings[station + 1][next], rings[station + 1][side]);
            }
        }

        AddWing(hull, 1f);
        AddWing(hull, -1f);

        hull.AddPrismXY(
            new[] { new Vector2(-0.07f, 0.08f), new Vector2(0.07f, 0.08f), new Vector2(0.035f, 0.34f), new Vector2(-0.035f, 0.28f) },
            -0.55f, 0.12f);
        return hull.ToMesh("WarshipArmor");
    }

    private static void AddWing(MeshData mesh, float side)
    {
        mesh.AddPrismXZ(new[]
        {
            new Vector2(side * 0.12f, 0.28f),
            new Vector2(side * 0.28f, 0.08f),
            new Vector2(side * 0.61f, -0.14f),
            new Vector2(side * 0.55f, -0.48f),
            new Vector2(side * 0.16f, -0.32f)
        }, 0f, 0.045f);
    }

    private static Mesh BuildDetailMesh()
    {
        var parts = new List<CombineInstance>(12);
        Mesh sphere = ResolvePrimitiveMesh(PrimitiveType.Sphere);
        Mesh cylinder = ResolvePrimitiveMesh(PrimitiveType.Cylinder);
        Mesh cube = ResolvePrimitiveMesh(PrimitiveType.Cube);

        AddPart(parts, sphere, new Vector3(0f, 0.14f, 0.22f), new Vector3(0.2f, 0.1f, 0.34f), Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            AddPart(parts, cylinder, new Vector3(side * 0.15f, -0.01f, -0.48f), new Vector3(0.105f, 0.105f, 0.19f), Quaternion.Euler(90f, 0f, 0f));
            AddPart(parts, cylinder, new Vector3(side * 0.52f, -0.025f, -0.02f), new Vector3(0.028f, 0.028f, 0.38f), Quaternion.Euler(90f, 0f, 0f));
            AddPart(parts, cube, new Vector3(side * 0.35f, 0.035f, -0.24f), new Vector3(0.16f, 0.025f, 0.2f), Quaternion.Euler(0f, side * -14f, 0f));
        }

        var mesh = new Mesh { name = "WarshipGunmetalDetails" };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        return mesh;
    }

    private static Mesh BuildAccentMesh()
    {
        var accents = new MeshData();
        for (int side = -1; side <= 1; side += 2)
        {
            accents.AddPrismXZ(new[]
            {
                new Vector2(side * 0.23f, 0.06f),
                new Vector2(side * 0.29f, 0.02f),
                new Vector2(side * 0.52f, -0.14f),
                new Vector2(side * 0.49f, -0.2f),
                new Vector2(side * 0.24f, -0.08f)
            }, 0.027f, 0.008f);
            accents.AddPrismXZ(new[]
            {
                new Vector2(side * 0.075f, 0.5f),
                new Vector2(side * 0.11f, 0.46f),
                new Vector2(side * 0.055f, 0.25f),
                new Vector2(side * 0.015f, 0.28f)
            }, 0.11f, 0.008f);
        }

        Mesh strips = accents.ToMesh("WarshipIonStrips");
        var parts = new List<CombineInstance>(4)
        {
            new CombineInstance { mesh = strips, transform = Matrix4x4.identity }
        };
        Mesh sphere = ResolvePrimitiveMesh(PrimitiveType.Sphere);
        AddPart(parts, sphere, new Vector3(0f, 0.2f, 0.23f), new Vector3(0.09f, 0.035f, 0.15f), Quaternion.identity);
        AddPart(parts, sphere, new Vector3(-0.15f, -0.01f, -0.59f), new Vector3(0.075f, 0.075f, 0.025f), Quaternion.identity);
        AddPart(parts, sphere, new Vector3(0.15f, -0.01f, -0.59f), new Vector3(0.075f, 0.075f, 0.025f), Quaternion.identity);
        var mesh = new Mesh { name = "WarshipIonAccents" };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        DestroyTemporary(strips);
        return mesh;
    }

    private static void DestroyTemporary(Object instance)
    {
        if (Application.isPlaying)
        {
            Object.Destroy(instance);
        }
        else
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static Mesh ResolvePrimitiveMesh(PrimitiveType type)
    {
        GameObject probe = GameObject.CreatePrimitive(type);
        Mesh mesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);
        return mesh;
    }

    private static void AddPart(List<CombineInstance> parts, Mesh mesh, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
    }

    private sealed class MeshData
    {
        private readonly List<Vector3> vertices = new List<Vector3>(256);
        private readonly List<int> triangles = new List<int>(384);

        public void AddPrismXZ(Vector2[] outline, float centerY, float thickness)
        {
            int count = outline.Length;
            float half = Mathf.Abs(thickness) * 0.5f;
            var top = new Vector3[count];
            var bottom = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                top[i] = new Vector3(outline[i].x, centerY + half, outline[i].y);
                bottom[i] = new Vector3(outline[i].x, centerY - half, outline[i].y);
            }

            AddPrismFaces(top, bottom);
        }

        public void AddPrismXY(Vector2[] outline, float centerZ, float thickness)
        {
            int count = outline.Length;
            float half = Mathf.Abs(thickness) * 0.5f;
            var top = new Vector3[count];
            var bottom = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                top[i] = new Vector3(outline[i].x, outline[i].y, centerZ + half);
                bottom[i] = new Vector3(outline[i].x, outline[i].y, centerZ - half);
            }

            AddPrismFaces(top, bottom);
        }

        public void AddDoubleSidedTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            AddTriangle(a, b, c);
            AddTriangle(c, b, a);
        }

        public Mesh ToMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddPrismFaces(Vector3[] top, Vector3[] bottom)
        {
            for (int i = 1; i < top.Length - 1; i++)
            {
                AddDoubleSidedTriangle(top[0], top[i], top[i + 1]);
                AddDoubleSidedTriangle(bottom[0], bottom[i + 1], bottom[i]);
            }

            for (int i = 0; i < top.Length; i++)
            {
                int next = (i + 1) % top.Length;
                AddDoubleSidedTriangle(top[i], bottom[i], top[next]);
                AddDoubleSidedTriangle(top[next], bottom[i], bottom[next]);
            }
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }
    }
}
