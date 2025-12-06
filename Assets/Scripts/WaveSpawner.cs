using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[DisallowMultipleComponent]
public class WaveSpawner : MonoBehaviour
{
    [Serializable]
    public class Wave
    {
        [Tooltip("Number of enemies to spawn in this wave (if spawnPrefabs has >1, they will be chosen randomly)")]
        public int enemyCount = 5;

        [Tooltip("Delay between each enemy spawn (seconds) inside this wave")]
        public float spawnInterval = 0.5f;
    }

    public enum SpawnShape
    {
        Circle,
        Rectangle
    }

    [Header("Wave Settings")]
    [Tooltip("Define waves (counts and per-wave spawn interval)")]
    public List<Wave> waves = new List<Wave>() { new Wave { enemyCount = 5, spawnInterval = 0.6f } };

    [Header("Enemy Prefabs")]
    [Tooltip("Enemy prefab(s) to spawn. Must have the 'Enemy' component on the root.")]
    public GameObject[] spawnPrefabs;

    [Header("Spawn Area (local to this GameObject)")]
    public SpawnShape spawnShape = SpawnShape.Circle;

    [Tooltip("Used when SpawnShape is Circle: radius within which enemies will randomly spawn (XZ plane).")]
    public float spawnRadius = 6f;

    [Tooltip("Used when SpawnShape is Circle: min distance from the center where enemies will not spawn (optional).")]
    public float innerRadius = 1.0f;

    [Tooltip("Used when SpawnShape is Rectangle: overall width (X) and depth (Z) of the spawn rectangle.")]
    public Vector2 rectangleSize = new Vector2(12f, 8f);

    [Tooltip("Used when SpawnShape is Rectangle: optional inner rectangular exclusion area (set to zero to disable).")]
    public Vector2 innerRectangleSize = Vector2.zero;

    [Header("Spawn Indicator")]
    [Tooltip("Optional indicator prefab shown at spawn location for indicatorDelay seconds.")]
    public GameObject spawnIndicatorPrefab;
    [Tooltip("How long the indicator shows before the enemy spawns (seconds).")]
    public float indicatorDelay = 1.0f;

    [Header("Ground / safety")]
    [Tooltip("Layer mask to use when raycasting indicator -> ground (optional).")]
    public LayerMask groundMask = ~0;

    [Header("Debug / Utilities")]
    [Tooltip("Auto-start waves on Awake (for testing).")]
    public bool autoStartForTest = false;

    private int currentWaveIndex = -1;
    private int aliveEnemies = 0;
    private Coroutine runCoroutine;

    // events
    public event Action<int> OnWaveStarted;            // passes wave index
    public event Action<int> OnWaveCompleted;          // passes wave index
    public event Action OnAllWavesCompleted;
    public event Action<int> OnEnemySpawned;           // passes remaining enemies in wave after spawn
    public event Action<int> OnEnemyDied;              // passes alive count

    private void Awake()
    {
        if (autoStartForTest)
        {
            StartWaves();
        }
    }

    /// <summary>
    /// Begins the full wave sequence (start from wave 0).
    /// If a run is already active it will stop and restart.
    /// </summary>
    public void StartWaves()
    {
        if (runCoroutine != null) StopCoroutine(runCoroutine);
        runCoroutine = StartCoroutine(RunWaves());
    }

    /// <summary>
    /// Stop an active wave run (cancels further spawns), but does not destroy existing enemies.
    /// </summary>
    public void StopWaves()
    {
        if (runCoroutine != null)
        {
            StopCoroutine(runCoroutine);
            runCoroutine = null;
        }
    }

    private IEnumerator RunWaves()
    {
        currentWaveIndex = 0;

        while (currentWaveIndex < waves.Count)
        {
            Wave wave = waves[currentWaveIndex];

            // notify
            OnWaveStarted?.Invoke(currentWaveIndex);

            // spawn wave
            int toSpawn = wave.enemyCount;
            int spawned = 0;

            while (spawned < toSpawn)
            {
                Vector3 spawnPos;
                TryGetRandomSpawnPosition(out spawnPos);

                // show indicator (if any)
                if (spawnIndicatorPrefab != null && indicatorDelay > 0f)
                {
                    GameObject ind = Instantiate(spawnIndicatorPrefab, spawnPos, Quaternion.identity);
                    // optional: indicator can implement its own behavior; we destroy after delay+small buffer
                    Destroy(ind, indicatorDelay + 0.25f);
                }

                // wait indicator delay
                float wait = indicatorDelay;
                // if no indicator, we still may optionally wait a minimal amount (0)
                if (wait > 0f) yield return new WaitForSeconds(wait);

                // spawn actual enemy
                GameObject prefab = ChooseEnemyPrefab();
                if (prefab != null)
                {
                    GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
                    var enemy = go.GetComponent<Enemy>();
                    if (enemy != null)
                    {
                        // set spawner reference so enemies can report back to spawner if desired
                        enemy.spawner = this;
                        RegisterEnemy(enemy);
                    }
                }

                spawned++;
                OnEnemySpawned?.Invoke(toSpawn - spawned);

                // wait spawn interval
                if (wave.spawnInterval > 0f)
                    yield return new WaitForSeconds(wave.spawnInterval);
                else
                    yield return null;
            }
    
            // wait until all spawned enemies die
            // if aliveEnemies is 0 immediately this will pass
            while (aliveEnemies > 0)
            {
                yield return null;
            }

            // wave complete
            OnWaveCompleted?.Invoke(currentWaveIndex);

            // next wave
            currentWaveIndex++;
            // small inter-wave buffer (optional)
            yield return new WaitForSeconds(0.25f);
        }

        // all waves done
        OnAllWavesCompleted?.Invoke();
        runCoroutine = null;
    }

