using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string resultsSceneName = "Results";

    private bool isDead;

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead || collision == null || collision.gameObject == null)
        {
            return;
        }

        if (!collision.gameObject.CompareTag(obstacleTag))
        {
            return;
        }

        isDead = true;
        StartCoroutine(HandleDeath());
    }

    private IEnumerator HandleDeath()
    {
        Time.timeScale = 0f;

        // نستخدم وقت حقيقي لأن Time.timeScale = 0
        yield return new WaitForSecondsRealtime(0.05f);

        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(resultsSceneName))
        {
            SceneManager.LoadScene(resultsSceneName);
        }
        else
        {
            Debug.LogWarning($"Scene '{resultsSceneName}' is not in Build Settings. Time scale was restored to 1.");
            isDead = false;
        }
    }

    private void OnDisable()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }
    }
}