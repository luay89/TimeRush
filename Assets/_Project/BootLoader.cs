using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    void Start()
    {
        if (GameStateMachine.HasInstance)
        {
            GameStateMachine.Instance.StartBootFlow();
            return;
        }

        SceneManager.LoadScene(SceneNames.MenuHub);
    }
}
