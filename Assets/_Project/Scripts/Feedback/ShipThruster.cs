using UnityEngine;

/// <summary>
/// Procedural thruster-glow particle trail emitted from the back of the player ship. Purely
/// cosmetic (no collider, never read by gameplay/fairness) -- reinforces the "the ship is
/// actually flying forward" cue the space theme relies on. Uses the currently selected ship
/// skin's accent color so it matches whatever hull the player has bought, the same
/// ShipSkinManager/ShipSkinCatalog lookup <see cref="ShipVisualTint"/> already uses.
/// Attached by PlayerController.Awake() to the same "Visual" holder ShipVisualTint sits on, so
/// it works no matter what mesh/material the ship ends up using.
/// </summary>
public sealed class ShipThruster : MonoBehaviour
{
    [SerializeField, Range(0.15f, 1f)] private float particleLifetime = 0.45f;
    [SerializeField, Range(0.05f, 0.6f)] private float particleSize = 0.22f;
    [SerializeField, Range(10f, 90f)] private float emissionRate = 45f;
    [SerializeField] private Vector3 localEmitOffset = new Vector3(0f, 0.05f, -0.55f);

    [Header("Engine Glow")]
    [Tooltip("Hot engine-nozzle flares behind the hull (additive + bloom), like a fighter at full burn. Purely cosmetic.")]
    [SerializeField] private Vector3[] engineNozzles = { new Vector3(-0.2f, 0.03f, -0.62f), new Vector3(0.2f, 0.03f, -0.62f) };
    [SerializeField, Range(0.1f, 1.2f)] private float nozzleGlowSize = 0.34f;
    [SerializeField, Range(1f, 8f)] private float nozzleGlowIntensity = 3.2f;

    private const string GlowShaderPath = "Shaders/TimeRushAdditiveGlow";

    private Material ownedMaterial;
    private Material nozzleMaterial;
    private Color nozzleColor;
    private Transform[] nozzleGlows = System.Array.Empty<Transform>();

    private void Awake()
    {
        BuildThruster();
        BuildNozzleGlows();
    }

    private void Update()
    {
        if (!nozzleMaterial)
        {
            return;
        }

        // Fast, small flicker so the burn reads alive; never tied to gameplay state.
        float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 18f, 0.37f);
        nozzleMaterial.SetColor("_Color", nozzleColor * flicker);
        for (int i = 0; i < nozzleGlows.Length; i++)
        {
            nozzleGlows[i].localScale = Vector3.one * nozzleGlowSize * (0.92f + 0.16f * flicker);
        }
    }

    private void BuildNozzleGlows()
    {
        Shader glowShader = Resources.Load<Shader>(GlowShaderPath);
        if (!glowShader || engineNozzles == null)
        {
            return;
        }

        int index = ShipSkinCatalog.IndexOf(ShipSkinManager.SelectedSkinId);
        Color hull = ShipSkinCatalog.Skins[index].HullColor;
        // Saturated skin-colored burn with a little white heat, pushed into HDR so bloom haloes it.
        nozzleColor = Color.Lerp(hull, Color.white, 0.2f) * nozzleGlowIntensity;
        nozzleColor.a = 1f;

        nozzleMaterial = new Material(glowShader);
        nozzleMaterial.mainTexture = SoftParticleMaterial.SharedTexture;
        nozzleMaterial.SetColor("_Color", nozzleColor);

        nozzleGlows = new Transform[engineNozzles.Length];
        for (int i = 0; i < engineNozzles.Length; i++)
        {
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "NozzleGlow" + i;
            Destroy(glow.GetComponent<Collider>());
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = engineNozzles[i];
            glow.transform.localScale = Vector3.one * nozzleGlowSize;
            var glowRenderer = glow.GetComponent<Renderer>();
            glowRenderer.sharedMaterial = nozzleMaterial;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
            nozzleGlows[i] = glow.transform;
        }
    }

    // SoftParticleMaterial.Create() hands back a fresh Material instance (sharing the module's one
    // cached texture, which must NOT be destroyed here) -- runtime-created Materials aren't freed
    // automatically when their GameObject is destroyed, so every ship respawn would otherwise leak
    // one small material for the rest of the process's lifetime.
    private void OnDestroy()
    {
        if (ownedMaterial)
        {
            Destroy(ownedMaterial);
        }

        if (nozzleMaterial)
        {
            Destroy(nozzleMaterial);
        }
    }

    private void BuildThruster()
    {
        string selectedId = ShipSkinManager.SelectedSkinId;
        int index = ShipSkinCatalog.IndexOf(selectedId);
        Color glowColor = ShipSkinCatalog.Skins[index].AccentColor;

        var thrusterObject = new GameObject("ThrusterGlow");
        thrusterObject.transform.SetParent(transform, false);
        thrusterObject.transform.localPosition = localEmitOffset;
        // Cone's default emit axis is local +Z; rotating 180 degrees around Y sends exhaust out
        // the back of the ship regardless of which way the imported model's own forward faces.
        thrusterObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var system = thrusterObject.AddComponent<ParticleSystem>();

        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = 1.6f;
        main.startSize = particleSize;
        main.startColor = glowColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.maxParticles = 120;

        var emission = system.emission;
        emission.rateOverTime = emissionRate;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 7f;
        shape.radius = 0.06f;

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(glowColor, 0f), new GradientColorKey(glowColor, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

        var particleRenderer = thrusterObject.GetComponent<ParticleSystemRenderer>();
        ownedMaterial = SoftParticleMaterial.Create();
        particleRenderer.material = ownedMaterial;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
    }
}
