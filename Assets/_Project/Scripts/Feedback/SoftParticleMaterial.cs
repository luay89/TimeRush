using UnityEngine;

/// <summary>
/// Shared factory for a simple soft round particle material, used by ShipThruster and
/// RushTrackEnvironment's speed dust. Originally both tried Resources.GetBuiltinResource
/// <Material>("Default-Particle.mat"), which fails to load in this project/Unity version
/// ("Failed to find Default-Particle.mat" / "could not be loaded from the resource file") and
/// logged an error every time a ship or the environment was (re)built -- e.g. every Continue.
/// This generates its own small radial-falloff texture once (cached, never reloaded) and uses
/// Sprites/Default, a shader guaranteed to exist in every Unity project and already alpha-blended
/// by default, so no GetBuiltinResource call and no per-material blend-mode setup is needed.
/// </summary>
public static class SoftParticleMaterial
{
    private const int TextureSize = 32;

    private static Texture2D cachedTexture;

    public static Material Create()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = ResolveTexture();
        return material;
    }

    /// <summary>Shared radial glow texture (cached; callers must never destroy it).</summary>
    public static Texture2D SharedTexture => ResolveTexture();

    private static Texture2D ResolveTexture()
    {
        if (cachedTexture)
        {
            return cachedTexture;
        }

        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(TextureSize / 2f, TextureSize / 2f);
        float maxDistance = TextureSize / 2f;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(1f - distance / maxDistance);
                alpha *= alpha; // soften the falloff into a round glow instead of a hard disc
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        cachedTexture = texture;
        return texture;
    }
}
