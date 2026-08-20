using TMPro;
using UnityEngine;

/// <summary>
/// Connects the HUD TextMeshPro label to the GameController score authority.
/// </summary>
public class ScoreUIBinder : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    private void Awake()
    {
        if (!scoreText)
        {
            scoreText = GetComponent<TextMeshProUGUI>();
        }
    }

    private void Start()
    {
        if (!scoreText)
        {
            Debug.LogError("ScoreUIBinder: Missing TextMeshProUGUI reference.", this);
            return;
        }

        if (GameController.Instance != null)
        {
            GameController.Instance.RegisterScoreUI(scoreText);
            return;
        }

        Debug.LogError("GameController not found for Score binding.", this);
    }
}
