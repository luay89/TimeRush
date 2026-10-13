using UnityEngine;

/// <summary>
/// Procedural meshes for the flanking asteroid field: lumpy, faceted rocks (a displaced
/// icosphere with flat-shaded faces, so the light picks out every facet the way it does on real
/// low-poly rock), glowing crystal shards, a flat planetary ring, and a mottled rock texture.
/// Built once per process and shared; purely cosmetic, no colliders.
/// </summary>
public static class ProceduralRockMeshes
{
    public const int RockVariantCount = 5;

    private static readonly Mesh[] rocks = new Mesh[RockVariantCount];
    private static Mesh crystal;
    private static Mesh ring;
    private static Texture2D rockTexture;

    /// <summary>A rock roughly 1 unit across (same footprint as a unit cube).</summary>
    public static Mesh Rock(int variant)
    {
        variant = Mathf.Abs(variant) % RockVariantCount;
        if (!rocks[variant])
        {
            rocks[variant] = BuildRock(variant);
        }

        return rocks[variant];
    }

    /// <summary>A double-pointed crystal 1 unit tall, 0.35 wide.</summary>
    public static Mesh Crystal()
    {
        if (!crystal)
        {
            var points = new[]
            {
                new Vector3(0f, 0.5f, 0f), new Vector3(0.17f, 0.05f, 0f), new Vector3(0f, -0.02f, 0.15f),
                new Vector3(-0.15f, 0.08f, 0f), new Vector3(0f, 0.03f, -0.17f), new Vector3(0f, -0.5f, 0f),
            };
            int[] faces = { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            crystal = BuildFlatShaded("RockCrystal", points, faces, null);
        }

        return crystal;
    }

    /// <summary>A flat ring in the XZ plane, outer radius 0.5, visible from both sides.</summary>
    public static Mesh Ring()
    {
        if (ring)
        {
            return ring;
        }

        const int segments = 64;
        const float inner = 0.34f;
        const float outer = 0.5f;
        var vertices = new Vector3[segments * 2];
        var uvs = new Vector2[segments * 2];
        var triangles = new int[segments * 12];
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = direction * inner;
            vertices[i * 2 + 1] = direction * outer;
            uvs[i * 2] = new Vector2(i / (float)segments, 0f);
            uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);

            int a = i * 2;
            int b = i * 2 + 1;
            int c = (i * 2 + 2) % (segments * 2);
            int d = (i * 2 + 3) % (segments * 2);
            int t = i * 12;
            triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
            triangles[t + 3] = b; triangles[t + 4] = c; triangles[t + 5] = d;
            triangles[t + 6] = a; triangles[t + 7] = b; triangles[t + 8] = c;
            triangles[t + 9] = b; triangles[t + 10] = d; triangles[t + 11] = c;
        }

