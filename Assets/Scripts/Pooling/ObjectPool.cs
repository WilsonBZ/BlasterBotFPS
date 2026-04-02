using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A typed pool for a single prefab. Grows automatically when exhausted.
/// Objects must call PoolManager.Release() when done instead of Destroy().
/// </summary>
public class ObjectPool
{
    private readonly GameObject prefab;
    private readonly Transform container;
    private readonly Stack<GameObject> inactive = new Stack<GameObject>();

    public ObjectPool(GameObject prefab, Transform container, int initialSize)
    {
        this.prefab    = prefab;
        this.container = container;

        for (int i = 0; i < initialSize; i++)
            inactive.Push(CreateNew());
    }

    /// <summary>Returns a ready-to-use instance, activating it at the given position/rotation.</summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = inactive.Count > 0 ? inactive.Pop() : CreateNew();
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.SetParent(null);
        go.SetActive(true);
        return go;
    }

    /// <summary>Returns the instance to the pool. Caller must not reference it afterwards.</summary>
    public void Release(GameObject go)
    {
        go.SetActive(false);
        go.transform.SetParent(container);
        inactive.Push(go);
    }

    private GameObject CreateNew()
    {
        GameObject go = Object.Instantiate(prefab, container);
        go.SetActive(false);
        return go;
    }
}
