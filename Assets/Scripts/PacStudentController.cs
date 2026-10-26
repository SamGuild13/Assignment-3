using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    public enum Direction { None, Up, Left, Down, Right }

    [SerializeField] private LevelGenerator level;
    [SerializeField] private float tilesPerSecond = 5f;

    // Required by the spec: names and behaviour matter.
    private Direction lastInput = Direction.None;     // last key the player pressed (never cleared by us)
    private Direction currentInput = Direction.None;  // last direction we actually moved in

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
    }

    void Update()
    {
        if (level == null || !level.IsReady) return;

        if (!initialised)
        {
            ResetToStart();
            initialised = true;
        }

        if (!InputEnabled) return;

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