        ring = new Mesh { name = "PlanetRing", vertices = vertices, uv = uvs, triangles = triangles };
        ring.RecalculateNormals();
        ring.RecalculateBounds();
        return ring;
    }

    /// <summary>Greyscale mottled rock (speckles, darker patches); tinted by the material colour.</summary>
    public static Texture2D RockTexture()
    {
        if (rockTexture)
        {
            return rockTexture;
        }

        const int size = 128;
        rockTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "ProceduralRock", wrapMode = TextureWrapMode.Repeat };
        var random = new System.Random(4242);
        float offsetX = (float)random.NextDouble() * 100f;
        float offsetY = (float)random.NextDouble() * 100f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Tileable by sampling noise on a torus-like wrap of the coordinates.
                float u = x / (float)size;
                float v = y / (float)size;
                float large = TileableNoise(u, v, 4f, offsetX, offsetY);
                float medium = TileableNoise(u, v, 12f, offsetY, offsetX);
                float fine = (float)random.NextDouble();
                float value = 0.62f + large * 0.28f + medium * 0.14f + (fine - 0.5f) * 0.12f;
                if (fine > 0.985f)
                {
                    value *= 0.55f; // pits
                }

                value = Mathf.Clamp01(value);
                pixels[y * size + x] = new Color(value, value * 0.98f, value * 0.95f, 1f);
            }
        }

        rockTexture.SetPixels(pixels);
        rockTexture.Apply(true, true);
        return rockTexture;
    }

    private static float TileableNoise(float u, float v, float frequency, float ox, float oy)
    {
        float a = Mathf.PerlinNoise(ox + u * frequency, oy + v * frequency);
        float b = Mathf.PerlinNoise(ox + (u - 1f) * frequency, oy + v * frequency);
        float c = Mathf.PerlinNoise(ox + u * frequency, oy + (v - 1f) * frequency);
        float d = Mathf.PerlinNoise(ox + (u - 1f) * frequency, oy + (v - 1f) * frequency);
        float top = Mathf.Lerp(a, b, u);
        float bottom = Mathf.Lerp(c, d, u);
        return Mathf.Lerp(top, bottom, v) - 0.5f;
    }

    private static Mesh BuildRock(int variant)
    {
        BuildIcosphere(2, out Vector3[] points, out int[] faces);
        var random = new System.Random(9173 + variant * 131);

        // Bumps and dents from a handful of random directions: positive amplitudes push lobes
        // out, negative ones carve flattened faces and crater-like dents.
        const int features = 9;
        var directions = new Vector3[features];
        var amplitudes = new float[features];
        var sharpness = new float[features];
        for (int k = 0; k < features; k++)
        {
            directions[k] = RandomDirection(random);
            amplitudes[k] = Mathf.Lerp(-0.3f, 0.22f, (float)random.NextDouble());
            sharpness[k] = Mathf.Lerp(2f, 7f, (float)random.NextDouble());
        }

        var stretch = new Vector3(
            Mathf.Lerp(0.8f, 1.15f, (float)random.NextDouble()),
            Mathf.Lerp(0.75f, 1.1f, (float)random.NextDouble()),
            Mathf.Lerp(0.8f, 1.15f, (float)random.NextDouble()));

        var uvs = new Vector2[points.Length];
        float maxExtent = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 direction = points[i].normalized;
            float radius = 1f;
            for (int k = 0; k < features; k++)
            {
                radius += amplitudes[k] * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(direction, directions[k])), sharpness[k]);
            }

            radius += ((float)random.NextDouble() - 0.5f) * 0.08f;
            Vector3 point = Vector3.Scale(direction * radius, stretch);
            points[i] = point;
            maxExtent = Mathf.Max(maxExtent, Mathf.Abs(point.x), Mathf.Abs(point.y), Mathf.Abs(point.z));
            uvs[i] = new Vector2(Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f, direction.y * 0.5f + 0.5f) * 2f;
        }

        float normalize = 0.5f / Mathf.Max(0.01f, maxExtent);
        for (int i = 0; i < points.Length; i++)
        {
            points[i] *= normalize;
        }

        return BuildFlatShaded($"Rock_{variant}", points, faces, uvs);
    }

    // Unshares vertices so every triangle has its own face normal: faceted, chiselled rock.
    private static Mesh BuildFlatShaded(string name, Vector3[] points, int[] faces, Vector2[] uvs)
    {
        var vertices = new Vector3[faces.Length];
        var flatUvs = new Vector2[faces.Length];
        var triangles = new int[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            vertices[i] = points[faces[i]];
            flatUvs[i] = uvs != null ? uvs[faces[i]] : new Vector2(vertices[i].x + 0.5f, vertices[i].y + 0.5f);
            triangles[i] = i;
        }

        var mesh = new Mesh { name = name, vertices = vertices, uv = flatUvs, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3 RandomDirection(System.Random random)
    {
        float z = (float)random.NextDouble() * 2f - 1f;
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt(1f - z * z);
        return new Vector3(r * Mathf.Cos(angle), z, r * Mathf.Sin(angle));
    }

    private static void BuildIcosphere(int subdivisions, out Vector3[] points, out int[] faces)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var vertexList = new System.Collections.Generic.List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < vertexList.Count; i++)
        {
            vertexList[i] = vertexList[i].normalized;
        }

        var faceList = new System.Collections.Generic.List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        for (int s = 0; s < subdivisions; s++)
        {
            var midpoints = new System.Collections.Generic.Dictionary<long, int>();
            var next = new System.Collections.Generic.List<int>(faceList.Count * 4);
            for (int f = 0; f < faceList.Count; f += 3)
            {
                int a = faceList[f];
                int b = faceList[f + 1];
                int c = faceList[f + 2];
                int ab = Midpoint(a, b, vertexList, midpoints);
                int bc = Midpoint(b, c, vertexList, midpoints);
                int ca = Midpoint(c, a, vertexList, midpoints);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            faceList = next;
        }

        points = vertexList.ToArray();
        faces = faceList.ToArray();
    }

    private static int Midpoint(int a, int b, System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.Dictionary<long, int> cache)
    {
        long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
        if (cache.TryGetValue(key, out int index))
        {
            return index;
        }

        vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
        index = vertices.Count - 1;
        cache[key] = index;
        return index;
    }
}
