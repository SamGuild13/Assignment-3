using TMPro;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GhostManager : MonoBehaviour
{
    public static GhostManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PacStudentController pacStudent;
    [SerializeField] private GhostController[] ghosts;
    [SerializeField] private TMP_Text ghostTimerText;

    [Header("Music")]
    [SerializeField] private AudioClip scaredMusic;   // Scared_state
    [SerializeField] private AudioClip deadMusic;     // Killed_state

    [Header("Audio")]
    [SerializeField] private AudioClip ghostEatClip;  // Dog_eat...

    [Header("Rules")]
    [SerializeField] private float scaredDuration = 10f;
    [SerializeField] private float recoveringThreshold = 3f;
    [SerializeField] private float deadDuration = 3f;
    [SerializeField] private int ghostPoints = 300;

    [Header("Debug (leave unticked when submitting)")]
    [SerializeField] private bool debugKeys = false;

    private AudioSource sfx;
    private GhostController.GhostState globalState = GhostController.GhostState.Normal;
    private float scaredTimer;

    // True during the PacStudent death sequence. Ghost movement (85% band) checks this.
    public bool Frozen { get; private set; }

    void Awake()
    {
        Instance = this;
        sfx = GetComponent<AudioSource>();
        sfx.playOnAwake = false;

        if (ghostTimerText != null) ghostTimerText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing) return;
        if (Frozen) return;

        if (debugKeys)
        {
            for (int i = 0; i < ghosts.Length && i < 4; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) OnGhostTouched(ghosts[i]);
            }
        }

        // Power pill timer.
        if (globalState != GhostController.GhostState.Normal)
        {
            scaredTimer -= Time.deltaTime;

            if (scaredTimer <= 0f)
            {
                EndScared();
            }
            else
            {
                if (ghostTimerText != null) ghostTimerText.text = Mathf.CeilToInt(scaredTimer).ToString();

                if (globalState == GhostController.GhostState.Scared && scaredTimer <= recoveringThreshold)
                {
                    globalState = GhostController.GhostState.Recovering;
                    SetLivingGhosts(GhostController.GhostState.Recovering);
                }
            }
        }

        // Dead ghosts come back after their own 3 second timer.
        foreach (GhostController ghost in ghosts)
        {
            if (ghost.State != GhostController.GhostState.Dead) continue;

            ghost.DeadTimer -= Time.deltaTime;
            if (ghost.DeadTimer <= 0f)
            {
                // Normal if the scared timer has finished, otherwise Scared or Recovering.
                ghost.SetState(globalState);
                UpdateMusic();
            }
        }
    }

    // Called when PacStudent eats a power pill.
    public void StartScared()
    {
        globalState = GhostController.GhostState.Scared;
        scaredTimer = scaredDuration;

        if (ghostTimerText != null)
        {
            ghostTimerText.gameObject.SetActive(true);
            ghostTimerText.text = Mathf.CeilToInt(scaredTimer).ToString();
        }

        SetLivingGhosts(GhostController.GhostState.Scared);
        UpdateMusic();
    }

    // Called when PacStudent touches a ghost.
    public void OnGhostTouched(GhostController ghost)
    {
        switch (ghost.State)
        {
            case GhostController.GhostState.Scared:
            case GhostController.GhostState.Recovering:
                EatGhost(ghost);
                break;

            case GhostController.GhostState.Normal:
                if (pacStudent != null) pacStudent.Die();
                break;

                // Dead ghosts ignore PacStudent.
        }
    }

    public void SetFrozen(bool frozen)
    {
        Frozen = frozen;
    }

    // After PacStudent dies: every ghost back to its start position in the Normal state.
    public void ResetAllGhosts()
    {
        globalState = GhostController.GhostState.Normal;
        scaredTimer = 0f;

        if (ghostTimerText != null) ghostTimerText.gameObject.SetActive(false);

        foreach (GhostController ghost in ghosts)
        {
            ghost.ResetToStart();
        }

        UpdateMusic();
    }

    private void EatGhost(GhostController ghost)
    {
        ghost.SetState(GhostController.GhostState.Dead);
        ghost.DeadTimer = deadDuration;

        if (GameManager.Instance != null) GameManager.Instance.AddScore(ghostPoints);
        if (ghostEatClip != null) sfx.PlayOneShot(ghostEatClip);

        UpdateMusic();
    }

    private void EndScared()
    {
        globalState = GhostController.GhostState.Normal;
        scaredTimer = 0f;

        if (ghostTimerText != null) ghostTimerText.gameObject.SetActive(false);

        // A Dead ghost must not be changed back to Normal.
        SetLivingGhosts(GhostController.GhostState.Normal);
        UpdateMusic();
    }

    private void SetLivingGhosts(GhostController.GhostState state)
    {
        foreach (GhostController ghost in ghosts)
        {
            if (ghost.State != GhostController.GhostState.Dead) ghost.SetState(state);
        }
    }

    private bool AnyGhostDead()
    {
        foreach (GhostController ghost in ghosts)
        {
            if (ghost.State == GhostController.GhostState.Dead) return true;
        }
        return false;
    }

    private void UpdateMusic()
    {
        if (GameManager.Instance == null) return;

        if (AnyGhostDead()) GameManager.Instance.PlayMusic(deadMusic, true);
        else if (globalState != GhostController.GhostState.Normal) GameManager.Instance.PlayMusic(scaredMusic, true);
        else GameManager.Instance.PlayNormalMusic();
    }
}