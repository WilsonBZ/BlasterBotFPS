using UnityEngine;

/// <summary>
/// One per room scene. Wires up the room's <see cref="RoomManager"/> events to
/// scene-level responses such as visual feedback when the room is cleared.
/// The actual scene transition is handled by <see cref="SceneExitTrigger"/>.
/// </summary>
[DisallowMultipleComponent]
public class RoomSceneConnector : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RoomManager for this room. Auto-found in scene if not assigned.")]
    public RoomManager roomManager;

    [Header("Optional Feedback")]
    [Tooltip("GameObjects to activate when the room is cleared (e.g. a cleared indicator light).")]
    public GameObject[] onClearedActivate;

    [Tooltip("GameObjects to deactivate when the room is cleared.")]
    public GameObject[] onClearedDeactivate;

    private void Awake()
    {
        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }
    }

    private void OnEnable()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomCleared += HandleRoomCleared;
        }
    }

    private void OnDisable()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomCleared -= HandleRoomCleared;
        }
    }

    private void HandleRoomCleared()
    {
        foreach (var go in onClearedActivate)
        {
            if (go != null)
            {
                go.SetActive(true);
            }
        }

        foreach (var go in onClearedDeactivate)
        {
            if (go != null)
            {
                go.SetActive(false);
            }
        }

        Debug.Log($"[RoomSceneConnector] Room cleared in scene: {gameObject.scene.name}");
    }
}
