using UnityEngine;

/// <summary>
/// Add this to any prefab that participates in the pool system.
/// Provides a convenient self-release method and resets state on re-enable.
/// </summary>
public class Poolable : MonoBehaviour
{
    /// <summary>Set automatically by PoolManager when the object is retrieved.</summary>
    [HideInInspector] public PoolManager Pool;

    /// <summary>
    /// Returns this GameObject to its pool.
    /// Call this instead of Destroy().
    /// </summary>
    public void Release()
    {
        if (Pool != null)
            Pool.Release(gameObject);
        else
            Destroy(gameObject);
    }
}
