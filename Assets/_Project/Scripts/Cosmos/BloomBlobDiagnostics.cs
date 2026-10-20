#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Development-only probe for the white bloom blobs: every second it looks at what the game
/// camera can see and logs (once each) any renderer whose transform or material colour holds an
/// invalid or absurdly bright value -- the kind of pixel bloom smears into a huge white shape.
/// Compiled out of release builds.
/// </summary>
public sealed class BloomBlobDiagnostics : MonoBehaviour
{
    private const float Limit = 60f;
    private static readonly string[] ColorProperties = { "_Color", "_EmissionColor", "_TintColor" };

    private readonly HashSet<int> reported = new HashSet<int>();
    private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    private float timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.Game || FindObjectOfType<BloomBlobDiagnostics>())
        {
            return;
        }

        var go = new GameObject("BloomBlobDiagnostics");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<BloomBlobDiagnostics>();
    }

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f)
        {
            return;
        }

        timer = 1f;
        foreach (Renderer renderer in FindObjectsOfType<Renderer>())
        {
            if (!renderer.enabled || !renderer.isVisible || reported.Contains(renderer.GetInstanceID()))
            {
                continue;
            }

            string problem = Inspect(renderer);
            if (problem != null)
            {
                reported.Add(renderer.GetInstanceID());
                Debug.LogWarning($"[BLOB-DIAG] {Path(renderer.transform)}: {problem}", renderer);
            }
        }
    }

    private string Inspect(Renderer renderer)
    {
        Transform t = renderer.transform;
        if (Bad(t.position) || Bad(t.lossyScale) || t.lossyScale.magnitude > 500f)
        {
            return $"transform pos {t.position} scale {t.lossyScale}";
        }

        renderer.GetPropertyBlock(block);
        foreach (string property in ColorProperties)
        {
            if (!block.isEmpty)
            {
                Color fromBlock = block.GetColor(property);
                if (Bad(fromBlock))
                {
                    return $"property block {property} = {fromBlock}";
                }
            }

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material && material.HasProperty(property))
                {
                    Color color = material.GetColor(property);
                    if (Bad(color))
                    {
                        return $"material '{material.name}' ({material.shader.name}) {property} = {color}";
                    }
                }
            }
        }

        var particles = renderer as ParticleSystemRenderer;
        if (particles && particles.TryGetComponent(out ParticleSystem system) && system.particleCount > 0)
        {
            var main = system.main;
            if (Bad(main.startColor.color))
            {
                return $"particle start colour {main.startColor.color}";
            }
        }

        return null;
    }

    private static bool Bad(Vector3 v) => float.IsNaN(v.x + v.y + v.z) || float.IsInfinity(v.x + v.y + v.z) || v.magnitude > 1e6f;

    private static bool Bad(Color c) => float.IsNaN(c.r + c.g + c.b) || float.IsInfinity(c.r + c.g + c.b) || c.r > Limit || c.g > Limit || c.b > Limit;

    private static string Path(Transform t)
    {
        string path = t.name;
        while (t.parent)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}
#endif
