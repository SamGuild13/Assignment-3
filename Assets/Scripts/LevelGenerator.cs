using UnityEngine;
using System.Collections.Generic;

public class LevelGenerator : MonoBehaviour
{
    [Header("Level Data (one quadrant - top-left)")]
    public int[,] levelMap = new int[,]
    {
        {1,2,2,2,2,2,2,2,2,2,2,2,2,7},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,4},
        {2,6,4,0,0,4,5,4,0,0,0,4,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,3},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,5},
        {2,5,3,4,4,3,5,3,3,5,3,4,4,4},
        {2,5,3,4,4,3,5,4,4,5,3,4,4,3},
        {2,5,5,5,5,5,5,4,4,5,5,5,5,4},
        {1,2,2,2,2,1,5,4,3,4,4,3,0,4},
        {0,0,0,0,0,2,5,4,3,4,4,3,0,3},
        {0,0,0,0,0,2,5,4,4,0,0,0,0,0},
        {0,0,0,0,0,2,5,4,4,0,3,4,4,8},
        {2,2,2,2,2,1,5,3,3,0,4,0,0,0},
        {0,0,0,0,0,0,5,0,0,0,4,0,0,0},
    };

    [Header("Tile Prefabs")]
    public GameObject outsideCornerPrefab;
    public GameObject outsideWallPrefab;
    public GameObject insideCornerPrefab;
    public GameObject insideWallPrefab;
    public GameObject pelletPrefab;
    public GameObject powerPelletPrefab;
    public GameObject tJunctionPrefab;
    public GameObject ghostDoorPrefab;
    public GameObject floorTilePrefab;

    [Header("Scene References")]
    public GameObject manualLevelRoot;
    public Camera mainCamera;

    [Header("Settings")]
    public float cellSize = 1f;
    public float cameraPadding = 1f;
    public bool spawnFloorTiles = true;

    [Header("Tunnel")]
    public int tunnelRow = 5;

    [Header("Rotation Offsets")]
    public float insideCornerRotationOffset = 0f;
    public float insideWallRotationOffset = 0f;

    private const int EMPTY = 0;
    private const int OUTSIDE_CORNER = 1;
    private const int OUTSIDE_WALL = 2;
    private const int INSIDE_CORNER = 3;
    private const int INSIDE_WALL = 4;
    private const int PELLET = 5;
    private const int POWER_PELLET = 6;
    private const int T_JUNCTION = 7;
    private const int GHOST_DOOR = 8;

    private static readonly HashSet<int> WallTypes = new HashSet<int>
    {
        OUTSIDE_CORNER, OUTSIDE_WALL, INSIDE_CORNER, INSIDE_WALL, T_JUNCTION, GHOST_DOOR
    };

    private HashSet<Vector2Int> floorOnlyCells = new HashSet<Vector2Int>();
    private int[,] fullMap;
    private int fullRows, fullCols;
    private Transform levelParent;

    void Start()
    {
        if (manualLevelRoot != null)
        {
            Destroy(manualLevelRoot);
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        BuildFullMap();
        PunchTunnelOpening();
        FillReachableVoidsWithFloor();
        GenerateLevel();
        FitCamera();
    }

    // levelMap has no tunnel gap encoded in it - this cuts one manually at
    // tunnelRow, clearing the empty run at each edge into open floor.
    private void PunchTunnelOpening()
    {
        if (tunnelRow < 0 || tunnelRow >= fullRows) return;

        for (int c = 0; c < fullCols; c++)
        {
            if (fullMap[tunnelRow, c] != EMPTY) break;
            fullMap[tunnelRow, c] = PELLET;
            floorOnlyCells.Add(new Vector2Int(tunnelRow, c));
        }

        for (int c = fullCols - 1; c >= 0; c--)
        {
            if (fullMap[tunnelRow, c] != EMPTY) break;
            fullMap[tunnelRow, c] = PELLET;
            floorOnlyCells.Add(new Vector2Int(tunnelRow, c));
        }
    }

    // Some EMPTY cells are sealed obstacle interiors (stay black). Others are
    // just corridor space marked 0 that should show floor. Flood fill from
    // every real floor cell through chains of EMPTY neighbours - anything
    // reached gets floor, anything untouched stays a true void.
    private void FillReachableVoidsWithFloor()
    {
        bool[,] visited = new bool[fullRows, fullCols];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int r = 0; r < fullRows; r++)
        {
            for (int c = 0; c < fullCols; c++)
            {
                if (fullMap[r, c] == PELLET || fullMap[r, c] == POWER_PELLET)
                {
                    visited[r, c] = true;
                    queue.Enqueue(new Vector2Int(r, c));
                }
            }
        }

        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nr = cur.x + dr[d];
                int nc = cur.y + dc[d];
                if (nr < 0 || nr >= fullRows || nc < 0 || nc >= fullCols) continue;
                if (visited[nr, nc]) continue;
                if (fullMap[nr, nc] != EMPTY) continue;

                visited[nr, nc] = true;
                queue.Enqueue(new Vector2Int(nr, nc));

                fullMap[nr, nc] = PELLET;
                floorOnlyCells.Add(new Vector2Int(nr, nc));
            }
        }
    }

    // Mirrors the quadrant into the full level. Rows share a middle seam
    // (not duplicated); columns mirror in full (true doubling, no seam).
    private void BuildFullMap()
    {
        int qRows = levelMap.GetLength(0);
        int qCols = levelMap.GetLength(1);

        fullRows = qRows * 2 - 1;
        fullCols = qCols * 2;
        fullMap = new int[fullRows, fullCols];

        for (int r = 0; r < fullRows; r++)
        {
            int srcR = (r < qRows) ? r : (fullRows - 1 - r);

            for (int c = 0; c < fullCols; c++)
            {
                int srcC = (c < qCols) ? c : (fullCols - 1 - c);
                fullMap[r, c] = levelMap[srcR, srcC];
            }
        }
    }

    private void GenerateLevel()
    {
        GameObject parentObj = new GameObject("GeneratedLevel");
        levelParent = parentObj.transform;

        for (int r = 0; r < fullRows; r++)
        {
            for (int c = 0; c < fullCols; c++)
            {
                int value = fullMap[r, c];

                if (value == EMPTY)
                {
                    continue;
                }

                if (spawnFloorTiles && floorTilePrefab != null)
                {
                    SpawnTile(floorTilePrefab, r, c, 0f);
                }

                Vector3 pos = GridToWorld(r, c);
                GameObject prefab = null;
                float rotationZ = 0f;

                switch (value)
                {
                    case OUTSIDE_CORNER:
                        prefab = outsideCornerPrefab;
                        rotationZ = GetCornerRotation(r, c);
                        break;
                    case OUTSIDE_WALL:
                        prefab = outsideWallPrefab;
                        rotationZ = GetStraightWallRotation(r, c);
                        break;
                    case INSIDE_CORNER:
                        prefab = insideCornerPrefab;
                        rotationZ = GetCornerRotation(r, c) + insideCornerRotationOffset;
                        break;
                    case INSIDE_WALL:
                        prefab = insideWallPrefab;
                        rotationZ = GetStraightWallRotation(r, c) + insideWallRotationOffset;
                        break;
                    case PELLET:
                        if (floorOnlyCells.Contains(new Vector2Int(r, c)))
                        {
                            continue;
                        }
                        prefab = pelletPrefab;
                        break;
                    case POWER_PELLET:
                        prefab = powerPelletPrefab;
                        break;
                    case T_JUNCTION:
                        prefab = tJunctionPrefab;
                        rotationZ = GetTJunctionRotation(r, c);
                        break;
                    case GHOST_DOOR:
                        prefab = ghostDoorPrefab;
                        rotationZ = GetStraightWallRotation(r, c);
                        break;
                }

                if (prefab == null)
                {
                    Debug.LogWarning($"No prefab assigned for legend value {value} at ({r},{c})");
                    continue;
                }

                GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0f, 0f, rotationZ), levelParent);
                go.name = $"{prefab.name}_{r}_{c}";
            }
        }
    }

    private void SpawnTile(GameObject prefab, int r, int c, float rotationZ)
    {
        Vector3 pos = GridToWorld(r, c);
        GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0f, 0f, rotationZ), levelParent);

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = -1;
        }
    }

    private Vector3 GridToWorld(int row, int col)
    {
        float x = col * cellSize;
        float y = -row * cellSize;
        return new Vector3(x, y, 0f);
    }

    private bool IsWall(int r, int c)
    {
        if (r < 0 || r >= fullRows || c < 0 || c >= fullCols) return false;
        return WallTypes.Contains(fullMap[r, c]);
    }

    private float GetStraightWallRotation(int r, int c)
    {
        bool up = IsWall(r - 1, c);
        bool down = IsWall(r + 1, c);
        bool left = IsWall(r, c - 1);
        bool right = IsWall(r, c + 1);

        bool verticalThrough = up && down;
        bool horizontalThrough = left && right;

        if (verticalThrough && !horizontalThrough) return 90f;
        if (horizontalThrough && !verticalThrough) return 0f;

        int vCount = (up ? 1 : 0) + (down ? 1 : 0);
        int hCount = (left ? 1 : 0) + (right ? 1 : 0);

        if (vCount > hCount) return 90f;
        return 0f;
    }

    private float GetCornerRotation(int r, int c)
    {
        bool up = IsWall(r - 1, c);
        bool down = IsWall(r + 1, c);
        bool left = IsWall(r, c - 1);
        bool right = IsWall(r, c + 1);

        if (right && down && !up && !left) return 180f;
        if (up && right && !down && !left) return 270f;
        if (left && up && !down && !right) return 0f;
        if (down && left && !up && !right) return 90f;

        // Genuine 4-way meeting rendered with a 2-armed corner sprite - no
        // rotation shows every connection, so pick consistently by quadrant.
        bool leftHalf = c < fullCols / 2f;
        bool topHalf = r < fullRows / 2f;

        if (leftHalf && topHalf) return 180f;
        if (!leftHalf && topHalf) return 270f;
        if (!leftHalf && !topHalf) return 0f;
        return 90f;
    }

    private float GetTJunctionRotation(int r, int c)
    {
        bool up = IsWall(r - 1, c);
        bool down = IsWall(r + 1, c);
        bool left = IsWall(r, c - 1);
        bool right = IsWall(r, c + 1);

        if (!up && left && right && down) return 180f;
        if (!left && up && down && right) return 270f;
        if (!down && left && right && up) return 0f;
        if (!right && up && down && left) return 90f;

        bool leftHalf = c < fullCols / 2f;
        bool topHalf = r < fullRows / 2f;

        if (leftHalf && topHalf) return 180f;
        if (!leftHalf && topHalf) return 270f;
        if (!leftHalf && !topHalf) return 0f;
        return 90f;
    }

    private void FitCamera()
    {
        if (mainCamera == null) return;

        float width = fullCols * cellSize;
        float height = fullRows * cellSize;

        float centerX = (width - cellSize) / 2f;
        float centerY = -(height - cellSize) / 2f;

        mainCamera.transform.position = new Vector3(centerX, centerY, mainCamera.transform.position.z);

        if (mainCamera.orthographic)
        {
            float halfHeight = height / 2f + cameraPadding;
            float halfWidthAsHeight = (width / 2f + cameraPadding) / mainCamera.aspect;
            mainCamera.orthographicSize = Mathf.Max(halfHeight, halfWidthAsHeight);
        }
    }
}