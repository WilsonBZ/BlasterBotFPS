using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the ElevatorTrigger box collider.
/// Player enters → prompt shows. Player presses Interact (E) → AdvanceFloor().
/// Resets automatically via AdditiveSceneManager.OnRoomsReset each floor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ElevatorInteraction : MonoBehaviour
{
    [Header("Prompt UI")]
    public Canvas promptCanvas;

    private bool playerInside = false;
    private bool hasActivated = false;
    private InputAction interactAction;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(false);

        if (InputSystem.actions != null)
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: false);

        if (interactAction == null)
            Debug.LogWarning("[ElevatorInteraction] 'Player/Interact' action not found.");
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.performed += OnInteract;

        AdditiveSceneManager.OnRoomsReset += ResetState;
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteract;

        AdditiveSceneManager.OnRoomsReset -= ResetState;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasActivated || !other.CompareTag("Player")) return;
        playerInside = true;
        SetPrompt(true);
        Debug.Log("[ElevatorInteraction] Player entered trigger.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = false;
        SetPrompt(false);
    }

    /// <summary>Fires on Interact press. Calls AdvanceFloor immediately.</summary>
    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!playerInside || hasActivated) return;

        hasActivated = true;
        SetPrompt(false);
        Debug.Log("[ElevatorInteraction] Advancing floor.");

        if (FloorProgressManager.Instance != null)
            FloorProgressManager.Instance.AdvanceFloor();
        else
            Debug.LogError("[ElevatorInteraction] FloorProgressManager.Instance is NULL — is it in the scene?");
    }

    /// <summary>Re-arms the trigger after each floor reset.</summary>
    private void ResetState()
    {
        hasActivated = false;
        playerInside = false;
        SetPrompt(false);
        Debug.Log("[ElevatorInteraction] Reset for new floor.");
    }

    private void SetPrompt(bool visible)
    {
        if (promptCanvas != null)
            promptCanvas.gameObject.SetActive(visible);
    }
}
