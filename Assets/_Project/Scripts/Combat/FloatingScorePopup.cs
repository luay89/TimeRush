using UnityEngine;

/// <summary>
/// A short "+20" that rises from a destroyed warship and fades out, facing the camera. Uses the
/// built-in legacy font through a TextMesh so it needs no font asset or canvas. Purely visual.
/// </summary>
public sealed class FloatingScorePopup : MonoBehaviour
{
    private const float Lifetime = 0.9f;
    private const float RiseSpeed = 2.6f;

    private static Font cachedFont;

    private TextMesh text;
    private Color baseColor;
    private float age;

    public static void Spawn(Vector3 position, string label, Color color)
    {
        Font font = ResolveFont();
        if (!font)
        {
            return;
        }

        var go = new GameObject("ScorePopup");
        go.transform.position = position + Vector3.up * 0.6f;
        var popup = go.AddComponent<FloatingScorePopup>();
        popup.text = go.AddComponent<TextMesh>();
        popup.text.font = font;
        popup.text.text = label;
        popup.text.fontSize = 64;
        popup.text.characterSize = 0.06f;
        popup.text.anchor = TextAnchor.MiddleCenter;
        popup.text.alignment = TextAlignment.Center;
        popup.text.fontStyle = FontStyle.Bold;
        popup.text.color = color;
        popup.baseColor = color;

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        popup.FaceCamera();
    }

    private static Font ResolveFont()
    {
        if (!cachedFont)
        {
            // Unity 2022.2+ ships "LegacyRuntime.ttf"; older versions used "Arial.ttf".
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cachedFont;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float t = age / Lifetime;
        transform.position += Vector3.up * (RiseSpeed * (1f - t * 0.6f) * Time.deltaTime);
        float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, (t - 0.15f) / 0.85f);
        transform.localScale = Vector3.one * pop;
        text.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t * t));
        FaceCamera();
    }

    private void FaceCamera()
    {
        Camera camera = Camera.main;
        if (camera)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, camera.transform.up);
        }
    }
}
