using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MenuHubUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    private void Awake()
    {
        // نضمن وجود UIDocument
        var doc = GetComponent<UIDocument>();
        if (doc == null) doc = gameObject.AddComponent<UIDocument>();

        // نضمن وجود Panel Settings (Unity يوفره كـ Asset افتراضي إذا أنشأته، لذلك ننشئه تلقائياً عند الحاجة)
        if (doc.panelSettings == null)
        {
            doc.panelSettings = Resources.Load<PanelSettings>("DefaultPanelSettings");
        }

        // إذا ماكو PanelSettings جاهز بالResources، ننشئ UI بسيط بدون اعتماد على Asset (باستخدام runtime panel)
        // لكن الافضل نجهّز PanelSettings مرة واحدة (سأسويها بالخطوة الجاية لو احتجنا)
        BuildUI(doc);
    }

    private void BuildUI(UIDocument doc)
    {
        var root = doc.rootVisualElement;
        root.style.flexGrow = 1;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;

        var button = new Button(() => SceneManager.LoadScene(gameSceneName))
        {
            text = "START"
        };

        button.style.width = 320;
        button.style.height = 90;
        button.style.fontSize = 28;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;

        root.Add(button);
    }
}
