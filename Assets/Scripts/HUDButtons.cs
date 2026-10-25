using UnityEngine;
using UnityEngine.SceneManagement;

public class HUDButtons : MonoBehaviour
{
    [SerializeField] private string startSceneName = "StartScene";

    public void ExitToStartScene()
    {
        SceneManager.LoadScene(startSceneName);
    }
}