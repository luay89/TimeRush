using UnityEngine;

/// <summary>
/// Presents pause state independently; it never stops gameplay or changes player state itself.
/// </summary>
public sealed class PauseOverlayPresenter : MonoBehaviour
{
    private const float PanelWidth = 360f;
    private const float PanelHeight = 180f;

    private void OnGUI()
    {
        if (!GameStateMachine.HasInstance || GameStateMachine.Instance.CurrentState != GameStateKind.Paused)
        {
            return;
        }

        float x = (Screen.width - PanelWidth) * 0.5f;
        float y = (Screen.height - PanelHeight) * 0.5f;
        GUI.Box(new Rect(x, y, PanelWidth, PanelHeight), "PAUSED");

        if (GUI.Button(new Rect(x + 40f, y + 105f, PanelWidth - 80f, 44f), "RESUME"))
        {
            GameStateMachine.Instance.Resume();
        }
    }
}
