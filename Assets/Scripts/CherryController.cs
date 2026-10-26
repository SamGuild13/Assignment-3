using System.Collections;
using UnityEngine;

public class CherryController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelGenerator level;
    [SerializeField] private GameObject cherryPrefab;
    [SerializeField] private SpriteMask levelMask;

    [Header("Settings")]
    [SerializeField] private float spawnDelay = 5f;      // seconds before each cherry
    [SerializeField] private float cherrySpeed = 4f;     // world units per second
    [SerializeField] private float spawnMargin = 1.5f;   // how far outside the map it starts
    [SerializeField] private int sortingOrder = 100;     // drawn over everything

    private GameObject activeCherry;

    // Level geometry, worked out from LevelGenerator's public data.
    private Vector2 levelCenter;
    private Vector2 levelHalfSize;

    void Start()
    {
        StartCoroutine(CherryRoutine());
    }

    private IEnumerator CherryRoutine()
    {
        while (level == null || !level.IsReady) yield return null;

        CalculateLevelBounds();
        SetupMask();

        while (true)
        {
            // 5 seconds after the scene starts, and 5 seconds after the previous cherry is gone.
            yield return new WaitForSeconds(spawnDelay);

            SpawnCherry(out Vector3 start, out Vector3 end);
            float duration = Vector3.Distance(start, end) / cherrySpeed;
            float elapsed = 0f;

            // Linear lerp, frame-rate independent. Stops early if the cherry gets eaten (Phase 5).
            while (activeCherry != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                activeCherry.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            // Reached the far side: destroy it.
            if (activeCherry != null)
            {
                Destroy(activeCherry);
            }
            activeCherry = null;
        }
    }

    private void CalculateLevelBounds()
    {
        int fullRows = level.levelMap.GetLength(0) * 2 - 1;
        int fullCols = level.levelMap.GetLength(1) * 2;
        float cell = level.cellSize;

        // Cells sit at x = col * cell, y = -row * cell, so the middle is half the last index.
        levelCenter = new Vector2((fullCols - 1) * cell / 2f, -(fullRows - 1) * cell / 2f);
        levelHalfSize = new Vector2(fullCols * cell / 2f, fullRows * cell / 2f);
    }

    private void SetupMask()
    {
        if (levelMask == null || levelMask.sprite == null) return;

        levelMask.transform.position = new Vector3(levelCenter.x, levelCenter.y, 0f);

        Vector2 spriteSize = levelMask.sprite.bounds.size;
        levelMask.transform.localScale = new Vector3(
            levelHalfSize.x * 2f / spriteSize.x,
            levelHalfSize.y * 2f / spriteSize.y,
            1f);
    }

    private void SpawnCherry(out Vector3 start, out Vector3 end)
    {
        // Random direction; start and end sit on opposite sides of the centre,
        // so the path always passes straight through the middle of the level.
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        float tx = Mathf.Abs(dir.x) > 0.0001f ? levelHalfSize.x / Mathf.Abs(dir.x) : float.MaxValue;
        float ty = Mathf.Abs(dir.y) > 0.0001f ? levelHalfSize.y / Mathf.Abs(dir.y) : float.MaxValue;
        float distanceToEdge = Mathf.Min(tx, ty);
        float radius = distanceToEdge + spawnMargin;

        start = levelCenter + dir * radius;
        end = levelCenter - dir * radius;

        activeCherry = Instantiate(cherryPrefab, start, Quaternion.identity);

        // Only visible while inside the level (the sprite mask), and drawn over everything else.
        SpriteRenderer[] renderers = activeCherry.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            sr.sortingOrder = sortingOrder;
        }
    }
}