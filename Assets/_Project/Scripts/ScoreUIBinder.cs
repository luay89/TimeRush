using TMPro;
using UnityEngine;

/// <summary>
/// Drives the scene-authored gameplay HUD and adds compact runtime telemetry.
/// The existing Score and Best references remain authoritative; the extra labels are
/// created only when the HUD prefab does not already provide them.
/// </summary>
public class ScoreUIBinder : MonoBehaviour
{
    [Header("HUD References")]
    [SerializeField] private TextMeshProUGUI scoreLabel;
    [SerializeField] private TextMeshProUGUI bestLabel;
    [SerializeField] private TextMeshProUGUI survivalTimeLabel;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private TextMeshProUGUI paceLabel;
    [SerializeField] private TextMeshProUGUI flowLabel;
    [SerializeField] private FeedbackConfig feedbackConfig;

    private int lastBest = int.MinValue;
    private float lastDisplayedTime = -1f;
    private float lastDisplayedPace = -1f;
    private int lastDisplayedFlow = int.MinValue;
    private float feedbackTimer;

    private static readonly Color Cyan = new Color(0.12f, 0.95f, 1f, 1f);
    private static readonly Color Violet = new Color(0.62f, 0.35f, 1f, 1f);
    private static readonly Color Muted = new Color(0.68f, 0.76f, 0.9f, 0.92f);
    private static readonly Color White = new Color(0.96f, 0.98f, 1f, 1f);

    private void Start()
    {
        EnsureRuntimeHud();

        var gc = GameController.Instance;
        if (gc == null)
        {
            Debug.LogError("GameController not found for Score binding.", this);
            return;
        }

        if (GameFeedbackSignals.HasInstance)
        {
            GameFeedbackSignals.Instance.Events.NearMissTriggered += HandleNearMiss;
        }

        if (scoreLabel)
        {
            gc.RegisterScoreUI(scoreLabel);
        }
        else
        {
            Debug.LogError("ScoreUIBinder: scoreLabel reference is missing.", this);
        }

        RefreshHud(gc, true);
    }

    private void OnDestroy()
    {
        if (GameFeedbackSignals.HasInstance)
        {
            GameFeedbackSignals.Instance.Events.NearMissTriggered -= HandleNearMiss;
        }
    }

    private void Update()
    {
        if (feedbackTimer > 0f)
        {
            feedbackTimer = Mathf.Max(0f, feedbackTimer - Time.deltaTime);
        }

        var gc = GameController.Instance;
        if (gc == null)
        {
            return;
        }

        RefreshHud(gc, false);
    }

    private void EnsureRuntimeHud()
    {
        if (!scoreLabel)
        {
            scoreLabel = FindLabel("ScoreLabel");
        }

        if (!bestLabel)
        {
            bestLabel = FindLabel("BestLabel");
        }

        ConfigureExistingLabel(scoreLabel, TextAlignmentOptions.Left, 72f, Cyan, new Vector2(56f, -52f), new Vector2(420f, 120f), new Vector2(0f, 1f));
        ConfigureExistingLabel(bestLabel, TextAlignmentOptions.Right, 36f, White, new Vector2(-56f, -60f), new Vector2(360f, 70f), new Vector2(1f, 1f));

        survivalTimeLabel = survivalTimeLabel ? survivalTimeLabel : CreateLabel("SurvivalTimeLabel");
        statusLabel = statusLabel ? statusLabel : CreateLabel("StatusLabel");
        paceLabel = paceLabel ? paceLabel : CreateLabel("PaceLabel");
        flowLabel = flowLabel ? flowLabel : CreateLabel("FlowLabel");

        ConfigureExistingLabel(survivalTimeLabel, TextAlignmentOptions.Center, 48f, White, new Vector2(0f, -46f), new Vector2(430f, 86f), new Vector2(0.5f, 1f));
        ConfigureExistingLabel(statusLabel, TextAlignmentOptions.Center, 22f, Cyan, new Vector2(0f, -126f), new Vector2(640f, 48f), new Vector2(0.5f, 1f));
        ConfigureExistingLabel(paceLabel, TextAlignmentOptions.Right, 24f, Violet, new Vector2(-56f, -112f), new Vector2(360f, 48f), new Vector2(1f, 1f));
        ConfigureExistingLabel(flowLabel, TextAlignmentOptions.Left, 24f, Violet, new Vector2(56f, -150f), new Vector2(420f, 50f), new Vector2(0f, 1f));
    }

