using UnityEngine;

/// <summary>
/// Makes the shield pickup read as a shield rather than a plain white ball: a blue glowing core
/// with two crossed rings orbiting it, gently bobbing. Cosmetic only; the pickup's own trigger
/// collider and PowerUpPickup logic are untouched.
/// </summary>
public sealed class ShieldPickupVisual : MonoBehaviour
{
    private static readonly Color ShieldColor = new Color(0.3f, 0.75f, 1f, 1f);

    private Transform ringA;
    private Transform ringB;
    private Material ringMaterial;
    private Material coreMaterial;

    private void Start()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer)
        {
            Material core = renderer.material;
            coreMaterial = core;
            core.color = new Color(0.2f, 0.55f, 1f, 1f);
            if (core.HasProperty("_EmissionColor"))
            {
                core.EnableKeyword("_EMISSION");
                // Kept below the bloom threshold's white-out point so the core stays blue.
                core.SetColor("_EmissionColor", new Color(0.1f, 0.45f, 1f) * 0.9f);
            }
        }

        ringMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0.9f) };
        ringA = CreateRing("RingA", 1.6f);
        ringB = CreateRing("RingB", 1.35f);
    }

    private Transform CreateRing(string name, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Ring();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    private void Update()
    {
        if (!ringA)
        {
            return;
        }

        float t = Time.time;
        ringA.localRotation = Quaternion.Euler(60f, t * 160f, 0f);
        ringB.localRotation = Quaternion.Euler(-60f, -t * 120f, 30f);
        ringMaterial.color = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0.7f + Mathf.Sin(t * 8f) * 0.25f);
    }

    private void OnDestroy()
    {
        if (ringMaterial)
        {
            Destroy(ringMaterial);
        }

        if (coreMaterial)
        {
            Destroy(coreMaterial);
        }
    }
}
