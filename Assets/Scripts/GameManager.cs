using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Countdown, Playing, GameOver }
    public GameState State { get; private set; } = GameState.Countdown;

    [Header("Scene References")]
    [SerializeField] private PacStudentController pacStudent;

    [Header("HUD")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private GameObject countdownBlocker;
    [SerializeField] private Image[] lifeIcons;           // Life1, Life2, Life3

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip introClip;         // plays during the countdown
    [SerializeField] private AudioClip normalClip;        // loops once the game starts

    [Header("Settings")]
    [SerializeField] private int startingLives = 3;

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public float ElapsedTime { get; private set; }

    void Awake()
    {
        Instance = this;
        Score = 0;
        Lives = startingLives;
        ElapsedTime = 0f;

        // Nobody moves until the countdown has finished.
        if (pacStudent != null) pacStudent.InputEnabled = false;

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (countdownBlocker != null) countdownBlocker.SetActive(false);

        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.Stop();
        }
    }

    void Start()
    {
        UpdateScoreUI();
        UpdateLivesUI();
        UpdateTimerUI();
        StartCoroutine(CountdownRoutine());
    }

    void Update()
    {
        // The timer only runs while the game is actually being played.
        if (State == GameState.Playing)
        {
            ElapsedTime += Time.deltaTime;
            UpdateTimerUI();
        }
    }

    private IEnumerator CountdownRoutine()
    {
        State = GameState.Countdown;

        countdownBlocker.SetActive(true);
        countdownText.gameObject.SetActive(true);

        PlayMusic(introClip, false);

        string[] steps = { "3", "2", "1", "GO!" };
        foreach (string step in steps)
        {
            countdownText.text = step;
            yield return new WaitForSeconds(1f);
        }

        // "GO!" has been shown for 1 second: hide the countdown and start the game.
        countdownText.gameObject.SetActive(false);
        countdownBlocker.SetActive(false);

        PlayNormalMusic();

        State = GameState.Playing;
        if (pacStudent != null) pacStudent.InputEnabled = true;
    }

    // ---- Used by other scripts ----

    public void AddScore(int points)
    {
        Score += points;
        UpdateScoreUI();
    }

    public void SetLives(int lives)
    {
        Lives = Mathf.Max(0, lives);
        UpdateLivesUI();
    }

    // Called when PacStudent has no lives left (the Game Over screen is added in part 5).
    public void TriggerGameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;
        if (pacStudent != null) pacStudent.InputEnabled = false;
    }

    public void PlayNormalMusic()
    {
        PlayMusic(normalClip, true);
    }

    public void PlayMusic(AudioClip clip, bool loop)
    {
        if (musicSource == null || clip == null) return;

        // Already playing this track: don't restart it.
        if (musicSource.isPlaying && musicSource.clip == clip) return;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    // ---- UI ----

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = Score.ToString("D6");
    }

    private void UpdateLivesUI()
    {
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] != null) lifeIcons[i].enabled = i < Lives;
        }
    }

    // mm:ss:cc where cc is hundredths of a second (01:32:25 = 1 min, 32 s, 250 ms)
    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int totalHundredths = Mathf.FloorToInt(ElapsedTime * 100f);
        int hundredths = totalHundredths % 100;
        int seconds = (totalHundredths / 100) % 60;
        int minutes = totalHundredths / 6000;

        timerText.text = $"{minutes:00}:{seconds:00}:{hundredths:00}";
    }
}