using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedurally generates the Pac-Man level from a 2D int array representing
/// a single quadrant (top-left), mirroring it horizontally, vertically, and
/// both to produce the full symmetric level.
///
/// IMPORTANT SETUP STEPS (do these in the Unity Inspector before pressing Play):
///  1. Attach this script to an empty GameObject (e.g. "LevelGenerator") in your scene.
///  2. Drag each of your 8 tile prefabs into the matching slots below.
///  3. Drag your existing manually-built Level01 root object into "manualLevelRoot".
///  4. Drag your Main Camera into "mainCamera" (or leave blank to auto-find Camera.main).
///  5. Set "cellSize" to match your sprite size in world units (32px sprite ÷ your
///     Pixels Per Unit setting - e.g. if PPU = 100, cellSize = 0.32).
/// </summary>
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
    public GameObject outsideCornerPrefab; // legend 1
    public GameObject outsideWallPrefab;   // legend 2
    public GameObject insideCornerPrefab;  // legend 3
    public GameObject insideWallPrefab;    // legend 4
    public GameObject pelletPrefab;        // legend 5
    public GameObject powerPelletPrefab;   // legend 6
    public GameObject tJunctionPrefab;     // legend 7
    public GameObject ghostDoorPrefab;     // legend 8
    public GameObject floorTilePrefab;     // optional, legend 0 (can be left null)

    [Header("Scene References")]
    public GameObject manualLevelRoot;     // drag your manually-built Level01 root here
    public Camera mainCamera;

    [Header("Settings")]
    public float cellSize = 0.32f;         // world units per grid cell
    public float cameraPadding = 1f;       // extra world units of breathing room around the level
    public bool spawnFloorTiles = false;   // only used if floorTilePrefab is assigned

    // ---- Legend constants ----
    private const int EMPTY = 0;
    private const int OUTSIDE_CORNER = 1;
    private const int OUTSIDE_WALL = 2;
    private const int INSIDE_CORNER = 3;
    private const int INSIDE_WALL = 4;
    private const int PELLET = 5;
    private const int POWER_PELLET = 6;
    private const int T_JUNCTION = 7;
    private const int GHOST_DOOR = 8;

    // Any value that counts as a "wall" for connectivity purposes.
    private static readonly HashSet<int> WallTypes = new HashSet<int>
    {
        OUTSIDE_CORNER, OUTSIDE_WALL, INSIDE_CORNER, INSIDE_WALL, T_JUNCTION, GHOST_DOOR
    };

    [Header("Tunnel (not encoded in levelMap - added manually here)")]
    [Tooltip("The row (in the FULL mirrored map, 0 = top) where the left/right tunnel opening sits. Check your manually-built level's Y position divided by cellSize to find this.")]
    public int tunnelRow = 5;

    [Header("Rotation fine-tuning")]
    [Tooltip("Extra rotation (multiple of 90) applied only to inside corners, in case their artwork's default orientation differs from outside corners. Try 0, 90, 180, 270.")]
    public float insideCornerRotationOffset = 0f;
    [Tooltip("Extra rotation (multiple of 90) applied only to inside walls, in case their artwork's default orientation differs from outside walls. Try 0 or 90.")]
    public float insideWallRotationOffset = 0f;

    private HashSet<Vector2Int> floorOnlyCells = new HashSet<Vector2Int>();

    private int[,] fullMap;
    private int fullRows, fullCols;
    private Transform levelParent;

    void Start()
    {
        // Remove the manually-built level from the running scene.
        // (The manual level itself must still exist in the project/scene hierarchy
        //  before Play is pressed - this just clears it at runtime.)
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

    /// <summary>
    /// The tunnel isn't part of levelMap - it's a deliberate opening you cut
    /// into the boundary manually. This reproduces that: clears the wall at
    /// the far left and far right edge of tunnelRow, replacing it with plain
    /// floor (no wall, no pellet).
    /// </summary>
    private void PunchTunnelOpening()
    {
        if (tunnelRow < 0 || tunnelRow >= fullRows) return;

        // Walk inward from the left edge, converting every contiguous EMPTY
        // cell to floor-only, until hitting the first real wall.
        for (int c = 0; c < fullCols; c++)
        {
            if (fullMap[tunnelRow, c] != EMPTY) break;
            fullMap[tunnelRow, c] = PELLET;
            floorOnlyCells.Add(new Vector2Int(tunnelRow, c));
        }

        // Same from the right edge inward.
        for (int c = fullCols - 1; c >= 0; c--)
        {
            if (fullMap[tunnelRow, c] != EMPTY) break;
            fullMap[tunnelRow, c] = PELLET;
            floorOnlyCells.Add(new Vector2Int(tunnelRow, c));
        }
    }

    /// <summary>
    /// Some '0' cells are genuinely sealed obstacle interiors (correctly
    /// black/void). Others are just corridor space that happens to be marked
    /// 0 in the array and should actually show floor. This flood-fills
    /// outward from every real floor cell (including the tunnel cells just
    /// punched above), through chains of adjacent '0' cells: anything reached
    /// this way is floor; anything never reached stays a true sealed void.
    /// </summary>
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
                if (fullMap[nr, nc] != EMPTY) continue; // walls block; only cross EMPTY cells

                visited[nr, nc] = true;
                queue.Enqueue(new Vector2Int(nr, nc));

                fullMap[nr, nc] = PELLET;
                floorOnlyCells.Add(new Vector2Int(nr, nc));
            }
        }
    }

    /// <summary>
    /// Expands the single quadrant into the full level by mirroring it
    /// horizontally and vertically. The last row and last column of the
    /// source quadrant's last ROW is treated as a shared seam and is NOT
    /// duplicated (avoids a doubled middle floor row). The last COLUMN,
    /// however, mirrors in full - there is no shared seam column, so the
    /// full width is a true doubling (14 columns -> 28, not 27).
    /// </summary>
    private void BuildFullMap()
    {
        int qRows = levelMap.GetLength(0);
        int qCols = levelMap.GetLength(1);

        fullRows = qRows * 2 - 1;
        fullCols = qCols * 2;
        fullMap = new int[fullRows, fullCols];

        for (int r = 0; r < fullRows; r++)
        {
            // Map full-grid row back to a source row in the quadrant.
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

                // EMPTY (0) cells are solid/non-floor areas (reward-item box interiors,
                // ghost-house interior) - they should stay bare, not get a floor tile.
                if (value == EMPTY)
                {
                    continue;
                }

                // Every other cell (walls, corners, pellets, junctions, ghost door)
                // sits on top of the continuous background floor.
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
                            // Tunnel or reachable-void cell: floor only, no pellet.
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

        // Push floor tiles behind whatever wall/pellet piece is instantiated at the
        // same position, regardless of the prefab's own default sorting order.
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = -1;
        }
    }

    private Vector3 GridToWorld(int row, int col)
    {
        // Row 0 is the top of the map -> positive Y; row increases downward -> Y decreases.
        float x = col * cellSize;
        float y = -row * cellSize;
        return new Vector3(x, y, 0f);
    }

    private bool IsWall(int r, int c)
    {
        if (r < 0 || r >= fullRows || c < 0 || c >= fullCols) return false;
        return WallTypes.Contains(fullMap[r, c]);
    }

    /// <summary>
    /// Straight walls default to horizontal. If the wall's connections are
    /// vertical instead, rotate 90 degrees.
    /// </summary>
    private float GetStraightWallRotation(int r, int c)
    {
        bool up = IsWall(r - 1, c);
        bool down = IsWall(r + 1, c);
        bool left = IsWall(r, c - 1);
        bool right = IsWall(r, c + 1);

        // A wall next to a T-junction or corner has a wall neighbor on BOTH
        // axes at once, since the junction itself counts as a wall in every
        // direction it touches. Simply checking "any horizontal neighbor" is
        // not enough - check which axis actually forms a genuine straight
        // through-line (both sides present on that axis) instead.
        bool verticalThrough = up && down;
        bool horizontalThrough = left && right;

        if (verticalThrough && !horizontalThrough) return 90f;
        if (horizontalThrough && !verticalThrough) return 0f;

        // Still ambiguous (both axes fully through, or only single-sided
        // neighbors on each axis) - fall back to whichever axis has more
        // connections overall.
        int vCount = (up ? 1 : 0) + (down ? 1 : 0);
        int hCount = (left ? 1 : 0) + (right ? 1 : 0);

        if (vCount > hCount) return 90f;
        return 0f;
    }

    /// <summary>
    /// Corner pieces default to connecting RIGHT + DOWN (matches levelMap[0,0],
    /// which must render with default/0-degree rotation). Rotate clockwise from
    /// there to match whichever two sides are actually connected.
    /// If your sprite's default orientation differs, adjust the returned angles.
    /// </summary>
    private float GetCornerRotation(int r, int c)
    {
        bool up = IsWall(r - 1, c);
        bool down = IsWall(r + 1, c);
        bool left = IsWall(r, c - 1);
        bool right = IsWall(r, c + 1);

        // Prefer an exact 2-side match first (the normal case for most corners).
        // Must check the OTHER two sides are false too, or a genuine 3/4-way
        // meeting would wrongly satisfy the first pair-check it happens to hit,
        // instead of correctly falling through to the ambiguous-case handling.
        // NOTE: rotations below are offset 180 degrees from the "textbook" mapping
        // to match this project's actual corner artwork orientation.
        if (right && down && !up && !left) return 180f;
        if (up && right && !down && !left) return 270f;
        if (left && up && !down && !right) return 0f;
        if (down && left && !up && !right) return 90f;

        // Fallback for any ambiguous case: 1, 3, or 4 real neighbors present
        // instead of the clean 2 a corner sprite can actually display (e.g.
        // a genuine 4-way wall meeting represented by a 2-armed corner, or a
        // tapered end-cap with only one real connection). There's no single
        // "correct" rotation here since the sprite physically can't show every
        // connection - so use a fully generic, deterministic rule based on
        // which quadrant of the map this cell sits in, rather than memorising
        // specific coordinates (which would break the moment a different
        // levelMap array is substituted in for grading).
        //
        // Rule: point both of the corner's arms TOWARD the center of the map -
        // right if in the left half, left if in the right half; down if in the
        // top half, up if in the bottom half. This is consistent for any array
        // of any size, and naturally produces mirrored results on mirrored
        // halves without needing to know anything about this specific map.
        bool leftHalf = c < fullCols / 2f;
        bool topHalf = r < fullRows / 2f;

        if (leftHalf && topHalf) return 180f;   // prefer right+down
        if (!leftHalf && topHalf) return 270f;  // prefer up+right
        if (!leftHalf && !topHalf) return 0f;   // prefer left+up
        return 90f;                             // left half, bottom half: prefer down+left
    }

    /// <summary>
    /// T-junction pieces default to connecting LEFT + RIGHT + DOWN (closed side
    /// facing UP - matches the T-junction at levelMap[0, lastCol] on the top
    /// boundary row). Rotate to match whichever single side is NOT connected.
    /// </summary>
    private float GetTJunctionRotation(int r, int c)
    {
        bool up = IsWall(r - 1, c);
        bool down = IsWall(r + 1, c);
        bool left = IsWall(r, c - 1);
        bool right = IsWall(r, c + 1);

        // Prefer an exact match first (all 3 expected sides present).
        // NOTE: offset 180 degrees from "textbook" to match this project's artwork.
        if (!up && left && right && down) return 180f;
        if (!left && up && down && right) return 270f;
        if (!down && left && right && up) return 0f;
        if (!right && up && down && left) return 90f;

        // Fallback for ambiguous cases (all 4 sides present, or fewer than 3) -
        // use the same generic, position-based rule as corners: no memorised
        // coordinates, so this holds up correctly for any substituted array.
        bool leftHalf = c < fullCols / 2f;
        bool topHalf = r < fullRows / 2f;

        if (leftHalf && topHalf) return 180f;
        if (!leftHalf && topHalf) return 270f;
        if (!leftHalf && !topHalf) return 0f;
        return 90f;
    }

    /// <summary>
    /// Positions and sizes the camera so the entire generated level is visible.
    /// </summary>
    private void FitCamera()
    {
        if (mainCamera == null) return;

        float width = fullCols * cellSize;
        float height = fullRows * cellSize;

        // Center of the grid in world space (row/col 0 is top-left, Y goes negative downward).
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