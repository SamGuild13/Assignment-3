using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private string level1SceneName = "SampleScene";

    [Header("High score text")]
    [SerializeField] private TMP_Text level1ScoreText;
    [SerializeField] private TMP_Text level2ScoreText;

    void Start()
    {
        // Read the saved high score and time for each level (0 and 00:00:00 if none yet).
        ShowScore(level1ScoreText, "Level 1", "Level1");
        ShowScore(level2ScoreText, "Level 2", "Level2");
    }

    private void ShowScore(TMP_Text label, string displayName, string levelKey)
    {
        if (label == null) return;

        int score = HighScoreData.GetScore(levelKey);
        float time = HighScoreData.GetTime(levelKey);

        label.text = $"{displayName} High Score : {score}\nTime : {HighScoreData.FormatTime(time)}";
    }

    public void LoadLevel1()
    {
        SceneManager.LoadScene(level1SceneName);
    }

    public void LoadLevel2()
    {
        // Intentionally empty until the Design Iteration (100% band).
    }
}