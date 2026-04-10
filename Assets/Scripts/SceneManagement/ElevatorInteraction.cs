using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Placed on the ElevatorTrigger zone. When the player enters the collider a world-space
/// canvas appears showing "Press E to reach next floor". Pressing E triggers the door
/// open animation and calls FloorProgressManager.AdvanceFloor(), which handles scene
/// reload, buff re-application, and 1.2x enemy scaling for the next floor.
/// The door closes automatically if the player exits the zone without pressing E.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ElevatorInteraction : MonoBehaviour
{
    [Header("Door")]
    [Tooltip("The ElevatorCylinderDoor controller in the scene.")]
    public ElevatorCylinderDoor cylinderDoor;

    [Header("Prompt UI")]
    [Tooltip("World-space Canvas that holds the 'Press E' prompt.")]
    public Canvas promptCanvas;
    [Tooltip("TextMeshPro label inside the canvas. Auto-resolved if canvas is assigned.")]
    public TMP_Text promptLabel;
    [Tooltip("Text displayed when inside the trigger zone.")]
    public string promptText = "[E] Next Floor";

    private bool playerInside = false;
    private bool hasActivated = false;

    private const KeyCode InteractKey = KeyCode.E;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (promptCanvas != null)
        {
            // Resolve text component automatically.
            if (promptLabel == null)
                promptLabel = promptCanvas.GetComponentInChildren<TMP_Text>();

            promptCanvas.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!playerInside || hasActivated) return;

        if (Input.GetKeyDown(InteractKey))
            StartCoroutine(ActivateElevator());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasActivated || !other.CompareTag("Player")) return;

        playerInside = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;
        ShowPrompt(false);

        // Close the door again if the player walked in but didn't press E.
        if (!hasActivated && cylinderDoor != null)
            cylinderDoor.Close();
    }

    private void ShowPrompt(bool visible)
    {
        if (promptCanvas == null) return;
        promptCanvas.gameObject.SetActive(visible);

        if (visible && promptLabel != null)
            promptLabel.text = promptText;
    }

    private IEnumerator ActivateElevator()
    {
        hasActivated = true;
        ShowPrompt(false);

        // Open the door first and give it a moment to animate.
        if (cylinderDoor != null)
        {
            cylinderDoor.Open();
            yield return new WaitForSeconds(1.5f);
        }

        // FloorProgressManager.AdvanceFloor() handles scene reload, buff re-apply,
        // and applying the 1.2x enemy multiplier via ApplyDifficultyToSpawners.
        FloorProgressManager.Instance?.AdvanceFloor();
    }
}
