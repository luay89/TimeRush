using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds one pooled HUD flash image at startup and fades it through feedback events without blocking gameplay UI.
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class ScreenFeedbackPresenter : MonoBehaviour
{
    [SerializeField] private FeedbackConfig feedbackConfig;

    private Image flashImage;
    private float flashTimeRemaining;
    private float flashDuration;
    private float flashOpacity;
    private Color flashColor;

    private void Awake()
    {
        CreateFlashImage();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearFlash();
    }

    private void Update()
    {
        if (flashTimeRemaining <= 0f || !flashImage)
        {
            return;
        }

        flashTimeRemaining = Mathf.Max(0f, flashTimeRemaining - Time.deltaTime);
        float opacity = flashDuration > 0f ? flashOpacity * (flashTimeRemaining / flashDuration) : 0f;
        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, opacity);

        if (flashTimeRemaining <= 0f)
        {
            ClearFlash();
        }
    }

    private void Subscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.NearMissTriggered += HandleNearMiss;
        events.ObstacleCollision += HandleCollision;
        events.RunPaused += ClearFlash;
    }

    private void Unsubscribe()
    {
        if (!GameFeedbackSignals.HasInstance)
        {
            return;
        }

        var events = GameFeedbackSignals.Instance.Events;
        events.NearMissTriggered -= HandleNearMiss;
        events.ObstacleCollision -= HandleCollision;
        events.RunPaused -= ClearFlash;
    }

    private void CreateFlashImage()
    {
        var imageObject = new GameObject("FeedbackFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        imageObject.transform.SetAsFirstSibling();
        flashImage = imageObject.GetComponent<Image>();
        flashImage.raycastTarget = false;

        var rect = flashImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        ClearFlash();
    }

    private void HandleNearMiss(NearMissFeedback payload)
    {
        BeginFlash(feedbackConfig ? feedbackConfig.nearMissColor : Color.cyan, feedbackConfig ? feedbackConfig.nearMissFlashOpacity : 0f);
    }

    private void HandleCollision(ObstacleCollisionFeedback payload)
    {
        BeginFlash(feedbackConfig ? feedbackConfig.collisionColor : Color.red, feedbackConfig ? feedbackConfig.collisionFlashOpacity : 0f);
    }

    private void BeginFlash(Color color, float opacity)
    {
        if (FeedbackPreferences.IsReduceFlashingEnabled(feedbackConfig) || !flashImage || !feedbackConfig)
        {
            return;
        }

        flashColor = color;
        flashOpacity = opacity;
        flashDuration = feedbackConfig.flashDuration;
        flashTimeRemaining = flashDuration;
        flashImage.color = new Color(color.r, color.g, color.b, opacity);
    }

    private void ClearFlash()
    {
        flashTimeRemaining = 0f;

        if (flashImage)
        {
            flashImage.color = Color.clear;
        }
    }
}
