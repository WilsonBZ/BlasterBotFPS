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

    [Header("Wave Settings")]
    [Tooltip("Define waves (counts and per-wave spawn interval)")]
    public List<Wave> waves = new List<Wave>() { new Wave { enemyCount = 5, spawnInterval = 0.6f } };

    [Header("Enemy Prefabs")]
    [Tooltip("Enemy prefab(s) to spawn. Must have the 'Enemy' component on the root.")]
    public GameObject[] spawnPrefabs;

    [Header("Spawn Area (local to this GameObject)")]
    [Tooltip("Radius within which enemies will randomly spawn (XZ plane).")]
    public float spawnRadius = 6f;

    [Tooltip("Min distance from the center where enemies will not spawn (optional).")]
    public float innerRadius = 1.0f;

    [Header("Spawn Indicator")]
    [Tooltip("Optional indicator prefab shown at spawn location for indicatorDelay seconds.")]
    public GameObject spawnIndicatorPrefab;
    [Tooltip("How long the indicator shows before the enemy spawns (seconds).")]
    public float indicatorDelay = 1.0f;

    [Header("NavMesh & safety")]
    [Tooltip("Max attempts to find a valid NavMesh sample position for spawn.")]
    public int navSampleAttempts = 8;
    [Tooltip("Max distance used when sampling NavMesh for spawn (meters).")]
    public float navSampleMaxDistance = 2f;
    [Tooltip("Layer mask to ignore when raycasting indicator -> ground (optional).")]
    public LayerMask groundMask = ~0;

    [Header("Debug / Utilities")]
    [Tooltip("Auto-start waves on Awake (for testing).")]
    public bool autoStartForTest = false;

    // runtime
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
                bool found = TryGetRandomSpawnPosition(out spawnPos);
                if (!found)
                {
                    // fallback to spawner position
                    spawnPos = transform.position + UnityEngine.Random.onUnitSphere * innerRadius;
                    spawnPos.y = transform.position.y;
                }

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
        // sample point within donut (innerRadius..spawnRadius)
        for (int i = 0; i < navSampleAttempts; i++)
        {
            // uniform ring sampling by choosing angle and radius sqrt-based
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = Mathf.Sqrt(UnityEngine.Random.value) * (spawnRadius - innerRadius) + innerRadius;
            Vector3 offset = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
            Vector3 worldCandidate = transform.position + offset;

            // sample navmesh near candidate
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(worldCandidate, out hit, navSampleMaxDistance, UnityEngine.AI.NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
        }

        // fallback - try random point on a circle surface (no nav sampling)
        position = transform.position + new Vector3(UnityEngine.Random.insideUnitCircle.x * spawnRadius, 0f, UnityEngine.Random.insideUnitCircle.y * spawnRadius);
        return false;
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
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, innerRadius);
    }
    #endregion
}
