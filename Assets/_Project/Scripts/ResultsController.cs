using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultsController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "MenuHub";

    private void Awake()
    {
        Time.timeScale = 1f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    public void Menu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }
}