using UnityEngine;

public class GhostController : MonoBehaviour
{
    public enum GhostState { Normal, Scared, Recovering, Dead }
    public enum Facing { Down, Left, Right, Up }

    [Header("Animator state names")]
    [SerializeField] private string statePrefix = "Dog1";     // Dog1_Down, Dog1_Left ...
    [SerializeField] private string scaredPrefix = "Scared";  // Scared_Down ...

    [Header("Optional overrides (type the EXACT state name if yours differs)")]
    [SerializeField] private string recoveringStateOverride = "";   // default: <prefix>_Recovering
    [SerializeField] private string deathStateOverride = "";        // default: <prefix>_Death

    private Animator animator;
    private Vector3 startPosition;

    public GhostState State { get; private set; } = GhostState.Normal;
    public Facing CurrentFacing { get; private set; } = Facing.Down;
    public float DeadTimer { get; set; }

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        startPosition = transform.position;
    }

    void Start()
    {
        PlayAnimation();
    }

    public void SetState(GhostState newState)
    {
        State = newState;
        PlayAnimation();
    }

    // Used when PacStudent dies (next part).
    public void ResetToStart()
    {
        transform.position = startPosition;
        CurrentFacing = Facing.Down;
        DeadTimer = 0f;
        SetState(GhostState.Normal);
    }

    private void PlayAnimation()
    {
        if (animator == null) return;

        string stateName;
        switch (State)
        {
            case GhostState.Scared:
                stateName = scaredPrefix + "_" + CurrentFacing;
                break;
            case GhostState.Recovering:
                stateName = string.IsNullOrEmpty(recoveringStateOverride)
                    ? statePrefix + "_Recovering" : recoveringStateOverride;
                break;
            case GhostState.Dead:
                stateName = string.IsNullOrEmpty(deathStateOverride)
                    ? statePrefix + "_Death" : deathStateOverride;
                break;
            default:
                stateName = statePrefix + "_" + CurrentFacing;
                break;
        }

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash))
        {
            Debug.LogWarning($"{name}: animator has no state called '{stateName}'. " +
                             $"Compare it with the state names in the Animator window.", this);
            return;
        }

        animator.Play(hash, 0, 0f);
    }
}