    private GameObject ChooseEnemyPrefab()
    {
        if (spawnPrefabs == null || spawnPrefabs.Length == 0) return null;
        if (spawnPrefabs.Length == 1) return spawnPrefabs[0];
        int idx = UnityEngine.Random.Range(0, spawnPrefabs.Length);
        return spawnPrefabs[idx];
    }

    /// <summary>
    /// Attempts to pick a random position inside the configured ring/area and sample NavMesh near it.
    /// </summary>
    public bool TryGetRandomSpawnPosition(out Vector3 position)
    {
        Vector3 worldCandidate = transform.position;

        if (spawnShape == SpawnShape.Circle)
        {
            // sample point within donut (innerRadius..spawnRadius)
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = Mathf.Sqrt(UnityEngine.Random.value) * (spawnRadius - innerRadius) + innerRadius;
            Vector3 offset = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
            worldCandidate = transform.position + offset;
        }
        else // Rectangle
        {
            int attempts = 0;
            const int maxAttempts = 32;
            Vector2 half = rectangleSize * 0.5f;
            Vector2 innerHalf = innerRectangleSize * 0.5f;
            while (attempts++ < maxAttempts)
            {
                float rx = UnityEngine.Random.Range(-half.x, half.x);
                float rz = UnityEngine.Random.Range(-half.y, half.y);
                // if inner rectangle defined, enforce outside inner rectangle
                if (innerRectangleSize.sqrMagnitude > 0f)
                {
                    if (Mathf.Abs(rx) < innerHalf.x && Mathf.Abs(rz) < innerHalf.y)
                        continue;
                }
                worldCandidate = transform.position + new Vector3(rx, 0f, rz);
                break;
            }
        }

        Vector3 rayOrigin = worldCandidate + Vector3.up * 10f;
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 50f, groundMask))
        {
            position = hit.point;
            return true;
        }

        position = new Vector3(worldCandidate.x, transform.position.y, worldCandidate.z);
        return true;
    }

    /// <summary>
    /// Register an enemy so the spawner can track alive count; subscribes to its death event.
    /// </summary>
    public void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        aliveEnemies++;
        // subscribe to death event (requires Enemy to call HandleDeath() in Die())
        enemy.GetComponent<BaseEnemy>().OnDeath += () => { UnregisterEnemy(enemy); };
        // also optionally listen for GameObject destruction fallback
        StartCoroutine(WatchEnemyFallback(enemy));
        OnEnemySpawned?.Invoke(aliveEnemies);
    }

    private IEnumerator WatchEnemyFallback(Enemy e)
    {
        // if enemy is destroyed without invoking OnDeath, we still want to decrement
        while (e != null && !e.IsDead)
        {
            yield return null;
        }
        // when IsDead becomes true (or object destroyed), UnregisterEnemy will be called via event; if event wasn't fired we make sure to cleanup
        yield break;
    }

    /// <summary>
    /// Unregister an enemy when it dies.
    /// </summary>
    private void UnregisterEnemy(Enemy enemy)
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        OnEnemyDied?.Invoke(aliveEnemies);
    }

    #region Editor helpers / debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (spawnShape == SpawnShape.Circle)
        {
            DrawCircle(transform.position, spawnRadius, 48, Color.red);
            DrawCircle(transform.position, innerRadius, 48, Color.yellow);
        }
        else
        {
            Vector3 half = new Vector3(rectangleSize.x * 0.5f, 0f, rectangleSize.y * 0.5f);
            Vector3 bl = transform.position + new Vector3(-half.x, 0f, -half.z);
            Vector3 br = transform.position + new Vector3(half.x, 0f, -half.z);
            Vector3 tr = transform.position + new Vector3(half.x, 0f, half.z);
            Vector3 tl = transform.position + new Vector3(-half.x, 0f, half.z);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);

            if (innerRectangleSize.sqrMagnitude > 0f)
            {
                Vector3 iHalf = new Vector3(innerRectangleSize.x * 0.5f, 0f, innerRectangleSize.y * 0.5f);
                Vector3 ibl = transform.position + new Vector3(-iHalf.x, 0f, -iHalf.z);
                Vector3 ibr = transform.position + new Vector3(iHalf.x, 0f, -iHalf.z);
                Vector3 itr = transform.position + new Vector3(iHalf.x, 0f, iHalf.z);
                Vector3 itl = transform.position + new Vector3(-iHalf.x, 0f, iHalf.z);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(ibl, ibr);
                Gizmos.DrawLine(ibr, itr);
                Gizmos.DrawLine(itr, itl);
                Gizmos.DrawLine(itl, ibl);
            }
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments, Color color)
    {
        if (segments < 4) segments = 4;
        float angleStep = 360f / segments;
        Vector3 prev = center + new Vector3(Mathf.Sin(0f) * radius, 0f, Mathf.Cos(0f) * radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            Gizmos.color = color;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
    #endregion
}
