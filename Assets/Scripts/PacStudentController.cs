using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    public enum Direction { None, Up, Left, Down, Right }

    [Header("References")]
    [SerializeField] private LevelGenerator level;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource moveAudio;
    [SerializeField] private ParticleSystem dust;

    [Header("Movement")]
    [SerializeField] private float tilesPerSecond = 5f;

    [Header("Movement Audio")]
    [SerializeField] private AudioClip walkingClip;   // moving, no pellet in the next cell (loops)
    [SerializeField] private AudioClip eatingClip;    // moving onto a pellet (restarts every cell)

    [Header("Animator State Names (must match the Animator window exactly)")]
    [SerializeField] private string upState = "Up";
    [SerializeField] private string downState = "Down";
    [SerializeField] private string leftState = "Left";
    [SerializeField] private string rightState = "Right";

    // Required by the spec: names and behaviour matter.
    private Direction lastInput = Direction.None;     // last key the player pressed (never cleared by us)
    private Direction currentInput = Direction.None;  // last direction we actually moved in

    private Direction facing = Direction.None;
    private Vector2Int currentCell;   // (row, col)
    private Vector2Int targetCell;
    private Vector3 startPos;
    private Vector3 targetPos;
    private float elapsed;
    private float duration;
    private bool isLerping;
    private bool initialised;

    // Used later by round start / game over / death.
    public bool InputEnabled { get; set; } = true;
    public bool IsLerping => isLerping;
    public Vector2Int CurrentCell => currentCell;
    public Direction CurrentDirection => currentInput;

    void Awake()
    {
        duration = 1f / tilesPerSecond;

        if (animator == null) animator = GetComponent<Animator>();
        if (moveAudio == null) moveAudio = GetComponent<AudioSource>();
        if (dust == null) dust = GetComponentInChildren<ParticleSystem>();

        if (moveAudio != null)
        {
            moveAudio.playOnAwake = false;
            moveAudio.Stop();
        }

        if (animator != null) animator.speed = 0f;
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
        if (level == null || !level.IsReady) return;

        if (!initialised)
        {
            ResetToStart();
            initialised = true;
        }

        if (!InputEnabled)
        {
            SetMovingEffects(false);
            return;
        }

        ReadInput();

        if (isLerping)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);

            if (elapsed >= duration)
            {
                float leftover = elapsed - duration;
                transform.position = targetPos;
                currentCell = targetCell;
                isLerping = false;

                // Reached a grid position: decide the next move straight away.
                if (TryStartMove())
                {
                    elapsed = leftover;
                    float t2 = Mathf.Clamp01(elapsed / duration);
                    transform.position = Vector3.Lerp(startPos, targetPos, t2);
                }
            }
        }
        else
        {
            TryStartMove();
        }

        SetMovingEffects(isLerping);
    }

    public void ResetToStart()
    {
        currentCell = level.PlayerStartCell;
        Vector3 p = level.CellToWorld(currentCell);
        transform.position = new Vector3(p.x, p.y, transform.position.z);

        isLerping = false;
        elapsed = 0f;
        lastInput = Direction.None;
        currentInput = Direction.None;
        facing = Direction.None;

        SetFacing(Direction.Right);   // spec: start facing right, waiting for input
        SetMovingEffects(false);
    }

    private void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.W)) lastInput = Direction.Up;
        else if (Input.GetKeyDown(KeyCode.A)) lastInput = Direction.Left;
        else if (Input.GetKeyDown(KeyCode.S)) lastInput = Direction.Down;
        else if (Input.GetKeyDown(KeyCode.D)) lastInput = Direction.Right;
    }

    private bool TryStartMove()
    {
        // 1) Try lastInput first.
        if (lastInput != Direction.None && CanMove(lastInput))
        {
            currentInput = lastInput;
            BeginMove(currentInput);
            return true;
        }

        // 2) lastInput blocked: keep going the way we were going.
        if (currentInput != Direction.None && CanMove(currentInput))
        {
            BeginMove(currentInput);
            return true;
        }

        // 3) Both blocked: stop and wait for new input.
        return false;
    }

    private bool CanMove(Direction dir)
    {
        return level.IsWalkable(currentCell + ToOffset(dir));
    }

    private void BeginMove(Direction dir)
    {
        targetCell = currentCell + ToOffset(dir);
        startPos = transform.position;
        Vector3 p = level.CellToWorld(targetCell);
        targetPos = new Vector3(p.x, p.y, transform.position.z);
        elapsed = 0f;
        isLerping = true;

        SetFacing(dir);
        PlayMoveAudio(level.HasPickup(targetCell));
    }

    // Plays the matching directional animation state, keeping the walk-cycle
    // position so turning doesn't make the animation jump back to frame 0.
    private void SetFacing(Direction dir)
    {
        if (dir == facing || animator == null) return;
        facing = dir;

        float normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
        animator.Play(StateNameFor(dir), 0, normalizedTime);
    }

    private string StateNameFor(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return upState;
            case Direction.Down: return downState;
            case Direction.Left: return leftState;
            default: return rightState;
        }
    }

    private void SetMovingEffects(bool moving)
    {
        if (animator != null) animator.speed = moving ? 1f : 0f;

        if (!moving && moveAudio != null && moveAudio.isPlaying)
        {
            moveAudio.Stop();
        }

        if (dust != null)
        {
            if (moving && !dust.isEmitting)
            {
                dust.Play();
            }
            else if (!moving && dust.isEmitting)
            {
                // Stop making new dust but let the existing specks fade out.
                dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    // Pellet ahead: restart the eating clip from the start (one sound per pellet).
    // No pellet: loop the walking clip, only (re)starting it if it isn't already playing.
    private void PlayMoveAudio(bool pelletAhead)
    {
        if (moveAudio == null) return;

        if (pelletAhead)
        {
            if (eatingClip == null) return;
            moveAudio.loop = false;
            moveAudio.clip = eatingClip;
            moveAudio.Play();
        }
        else
        {
            if (walkingClip == null) return;
            if (moveAudio.clip != walkingClip || !moveAudio.isPlaying)
            {
                moveAudio.loop = true;
                moveAudio.clip = walkingClip;
                moveAudio.Play();
            }
        }
    }

    // (row, col): up is row - 1, right is col + 1
    private static Vector2Int ToOffset(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return new Vector2Int(-1, 0);
            case Direction.Down: return new Vector2Int(1, 0);
            case Direction.Left: return new Vector2Int(0, -1);
            case Direction.Right: return new Vector2Int(0, 1);
            default: return Vector2Int.zero;
        }
    }
}