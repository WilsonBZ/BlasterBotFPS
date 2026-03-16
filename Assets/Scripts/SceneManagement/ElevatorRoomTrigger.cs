using UnityEngine;

/// When the player enters, it pauses movement and shows the buff selection UI.
/// After the player picks a buff, <see cref="FloorProgressManager"/> handles
/// resetting the scenes and scaling difficulty for the next floor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ElevatorRoomTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag("Player"))
        {
            return;
        }

        hasTriggered = true;
        TriggerElevatorSequence();
    }

    private void TriggerElevatorSequence()
    {
        // Lock player movement while buff UI is open.
        DisablePlayerInput();

        // Show buff selection. After the player picks, OnBuffApplied fires,
        // and NewBuffManager calls FloorProgressManager.AdvanceFloor().
        if (NewBuffManager.Instance != null)
        {
            // Hook into the one-time buff selection result.
            NewBuffManager.Instance.OnBuffApplied += OnBuffSelected;
            NewBuffManager.Instance.ShowBuffSelection();
        }
        else
        {
            Debug.LogError("[ElevatorRoomTrigger] NewBuffManager instance not found!");
            EnablePlayerInput();
        }
    }

    private void OnBuffSelected(BuffData buff)
    {
        NewBuffManager.Instance.OnBuffApplied -= OnBuffSelected;

        EnablePlayerInput();

        FloorProgressManager.Instance?.AdvanceFloor();
    }

    private void DisablePlayerInput()
    {
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        MouseMovement mouse = FindFirstObjectByType<MouseMovement>();
        if (mouse != null) mouse.enabled = false;
    }

    private void EnablePlayerInput()
    {
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) movement.enabled = true;

        MouseMovement mouse = FindFirstObjectByType<MouseMovement>();
        if (mouse != null) mouse.enabled = true;
    }
}
