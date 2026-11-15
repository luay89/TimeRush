using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight enemy explosion (visual-only) for the space-combat loop.
/// Reuses the same additive-glow shader and procedural meshes the rest of TimeRush uses.
/// </summary>
public sealed class CombatExplosionEffect : MonoBehaviour
{
    private const string GlowShaderPath = "Shaders/TimeRushAdditiveGlow";
    private const float LifetimeSeconds = 1.15f;

    private readonly List<Object> owned = new List<Object>(8);
    private float age;

    public static void Spawn(Vector3 position, float scale)
    {
        var go = new GameObject("CombatExplosion");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * Mathf.Max(0.25f, scale);
        go.AddComponent<CombatExplosionEffect>().Build();
    }

    private void Build()
    {
        Shader glowShader = Resources.Load<Shader>(GlowShaderPath);
        if (!glowShader)
        {
            Destroy(gameObject);
            return;
        }

        Texture2D texture = SoftParticleMaterial.SharedTexture;
        var flashMaterial = Own(new Material(glowShader) { mainTexture = texture });
        flashMaterial.SetColor("_Color", new Color(2.6f, 1.1f, 0.35f, 1f));

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Flash";
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = Vector3.zero;
        quad.GetComponent<Renderer>().sharedMaterial = flashMaterial;

        var ringMaterial = Own(new Material(glowShader) { mainTexture = texture });
        ringMaterial.SetColor("_Color", new Color(0.35f, 1.6f, 2.4f, 0.8f));
        var ring = new GameObject("Ring");
        ring.transform.SetParent(transform, false);
        ring.AddComponent<MeshFilter>().sharedMesh = ProceduralRockMeshes.Ring();
        var ringRenderer = ring.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = ringMaterial;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = Vector3.one * 0.3f;

        BuildSparks(glowShader, texture);

        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (GameAudio.CanPlay())
        {
            GameAudio.PlaySfx(audioSource, Own(CreateExplosionClip()), 0.65f);
        }
    }

    private void BuildSparks(Shader glowShader, Texture2D texture)
    {
        var material = Own(new Material(glowShader) { mainTexture = texture });
        material.SetColor("_Color", new Color(1.6f, 0.8f, 0.25f, 1f));

        var go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.gravityModifier = 0.12f;
        main.maxParticles = 48;
        var emission = system.emission;
        emission.rateOverTime = 0f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.05f;
        renderer.lengthScale = 1.2f;
        system.Play();
        system.Emit(36);
    }

    private static AudioClip CreateExplosionClip()
    {
        const int rate = 44100;
        int length = (int)(rate * 0.42f);
        var data = new float[length];
        var random = new System.Random(77);
        float low = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)length;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * Mathf.Lerp(0.6f, 0.05f, t);
            float thump = Mathf.Sin(i * 2f * Mathf.PI * 45f / rate) * 0.4f;
            data[i] = (low * 0.75f + thump) * Mathf.Exp(-t * 5.2f);
        }

        var clip = AudioClip.Create("CombatExplosion", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private T Own<T>(T asset) where T : Object
    {
        if (asset)
        {
            owned.Add(asset);
        }

        return asset;
    }

    private void OnDestroy()
    {
        foreach (var asset in owned)
        {
            if (asset)
            {
                Destroy(asset);
            }
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / LifetimeSeconds);

        // Keep it camera-facing so it reads on mobile.
        if (Camera.main)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }

        transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.3f, Mathf.Sqrt(t));
        if (age >= LifetimeSeconds)
        {
            Destroy(gameObject);
        }
    }
}

