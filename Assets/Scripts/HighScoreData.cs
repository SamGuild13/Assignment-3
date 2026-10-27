using UnityEngine;

// All PlayerPrefs access for high scores lives here, so the game and the StartScene agree.
public static class HighScoreData
{
    private static string ScoreKey(string levelKey) { return levelKey + "_HighScore"; }
    private static string TimeKey(string levelKey) { return levelKey + "_HighTime"; }

    public static bool HasScore(string levelKey)
    {
        return PlayerPrefs.HasKey(ScoreKey(levelKey));
    }

    public static int GetScore(string levelKey)
    {
        return PlayerPrefs.GetInt(ScoreKey(levelKey), 0);
    }

    public static float GetTime(string levelKey)
    {
        return PlayerPrefs.GetFloat(TimeKey(levelKey), 0f);
    }

    // Saves if the score is higher, or the score is the same and the time is lower.
    public static bool TrySave(string levelKey, int score, float time)
    {
        bool isBetter = !HasScore(levelKey)
                        || score > GetScore(levelKey)
                        || (score == GetScore(levelKey) && time < GetTime(levelKey));

        if (!isBetter) return false;

        PlayerPrefs.SetInt(ScoreKey(levelKey), score);
        PlayerPrefs.SetFloat(TimeKey(levelKey), time);
        PlayerPrefs.Save();
        return true;
    }

    // mm:ss:cc where cc is hundredths of a second
    public static string FormatTime(float seconds)
    {
        int totalHundredths = Mathf.FloorToInt(seconds * 100f);
        int hundredths = totalHundredths % 100;
        int secs = (totalHundredths / 100) % 60;
        int minutes = totalHundredths / 6000;

        return $"{minutes:00}:{secs:00}:{hundredths:00}";
    }
}