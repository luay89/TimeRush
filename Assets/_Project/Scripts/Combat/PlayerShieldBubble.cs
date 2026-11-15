using UnityEngine;

/// <summary>
/// Glowing blue bubble and spinning ring around the player's ship while it is invulnerable
/// (shield pickup or the grace period after Continue). Reads GameController's existing timer, so
/// it shows exactly when hits are being ignored; flickers during the last second as a warning.
/// Purely visual: no collider.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerShieldBubble : MonoBehaviour
{
    private static readonly Color ShieldColor = new Color(0.3f, 0.75f, 1f, 1f);

    private Transform root;
    private Transform ring;
    private Material bubbleMaterial;
    private Material ringMaterial;

    private void Awake()
    {
        root = new GameObject("ShieldBubble").transform;
        root.SetParent(transform, false);

        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh sphere = probe.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(probe);

        var bubble = new GameObject("Bubble");
        bubble.transform.SetParent(root, false);
        bubble.transform.localScale = new Vector3(2f, 1.3f, 2f);
        bubble.AddComponent<MeshFilter>().sharedMesh = sphere;
        var bubbleRenderer = bubble.AddComponent<MeshRenderer>();
        bubbleMaterial = new Material(Shader.Find("Sprites/Default"));
        bubbleRenderer.sharedMaterial = bubbleMaterial;
        bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bubbleRenderer.receiveShadows = false;

        var ringObject = new GameObject("Ring");
        ringObject.transform.SetParent(root, false);
        ringObject.transform.localScale = Vector3.one * 2.6f;
        ringObject.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Ring();
        var ringRenderer = ringObject.AddComponent<MeshRenderer>();
        ringMaterial = new Material(Shader.Find("Sprites/Default"));
        ringRenderer.sharedMaterial = ringMaterial;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ring = ringObject.transform;

        root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (bubbleMaterial)
        {
            Destroy(bubbleMaterial);
        }

        if (ringMaterial)
        {
            Destroy(ringMaterial);
        }
    }

    private void Update()
    {
        GameController controller = GameController.Instance;
        float remaining = controller && !controller.IsGameOver ? controller.InvulnerabilityRemaining : 0f;
        bool show = remaining > 0f;
        if (root.gameObject.activeSelf != show)
        {
            root.gameObject.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        bool flicker = remaining < 1f && Mathf.Repeat(Time.time * 10f, 1f) < 0.4f;
        float pulse = 0.85f + Mathf.Sin(Time.time * 6f) * 0.15f;
        float bubbleAlpha = flicker ? 0.05f : 0.22f * pulse;
        float ringAlpha = flicker ? 0.15f : 0.85f * pulse;
        bubbleMaterial.color = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, bubbleAlpha);
        ringMaterial.color = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, ringAlpha);
        ring.localRotation = Quaternion.Euler(12f * Mathf.Sin(Time.time * 1.7f), Time.time * 140f, 12f * Mathf.Cos(Time.time * 1.3f));
    }
}
