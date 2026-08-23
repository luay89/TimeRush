using TMPro;
using UnityEngine;

/// <summary>
/// Drives the scene-authored in-game Score HUD (see HUD.prefab).
/// Displays the live Score (large) and Best (smaller) during gameplay.
/// The Score label is bound to the GameController score authority via
/// <see cref="GameController.RegisterScoreUI"/>; the Best label is a read-only
/// mirror of <see cref="GameController.BestScore"/>.
/// This component only displays data — it never creates UI at runtime and never
/// changes scoring or difficulty logic.
/// </summary>
public class ScoreUIBinder : MonoBehaviour
{
    [Header("HUD References (assigned in HUD.prefab)")]
    [SerializeField] private TextMeshProUGUI scoreLabel;
    [SerializeField] private TextMeshProUGUI bestLabel;

    private int lastBest = int.MinValue;

    private void Start()
    {
        var gc = GameController.Instance;
        if (gc == null)
        {
            Debug.LogError("GameController not found for Score binding.", this);
            return;
        }

        if (scoreLabel)
        {
            gc.RegisterScoreUI(scoreLabel);
        }
        else
        {
            Debug.LogError("ScoreUIBinder: scoreLabel reference is missing.", this);
        }

        RefreshBest(gc);
    }

    private void Update()
    {
        var gc = GameController.Instance;
        if (gc == null)
        {
            return;
        }

        RefreshBest(gc);
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
        bestLabel.SetText("Best: {0}", best);
    }
}
