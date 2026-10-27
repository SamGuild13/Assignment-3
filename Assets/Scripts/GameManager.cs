using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private GameObject countdownBlocker;  // also used as the Game Over blocker
    [SerializeField] private Image[] lifeIcons;            // Life1, Life2, Life3
    [SerializeField] private Button exitButton;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip introClip;          // plays during the countdown
    [SerializeField] private AudioClip normalClip;         // loops once the game starts

    [Header("Settings")]
    [SerializeField] private int startingLives = 3;
    [SerializeField] private string levelKey = "Level1";   // PlayerPrefs key prefix (Level2 for the HD scene)
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private float gameOverDuration = 3f;

    [Header("Debug (leave off / 0 when submitting)")]
    [SerializeField] private bool debugKeys = false;       // G = trigger Game Over
    [SerializeField] private int debugPelletsToWin = 0;    // >0 = pretend only this many pellets exist

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public float ElapsedTime { get; private set; }

    private int pelletsRemaining;
    private bool pelletsCounted;

    void Awake()
    {
        Instance = this;
        Score = 0;
        Lives = startingLives;
        ElapsedTime = 0f;

        // Nobody moves until the countdown has finished.
        if (pacStudent != null) pacStudent.InputEnabled = false;

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        if (countdownBlocker != null) countdownBlocker.SetActive(false);

        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.Stop();
        }
    }

    void OnEnable()
    {
        Pickup.Eaten += OnPickupEaten;
    }

    void OnDisable()
    {
        Pickup.Eaten -= OnPickupEaten;
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

            if (debugKeys && Input.GetKeyDown(KeyCode.G)) TriggerGameOver();
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

        CountPellets();
        PlayNormalMusic();

        State = GameState.Playing;
        if (pacStudent != null) pacStudent.InputEnabled = true;
    }

    // ---- Pellet counting (Game Over when every pellet has been eaten) ----

    private void CountPellets()
    {
        pelletsRemaining = 0;
        Pickup[] all = FindObjectsByType<Pickup>(FindObjectsSortMode.None);
        foreach (Pickup p in all)
        {
            if (p.type == Pickup.PickupType.Pellet) pelletsRemaining++;
        }

        if (debugPelletsToWin > 0) pelletsRemaining = debugPelletsToWin;
        pelletsCounted = pelletsRemaining > 0;
    }

    private void OnPickupEaten(Pickup pickup)
    {
        if (State != GameState.Playing || !pelletsCounted) return;
        if (pickup.type != Pickup.PickupType.Pellet) return;

        pelletsRemaining--;
        if (pelletsRemaining <= 0) TriggerGameOver();
    }

    // ---- Game Over ----

    // Every pellet eaten, or PacStudent has no lives left.
    public void TriggerGameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;   // the timer stops here, because it only runs while Playing

        // Stop all player and ghost movement.
        if (pacStudent != null) pacStudent.InputEnabled = false;
        if (GhostManager.Instance != null) GhostManager.Instance.SetFrozen(true);

        // No UI buttons while Game Over is showing.
        if (exitButton != null) exitButton.interactable = false;

        countdownBlocker.SetActive(true);
        gameOverText.gameObject.SetActive(true);

        // Save only if this beats the previous best.
        HighScoreData.TrySave(levelKey, Score, ElapsedTime);

        StartCoroutine(ReturnToStartRoutine());
    }

    private IEnumerator ReturnToStartRoutine()
    {
        yield return new WaitForSeconds(gameOverDuration);
        SceneManager.LoadScene(startSceneName);
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

    private void UpdateTimerUI()
    {
        if (timerText != null) timerText.text = HighScoreData.FormatTime(ElapsedTime);
    }
}