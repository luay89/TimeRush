using UnityEngine;

/// <summary>
/// Shifts the directional light color/intensity and fog color gradually as the run progresses,
/// driven only by GameController.CurrentScore, so the scene visibly changes the longer a run
/// goes instead of looking identical from second 1 to the end. Purely cosmetic -- it never
/// reads or writes anything related to gameplay, obstacles, or difficulty.
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
            // Cool night blue -- the run's opening feel, unchanged from before.
            new MoodStage
            {
                scoreThreshold = 0,
                lightColor = new Color(0.92f, 0.94f, 1f, 1f),
                fogColor = new Color(0.0235f, 0.0314f, 0.0784f, 1f),
                lightIntensity = 0.95f,
            },
            // Violet dusk -- first clear shift, a couple hundred points in.
            new MoodStage
            {
                scoreThreshold = 400,
                lightColor = new Color(0.82f, 0.68f, 1f, 1f),
                fogColor = new Color(0.06f, 0.03f, 0.12f, 1f),
                lightIntensity = 1f,
            },
            // Warm ember -- mid-late run, the danger/intensity read.
            new MoodStage
            {
                scoreThreshold = 900,
                lightColor = new Color(1f, 0.62f, 0.5f, 1f),
                fogColor = new Color(0.1f, 0.035f, 0.05f, 1f),
                lightIntensity = 1.05f,
            },
            // Teal dawn -- a rare, hard-earned late-run payoff for a long survival streak.
            new MoodStage
            {
                scoreThreshold = 1600,
                lightColor = new Color(0.5f, 0.95f, 0.85f, 1f),
                fogColor = new Color(0.02f, 0.08f, 0.07f, 1f),
                lightIntensity = 1.05f,
            },
        };
    }
}
