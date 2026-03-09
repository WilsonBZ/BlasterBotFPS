using UnityEngine;

/// <summary>
/// Place this on a trigger collider attached to (or near) an exit door.
/// It watches the room's <see cref="RoomManager"/> cleared state and, once
/// cleared, fires the transition when the player physically walks through it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SceneExitTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RoomManager for the room this exit belongs to.")]
    public RoomManager roomManager;

    [Tooltip("The scene name of the room this exit belongs to. Must match the name in AdditiveSceneManager's list.")]
    public string owningSceneName;

    private Collider triggerCollider;
    private bool hasTriggered = false;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        bool roomIsCleared = roomManager == null || roomManager.isCleared;
        if (!roomIsCleared)
        {
            Debug.Log("[SceneExitTrigger] Room not yet cleared. Transition blocked.");
            return;
        }

        hasTriggered = true;
        AdditiveSceneManager.Instance?.OnPlayerExitedRoom(owningSceneName);
    }
}
