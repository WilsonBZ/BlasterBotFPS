using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    public DoorController[] doors;

    [Header("Spawn Indicator")]
    [SerializeField] private GameObject spawnIndicatorPrefab;
    [SerializeField] private float indicatorDuration = 1f;

    [System.Serializable]
    public class Wave
    {
        public GameObject enemyPrefab;
        public int enemyCount;
        public float spawnDelay = 1f;
    }

    public Wave[] waves;
    public Transform[] spawnPoints;

    private int currentWaveIndex = 0;
    private int aliveEnemies = 0;
    private bool playerTriggered = false;
    private bool allWavesCleared = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!playerTriggered && other.CompareTag("Player"))
        {
            playerTriggered = true;

            foreach (var door in doors)
                if (door) door.ActivateDoor();

            StartCoroutine(SpawnWave());
        }
    }

    private IEnumerator SpawnWave()
    {
        if (currentWaveIndex >= waves.Length)
            yield break;

        Wave wave = waves[currentWaveIndex];
        Debug.Log($"[WaveSpawner] Starting wave {currentWaveIndex} (count={wave.enemyCount})");

        // Spawn ALL enemies in the wave first
        for (int i = 0; i < wave.enemyCount; i++)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

            if (spawnIndicatorPrefab)
            {
                GameObject indicator = Instantiate(spawnIndicatorPrefab, spawnPoint.position, Quaternion.identity);
                Destroy(indicator, indicatorDuration);
                yield return new WaitForSeconds(indicatorDuration);
            }

            GameObject enemyGO = Instantiate(wave.enemyPrefab, spawnPoint.position, Quaternion.identity);
            BaseEnemy enemyScript = enemyGO.GetComponent<BaseEnemy>();

            if (enemyScript != null)
            {
                // If the enemy is already dead (some odd prefab logic) don't register it
                if (!enemyScript.IsDead)
                {
                    aliveEnemies++;
                    enemyScript.OnDeath += OnEnemyDeath;
                    Debug.Log($"[WaveSpawner] Registered enemy '{enemyGO.name}'. Alive count = {aliveEnemies}");
                }
                else
                {
                    Debug.LogWarning($"[WaveSpawner] Spawned enemy '{enemyGO.name}' but it is already dead.");
                }
            }
            else
            {
                Debug.LogError($"[WaveSpawner] Spawned prefab '{wave.enemyPrefab?.name}' has no BaseEnemy component!");
            }

            yield return new WaitForSeconds(wave.spawnDelay);
        }

        // Wait until all spawned enemies are dead
        while (aliveEnemies > 0)
        {
            yield return null;
        }

        Debug.Log($"[WaveSpawner] Wave {currentWaveIndex} cleared.");
        currentWaveIndex++;

        if (currentWaveIndex < waves.Length)
        {
            // small delay optionally
            StartCoroutine(SpawnWave());
        }
        else if (!allWavesCleared)
        {
            allWavesCleared = true;
            Debug.Log("[WaveSpawner] All waves cleared! Opening doors...");
            foreach (var door in doors)
            {
                if (door == null)
                    Debug.LogError("[WaveSpawner] DoorController reference missing in inspector!");
                else
                    door.DeactivateDoor();
            }
        }
    }

    private void OnEnemyDeath()
    {
        aliveEnemies--;
        Debug.Log($"[WaveSpawner] OnEnemyDeath invoked. Alive left = {aliveEnemies}");
    }
}
