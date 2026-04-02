using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that owns all object pools. Add prefabs via the Inspector
/// WarmupEntries list, or let pools create themselves on first use (lazy init).
/// Call PoolManager.Instance.Get(prefab, pos, rot) and PoolManager.Instance.Release(go).
/// </summary>
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [System.Serializable]
    public class WarmupEntry
    {
        public GameObject prefab;
        [Tooltip("Number of instances pre-created at startup.")]
        public int initialSize = 10;
    }

    [Header("Pre-warmed Pools")]
    [Tooltip("Add frequently-spawned prefabs here so Unity doesn't allocate mid-game.")]
    public List<WarmupEntry> warmupEntries = new List<WarmupEntry>();

    private readonly Dictionary<int, ObjectPool> pools   = new Dictionary<int, ObjectPool>();
    private readonly Dictionary<int, int>        prefabIds = new Dictionary<int, int>(); // instanceID -> prefabID

    private Transform poolRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        poolRoot = new GameObject("_Pools").transform;
        poolRoot.SetParent(transform);

        foreach (var entry in warmupEntries)
        {
            if (entry.prefab != null)
                GetOrCreatePool(entry.prefab, entry.initialSize);
        }
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>Retrieves an instance from the pool (or creates one if needed).</summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        ObjectPool pool = GetOrCreatePool(prefab);
        GameObject go   = pool.Get(position, rotation);

        // Remember which prefab this instance came from so Release() works without a prefab argument.
        prefabIds[go.GetInstanceID()] = prefab.GetInstanceID();

        // Re-register after activation (the ID changes when the object changes state).
        // We register on the Poolable component if present; otherwise caller must call Release() manually.
        Poolable poolable = go.GetComponent<Poolable>();
        if (poolable != null)
            poolable.Pool = this;

        return go;
    }

    /// <summary>Returns an instance back to its pool.</summary>
    public void Release(GameObject go)
    {
        if (go == null) return;

        int instanceId = go.GetInstanceID();
        if (!prefabIds.TryGetValue(instanceId, out int prefabId))
        {
            // Not from any pool — fall back to Destroy so nothing leaks.
            Destroy(go);
            return;
        }

        if (pools.TryGetValue(prefabId, out ObjectPool pool))
        {
            pool.Release(go);
            prefabIds.Remove(instanceId);
        }
        else
        {
            Destroy(go);
        }
    }

    // ─── Internals ─────────────────────────────────────────────────────────────

    private ObjectPool GetOrCreatePool(GameObject prefab, int initialSize = 5)
    {
        int id = prefab.GetInstanceID();
        if (!pools.TryGetValue(id, out ObjectPool pool))
        {
            Transform container = new GameObject(prefab.name + "_Pool").transform;
            container.SetParent(poolRoot);
            pool = new ObjectPool(prefab, container, initialSize);
            pools[id] = pool;
        }
        return pool;
    }
}