    private TextMeshProUGUI FindLabel(string objectName)
    {
        var child = transform.Find(objectName);
        return child ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private TextMeshProUGUI CreateLabel(string objectName)
    {
        var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(transform, false);
        return labelObject.GetComponent<TextMeshProUGUI>();
    }

    private void ConfigureExistingLabel(
        TextMeshProUGUI label,
        TextAlignmentOptions alignment,
        float fontSize,
        Color color,
        Vector2 anchoredPosition,
        Vector2 size,
        Vector2 anchor)
    {
        if (!label)
        {
            return;
        }

        label.alignment = alignment;
        label.fontSize = fontSize;
        label.color = color;
        label.fontWeight = FontWeight.Bold;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color(0.01f, 0.02f, 0.07f, 0.86f);

        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void RefreshHud(GameController gc, bool force)
    {
        RefreshBest(gc);

        float survivalTime = Mathf.Max(0f, gc.AliveTime);
        if (force || Mathf.Abs(survivalTime - lastDisplayedTime) >= 0.05f)
        {
            lastDisplayedTime = survivalTime;
            survivalTimeLabel?.SetText(string.Format("TIME  {0:00}:{1:00}", Mathf.FloorToInt(survivalTime / 60f), Mathf.FloorToInt(survivalTime % 60f)));
        }

        float pace = gc.GetPaceMultiplier();
        if (force || Mathf.Abs(pace - lastDisplayedPace) >= 0.01f)
        {
            lastDisplayedPace = pace;
            paceLabel?.SetText(string.Format("PACE  {0:0.00}x", pace));
        }

        int flow = gc.NearMissChain;
        if (force || flow != lastDisplayedFlow)
        {
            lastDisplayedFlow = flow;

            if (flowLabel)
            {
                if (flow > 0 && gc.FlowTimeRemaining > 0f)
                {
                    flowLabel.color = new Color(Violet.r, Violet.g, Violet.b, Mathf.Clamp01(0.35f + gc.FlowTimeRemaining / 6f));
                    flowLabel.SetText(string.Format("FLOW  x{0}  //  {1}", gc.FlowMultiplier, flow));
                }
                else
                {
                    flowLabel.SetText(string.Empty);
                }
            }
        }

        if (statusLabel && feedbackTimer <= 0f)
        {
            if (gc.IsGameOver)
            {
                statusLabel.color = White;
                statusLabel.SetText("RUN ENDED");
            }
            else
            {
                float opacity = gc.GetControlHintOpacity();
                statusLabel.color = new Color(Cyan.r, Cyan.g, Cyan.b, opacity);
                statusLabel.SetText(opacity > 0.02f ? "A/D  LANE  //  W/S  DEPTH" : string.Empty);
            }
        }
    }

    private void HandleNearMiss(NearMissFeedback feedback)
    {
        if (!statusLabel)
        {
            return;
        }

        feedbackTimer = feedbackConfig ? feedbackConfig.nearMissStatusDuration : 1.15f;
        statusLabel.color = Cyan;
        statusLabel.SetText(feedback.FlowMultiplier > 1
            ? string.Format("NEAR MISS  //  +{0}  x{1}", feedback.Award, feedback.FlowMultiplier)
            : string.Format("NEAR MISS  //  +{0}", feedback.Award));
    }

    private void RefreshBest(GameController gc)
    {
        if (!bestLabel)
        {
            return;
        }

        int best = gc.BestScore;
        if (best == lastBest)
        {
            return;
        }

        lastBest = best;
        bestLabel.SetText("BEST  {0}", best);
    }
}
