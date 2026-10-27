using System.Collections;
using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    public enum Direction { None, Up, Left, Down, Right }

    [Header("References")]
    [SerializeField] private LevelGenerator level;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource moveAudio;
    [SerializeField] private AudioSource sfxAudio;           // the SFXSource child
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem wallBumpEffect;
    [SerializeField] private ParticleSystem deathEffect;

    [Header("Movement")]
    [SerializeField] private float tilesPerSecond = 5f;

    [Header("Movement Audio")]
    [SerializeField] private AudioClip walkingClip;   // moving, no pellet in the next cell (loops)
    [SerializeField] private AudioClip eatingClip;    // moving onto a pellet (restarts every cell)

    [Header("Sound Effects")]
    [SerializeField] private AudioClip wallBumpClip;  // your "Collision" clip
    [SerializeField] private AudioClip cherryClip;    // leave empty until you have a cherry sound
    [SerializeField] private AudioClip deathClip;     // your "Death" clip

    [Header("Wall Bump")]
    [SerializeField] private int bumpParticleCount = 10;

    [Header("Death")]
    [SerializeField] private string deathState = "Cat_Death";
    [SerializeField] private int deathParticleCount = 25;
    [SerializeField] private float deathDuration = 1.5f;       // last-resort fallback
    [SerializeField] private float deathWaitOverride = 0f;     // 0 = use the animation length; otherwise seconds

    [Header("Scoring")]
    [SerializeField] private int pelletPoints = 10;
    [SerializeField] private int powerPelletPoints = 50;
    [SerializeField] private int cherryPoints = 100;

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
    private bool hasBumped;           // so the bump only happens on the first frame
    private bool isDying;

    // Used by round start / game over / death.
    public bool InputEnabled { get; set; } = true;
    public bool IsLerping => isLerping;
    public bool IsDying => isDying;
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

        // The death sequence owns the animator and audio until it finishes.
        if (isDying) return;

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

                // Reached a grid position: tunnel ends teleport, then decide the next move.
                TryTeleport();

                if (TryStartMove())
                {
                    elapsed = leftover;
                    float t2 = Mathf.Clamp01(elapsed / duration);
                    transform.position = Vector3.Lerp(startPos, targetPos, t2);
                }
                else
                {
                    HandleWallBump();
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
        hasBumped = false;

        SetFacing(Direction.Right);   // spec: start facing right, waiting for input
        SetMovingEffects(false);
    }

    // Called by GhostManager when PacStudent touches a Normal-state ghost.
    public void Die()
    {
        if (isDying) return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Playing) return;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        isDying = true;

        // Stop moving, stop the footsteps and dust, freeze the ghosts.
        isLerping = false;
        SetMovingEffects(false);
        if (GhostManager.Instance != null) GhostManager.Instance.SetFrozen(true);

        // Lose a life and update the HUD.
        GameManager.Instance.SetLives(GameManager.Instance.Lives - 1);

        // Death animation, particles and sound all start together.
        if (animator != null)
        {
            int hash = Animator.StringToHash(deathState);
            if (animator.HasState(0, hash))
            {
                animator.speed = 1f;
                animator.Play(hash, 0, 0f);
            }
            else
            {
                Debug.LogWarning($"PacStudent: animator has no state called '{deathState}'.", this);
            }
        }

        if (deathEffect != null)
        {
            deathEffect.transform.position = new Vector3(transform.position.x, transform.position.y, deathEffect.transform.position.z);
            deathEffect.Emit(deathParticleCount);
        }

        PlaySfx(deathClip);

        yield return new WaitForSeconds(GetDeathWait());

        // No lives left: hand over to Game Over (screen added in part 5).
        if (GameManager.Instance.Lives <= 0)
        {
            GameManager.Instance.TriggerGameOver();
            yield break;
        }

        // Respawn top-left, ghosts back to their start in the Normal state, wait for new input.
        ResetToStart();
        if (GhostManager.Instance != null)
        {
            GhostManager.Instance.ResetAllGhosts();
            GhostManager.Instance.SetFrozen(false);
        }

        isDying = false;
    }

    // How long the death sequence lasts: manual override, else the animation's own length,
    // else the sound's length, else a fixed fallback.
    private float GetDeathWait()
    {
        if (deathWaitOverride > 0f) return deathWaitOverride;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == deathState) return clip.length;
            }
        }

        return deathClip != null ? deathClip.length : deathDuration;
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
        hasBumped = false;

        SetFacing(dir);
        PlayMoveAudio(level.HasPickup(targetCell));
    }

    // Tunnel ends: arriving heading outwards moves PacStudent to the other side,
    // and the same direction carries on, which is inwards from that end.
    private void TryTeleport()
    {
        int colDirection = ToOffset(currentInput).y;
        if (colDirection == 0) return;

        if (level.TryGetTeleport(currentCell, colDirection, out Vector2Int destination))
        {
            currentCell = destination;
            Vector3 p = level.CellToWorld(currentCell);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }

    // First frame of hitting a wall: particle burst at the contact point + sound.
    private void HandleWallBump()
    {
        if (hasBumped || currentInput == Direction.None) return;
        hasBumped = true;

        if (wallBumpEffect != null)
        {
            Vector2Int off = ToOffset(currentInput);
            Vector3 contact = level.CellToWorld(currentCell) +
                              new Vector3(off.y, -off.x, 0f) * (level.cellSize * 0.5f);

            wallBumpEffect.transform.position = new Vector3(contact.x, contact.y, wallBumpEffect.transform.position.z);
            wallBumpEffect.Emit(bumpParticleCount);
        }

        PlaySfx(wallBumpClip);
    }

    // Pickups and ghosts are trigger colliders (PacStudent has a kinematic Rigidbody2D).
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Nothing registers during the countdown, the death sequence or after the game is over.
        if (isDying) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;

        Pickup pickup = other.GetComponent<Pickup>();
        if (pickup != null)
        {
            switch (pickup.type)
            {
                case Pickup.PickupType.Pellet:
                    AddScore(pelletPoints);
                    break;

                case Pickup.PickupType.PowerPellet:
                    AddScore(powerPelletPoints);
                    if (GhostManager.Instance != null) GhostManager.Instance.StartScared();
                    break;

                case Pickup.PickupType.Cherry:
                    AddScore(cherryPoints);
                    PlaySfx(cherryClip);
                    break;
            }

            Destroy(other.gameObject);
            return;
        }

        GhostController ghost = other.GetComponent<GhostController>();
        if (ghost != null && GhostManager.Instance != null)
        {
            GhostManager.Instance.OnGhostTouched(ghost);
        }
    }

    private void AddScore(int points)
    {
        if (GameManager.Instance != null) GameManager.Instance.AddScore(points);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxAudio != null && clip != null) sfxAudio.PlayOneShot(clip);
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