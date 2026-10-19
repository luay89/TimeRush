using UnityEngine;

/// <summary>
/// The player's ship blowing apart when a run ends: a white-hot flash, a rolling fireball, a
/// shockwave ring across the flight plane, sparks, tumbling hull fragments and a deep boom.
/// GameController plays it in slow motion before the results screen. The ship's renderers are
/// hidden (not destroyed), so nothing that reads the player object is affected. Visual/audio only.
/// </summary>
public sealed class ShipDeathSequence : MonoBehaviour
{
    private const string GlowShaderPath = "Shaders/TimeRushAdditiveGlow";
    private const float ShockwaveSeconds = 0.55f;
    private const float FlashSeconds = 0.35f;

    private readonly System.Collections.Generic.List<Object> ownedAssets = new System.Collections.Generic.List<Object>(8);
    private Transform shockwave;
    private Material shockwaveMaterial;
    private Transform flash;
    private Material flashMaterial;
    private Color flashColor;
    private Light flashLight;
    private float flashIntensity;
    private Transform[] fragments = System.Array.Empty<Transform>();
    private Vector3[] fragmentVelocity = System.Array.Empty<Vector3>();
    private Vector3[] fragmentSpin = System.Array.Empty<Vector3>();
    private float age;

    public static void Play()
    {
        var player = FindObjectOfType<PlayerController>();
        Vector3 position = player ? player.transform.position : Vector3.zero;
        if (player)
        {
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            foreach (ParticleSystem system in player.GetComponentsInChildren<ParticleSystem>())
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        var go = new GameObject("ShipDeathSequence");
        go.transform.position = position;
        go.AddComponent<ShipDeathSequence>().Build();
        GameFeedbackSignals.RaiseAmbientCameraJolt(1.6f);
    }

    private void Build()
    {
        Shader glowShader = Resources.Load<Shader>(GlowShaderPath);
        bool calm = FeedbackPreferences.IsReduceFlashingEnabled(null);
        Texture2D glowTexture = SoftParticleMaterial.SharedTexture;

        if (glowShader)
        {
            Material fireMaterial = Own(new Material(glowShader) { mainTexture = glowTexture });
            fireMaterial.SetColor("_Color", new Color(2.2f, 1.2f, 0.6f, 1f));
            BuildFireball(fireMaterial);
            BuildSparks(fireMaterial);

            flashMaterial = Own(new Material(glowShader) { mainTexture = glowTexture });
            flashColor = calm ? new Color(1.2f, 0.9f, 0.7f) : new Color(5f, 4f, 3.4f);
            flashMaterial.SetColor("_Color", flashColor);
            flash = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            Destroy(flash.GetComponent<Collider>());
            flash.SetParent(transform, false);
            flash.GetComponent<Renderer>().sharedMaterial = flashMaterial;

            shockwaveMaterial = Own(new Material(glowShader) { mainTexture = CreateShockTexture() });
            Own(shockwaveMaterial.mainTexture);
            shockwave = new GameObject("Shockwave").transform;
            shockwave.SetParent(transform, false);
            shockwave.gameObject.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Ring();
            shockwave.gameObject.AddComponent<MeshRenderer>().sharedMaterial = shockwaveMaterial;
        }

        var lightObject = new GameObject("ExplosionLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.5f;
        flashLight = lightObject.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.6f, 0.3f);
        flashLight.range = 30f;
        flashIntensity = calm ? 2.5f : 7f;
        flashLight.intensity = flashIntensity;

        BuildFragments();

        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (FeedbackPreferences.IsAudioEnabled(null))
        {
            AudioClip boom = Own(CreateBoomClip());
            audioSource.PlayOneShot(boom, 1f);
        }

        Update();
    }

    private T Own<T>(T asset) where T : Object
    {
        if (asset)
        {
            ownedAssets.Add(asset);
        }

        return asset;
    }

    private void OnDestroy()
    {
        foreach (Object asset in ownedAssets)
        {
            if (asset)
            {
                Destroy(asset);
            }
        }
    }

    private void BuildFireball(Material material)
    {
        var system = CreateSystem("Fireball", material);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
        main.maxParticles = 160;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 1.6f)));
        var color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.25f), new GradientColorKey(new Color(0.8f, 0.15f, 0.08f), 0.6f), new GradientColorKey(new Color(0.15f, 0.05f, 0.08f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var drag = system.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.dampen = 0.12f;
        drag.limit = 1.5f;
        system.Play();
        system.Emit(110);
    }

    private void BuildSparks(Material material)
    {
        var system = CreateSystem("Sparks", material);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 20f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(0.4f, 0.9f, 1f));
        main.gravityModifier = 0.6f;
        main.maxParticles = 140;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.06f;
        renderer.lengthScale = 1.5f;
        system.Play();
        system.Emit(120);
    }

    private ParticleSystem CreateSystem(string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission;
        emission.rateOverTime = 0f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return system;
    }

    private void BuildFragments()
    {
        Shader standard = Shader.Find("Standard");
        if (!standard)
        {
            return;
        }

        var material = Own(new Material(standard));
        material.color = new Color(0.25f, 0.27f, 0.32f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(1.2f, 0.35f, 0.05f));
        const int count = 7;
        fragments = new Transform[count];
        fragmentVelocity = new Vector3[count];
        fragmentSpin = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("HullFragment");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Rock(i);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.localScale = new Vector3(Random.Range(0.25f, 0.5f), Random.Range(0.08f, 0.16f), Random.Range(0.25f, 0.6f));
            go.transform.rotation = Random.rotation;
            fragments[i] = go.transform;
            Vector3 direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) * 0.8f + 0.2f;
            fragmentVelocity[i] = direction.normalized * Random.Range(5f, 11f);
            fragmentSpin[i] = Random.insideUnitSphere * 540f;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        Camera camera = Camera.main;

        if (flash)
        {
            float t = Mathf.Clamp01(age / FlashSeconds);
            flash.localScale = Vector3.one * Mathf.Lerp(2f, 11f, Mathf.Sqrt(t));
            flashMaterial.SetColor("_Color", flashColor * (1f - t));
            flash.gameObject.SetActive(t < 1f);
            if (camera)
            {
                flash.rotation = Quaternion.LookRotation(flash.position - camera.transform.position);
            }
        }

        if (shockwave)
        {
            float t = Mathf.Clamp01(age / ShockwaveSeconds);
            shockwave.localScale = Vector3.one * Mathf.Lerp(0.5f, 18f, 1f - (1f - t) * (1f - t));
            shockwaveMaterial.SetColor("_Color", new Color(0.6f, 1.6f, 2.4f) * (1f - t));
            shockwave.gameObject.SetActive(t < 1f);
        }

        if (flashLight)
        {
            flashLight.intensity = flashIntensity * Mathf.Exp(-age * 5f);
        }

        for (int i = 0; i < fragments.Length; i++)
        {
            fragmentVelocity[i] += Vector3.down * 6f * dt;
            fragments[i].position += fragmentVelocity[i] * dt;
            fragments[i].Rotate(fragmentSpin[i] * dt, Space.Self);
        }

        if (age > 4f)
        {
            Destroy(gameObject);
        }
    }

    // Bright rim fading inward: reads as an expanding energy ring.
    private static Texture2D CreateShockTexture()
    {
        const int height = 64;
        var texture = new Texture2D(2, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float alpha = Mathf.Pow(v, 3f) * Mathf.Clamp01((1f - v) * 10f);
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            texture.SetPixel(1, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();
        return texture;
    }

    // Deep boom: a falling sub thump, a roar of low noise and crackle in the tail.
    private static AudioClip CreateBoomClip()
    {
        const int rate = 44100;
        const float seconds = 2.2f;
        int length = (int)(rate * seconds);
        var data = new float[length];
        var random = new System.Random(99);
        float low = 0f;
        float lower = 0f;
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low += (noise - low) * 0.08f;
            lower += (low - lower) * 0.08f;
            phase += 2f * Mathf.PI * Mathf.Lerp(90f, 32f, Mathf.Clamp01(t * 2f)) / rate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 3.2f);
            float roar = lower * 3.2f * Mathf.Exp(-t * 1.6f);
            float crackle = random.NextDouble() < 0.0018 * Mathf.Exp(-t) ? (float)random.NextDouble() * 0.6f : 0f;
            float attack = Mathf.Min(1f, t * 400f);
            data[i] = Mathf.Clamp((thump * 0.8f + roar + crackle) * attack, -1f, 1f) * 0.9f;
        }

        var clip = AudioClip.Create("ShipExplosion", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
