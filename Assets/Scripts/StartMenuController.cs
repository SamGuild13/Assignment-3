using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private string level1SceneName = "YourA3SceneName";

    public void LoadLevel1()
    {
        SceneManager.LoadScene(level1SceneName);
    }

    public void LoadLevel2()
    {
        // Intentionally empty until the Design Iteration (100% band).
    }
}