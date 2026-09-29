using UnityEngine;

/// <summary>
/// Shifts the ambient starlight color/intensity and void-fog color gradually as the run
/// progresses, driven only by GameController.CurrentScore, so the scene visibly changes the
/// longer a run goes instead of looking identical from second 1 to the end -- the ship is really
/// flying from deep void through an asteroid belt, an ice comet field, and finally a nebula core.
/// Uses the same score thresholds as RushTrackEnvironment's asteroid-field zone palette so the
/// lighting and the passing rocks/debris shift together. Purely cosmetic -- it never reads or
/// writes anything related to gameplay, obstacles, or difficulty.
/// </summary>
public sealed class SceneMoodController : MonoBehaviour
{
    [System.Serializable]
    public struct MoodStage
    {
        public int scoreThreshold;
        public Color lightColor;
        public Color fogColor;
        [Range(0f, 3f)] public float lightIntensity;
    }

    [SerializeField] private Light directionalLight;
    [SerializeField] private float transitionSpeed = 0.6f;
    [SerializeField] private MoodStage[] stages = DefaultStages();

    private void Reset()
    {
        stages = DefaultStages();
        directionalLight = GetComponent<Light>();
    }

    private void Awake()
    {
        if (!directionalLight)
        {
            directionalLight = GetComponent<Light>();
        }

        if (stages == null || stages.Length == 0)
        {
            stages = DefaultStages();
        }
    }

    private void Update()
    {
        if (!directionalLight)
        {
            return;
        }

        GameController controller = GameController.Instance;
        int score = controller ? controller.CurrentScore : 0;
        MoodStage target = ResolveStage(score);
        float t = Time.deltaTime * transitionSpeed;

        directionalLight.color = Color.Lerp(directionalLight.color, target.lightColor, t);
        directionalLight.intensity = Mathf.Lerp(directionalLight.intensity, target.lightIntensity, t);

        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, target.fogColor, t);
        }
    }

    private MoodStage ResolveStage(int score)
    {
        MoodStage result = stages[0];
        for (int i = 0; i < stages.Length; i++)
        {
            if (score >= stages[i].scoreThreshold)
            {
                result = stages[i];
            }
        }

        return result;
    }

    private static MoodStage[] DefaultStages()
    {
        return new[]
        {
            // Deep Void -- cool starlit blue-white, the run's opening feel.
            new MoodStage
            {
                scoreThreshold = 0,
                lightColor = new Color(0.92f, 0.94f, 1f, 1f),
                fogColor = new Color(0.0235f, 0.0314f, 0.0784f, 1f),
                lightIntensity = 0.95f,
            },
            // Asteroid Belt -- warm amber starlight scattering off rust-colored rock, first clear
            // shift a couple hundred points in. Matches RushTrackEnvironment's Asteroid Belt zone.
            new MoodStage
            {
                scoreThreshold = 400,
                lightColor = new Color(1f, 0.66f, 0.42f, 1f),
                fogColor = new Color(0.09f, 0.045f, 0.02f, 1f),
                lightIntensity = 1.05f,
            },
            // Ice Comet Field -- pale icy cyan-white, mid-late run. Matches the Ice Comet Field zone.
            new MoodStage
            {
                scoreThreshold = 900,
                lightColor = new Color(0.75f, 0.92f, 1f, 1f),
                fogColor = new Color(0.03f, 0.06f, 0.11f, 1f),
                lightIntensity = 1.05f,
            },
            // Nebula Core -- a rare, hard-earned late-run payoff: violet-magenta glow for a long
            // survival streak. Matches the Nebula Core zone.
            new MoodStage
            {
                scoreThreshold = 1600,
                lightColor = new Color(0.88f, 0.6f, 1f, 1f),
                fogColor = new Color(0.09f, 0.03f, 0.15f, 1f),
                lightIntensity = 1.15f,
            },
        };
    }
}
