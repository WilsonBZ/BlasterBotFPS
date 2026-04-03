using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utilities for bulk-fixing mesh colliders and layers across all loaded scenes.
/// </summary>
public static class FixMeshColliders
{
    private const string GroundLayerName = "Ground";

    /// <summary>
    /// Syncs every MeshCollider's sharedMesh to its sibling MeshFilter's sharedMesh,
    /// and moves every MeshFilter GameObject from the Default layer to the Ground layer.
    /// </summary>
    [MenuItem("Tools/Fix Mesh Colliders + Set Ground Layer")]
    public static void FixAndRelayer()
    {
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        if (groundLayer < 0)
        {
            Debug.LogError($"[FixMeshColliders] Layer '{GroundLayerName}' not found. Aborting.");
            return;
        }

        int colliderFixed = 0;
        int layerChanged = 0;

        MeshFilter[] allFilters = Object.FindObjectsByType<MeshFilter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MeshFilter mf in allFilters)
        {
            // Fix layer
            if (mf.gameObject.layer == LayerMask.NameToLayer("Default"))
            {
                Undo.RecordObject(mf.gameObject, "Set Ground Layer");
                mf.gameObject.layer = groundLayer;
                EditorUtility.SetDirty(mf.gameObject);
                layerChanged++;
            }

            // Fix MeshCollider sharedMesh
            MeshCollider col = mf.GetComponent<MeshCollider>();
            if (col == null || mf.sharedMesh == null)
                continue;

            if (col.sharedMesh == mf.sharedMesh)
                continue;

            Undo.RecordObject(col, "Fix MeshCollider sharedMesh");
            col.sharedMesh = mf.sharedMesh;
            EditorUtility.SetDirty(col);
            colliderFixed++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[FixMeshColliders] Layer changed: {layerChanged} | Collider mesh fixed: {colliderFixed}");
    }
}
