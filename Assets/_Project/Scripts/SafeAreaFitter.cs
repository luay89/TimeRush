using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Applies the device safe area at the presentation boundary so individual UI screens do not own screen-edge math.
/// </summary>
[DisallowMultipleComponent]
public sealed class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] private RectTransform targetRectTransform;
    [SerializeField] private float uiToolkitHorizontalPadding = 32f;
    [SerializeField] private float uiToolkitVerticalPadding = 28f;

    private UIDocument document;
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool uiToolkitPaddingApplied;

    private void Awake()
    {
        targetRectTransform = targetRectTransform ? targetRectTransform : GetComponent<RectTransform>();
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        ApplySafeArea();
    }

    private void Start()
    {
        ApplySafeArea();
    }

    private void Update()
    {
        if (HasScreenMetricsChanged() || (document && !uiToolkitPaddingApplied))
        {
            ApplySafeArea();
        }
    }

    private bool HasScreenMetricsChanged()
    {
        return lastScreenWidth != Screen.width || lastScreenHeight != Screen.height || lastSafeArea != Screen.safeArea;
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (targetRectTransform)
        {
            CalculateAnchorBounds(safeArea, Screen.width, Screen.height, out var minimum, out var maximum);
            targetRectTransform.anchorMin = minimum;
            targetRectTransform.anchorMax = maximum;
            targetRectTransform.offsetMin = Vector2.zero;
            targetRectTransform.offsetMax = Vector2.zero;
        }

        ApplyUiToolkitPadding(safeArea);
    }

    private void ApplyUiToolkitPadding(Rect safeArea)
    {
        if (!document || document.rootVisualElement == null || document.rootVisualElement.panel == null)
        {
            uiToolkitPaddingApplied = false;
            return;
        }

        float pixelsPerPoint = Mathf.Max(0.01f, document.rootVisualElement.panel.scaledPixelsPerPoint);
        float left = safeArea.x / pixelsPerPoint + uiToolkitHorizontalPadding;
        float right = (Screen.width - safeArea.xMax) / pixelsPerPoint + uiToolkitHorizontalPadding;
        float bottom = safeArea.y / pixelsPerPoint + uiToolkitVerticalPadding;
        float top = (Screen.height - safeArea.yMax) / pixelsPerPoint + uiToolkitVerticalPadding;

        var root = document.rootVisualElement;
        root.style.paddingLeft = left;
        root.style.paddingRight = right;
        root.style.paddingBottom = bottom;
        root.style.paddingTop = top;
        uiToolkitPaddingApplied = true;
    }

    public static void CalculateAnchorBounds(Rect safeArea, float screenWidth, float screenHeight, out Vector2 minimum, out Vector2 maximum)
    {
        float width = Mathf.Max(1f, screenWidth);
        float height = Mathf.Max(1f, screenHeight);
        minimum = new Vector2(Mathf.Clamp01(safeArea.x / width), Mathf.Clamp01(safeArea.y / height));
        maximum = new Vector2(Mathf.Clamp01(safeArea.xMax / width), Mathf.Clamp01(safeArea.yMax / height));
    }
}
