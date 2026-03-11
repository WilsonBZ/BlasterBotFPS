using UnityEngine;
using System;

public class SlidingDoubleDoor : MonoBehaviour
{
    [Header("Door Panels")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Positions")]
    public Vector3 leftOpenOffset = new Vector3(-1f, 0, 0);
    public Vector3 rightOpenOffset = new Vector3(1f, 0, 0);

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;

    [Header("Settings")]
    public float openDistance = 4f;
    public float doorSpeed = 4f;

    [Tooltip("Tag used to find the player at runtime. Avoids cross-scene direct references.")]
    public string playerTag = "Player";

    private Transform player;

    [Header("Lock Settings")]
    [Tooltip("If true, door will not open even when player is nearby")]
    public bool isLocked = false;

    private bool isOpen = false;

    [Header("Wave Spawner Trigger")]
    public WaveSpawner waveSpawner;
    public bool triggerOnOpen = true;
    public bool triggerOnce = true;

    private bool prevIsOpen = false;
    private bool hasTriggered = false;

    public event Action<SlidingDoubleDoor> OnDoorOpened;
    public event Action<SlidingDoubleDoor> OnDoorClosed;

    void Start()
    {
        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;
        prevIsOpen = isOpen;

        FindPlayer();
    }

    /// <summary>Finds the player by tag. Safe to call again if the player spawns late.</summary>
    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag(playerTag);
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning($"[SlidingDoubleDoor] No GameObject with tag '{playerTag}' found. Door will retry each frame until found.");
        }
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        float dist = Vector3.Distance(player.position, transform.position);

        if (!isLocked && dist < openDistance)
            isOpen = true;
        else
            isOpen = false;

        if (isOpen && !prevIsOpen)
        {
            OnDoorOpened?.Invoke(this);

            if (triggerOnOpen && waveSpawner != null && (!triggerOnce || !hasTriggered))
            {
                waveSpawner.StartWaves();
                hasTriggered = true;
            }
        }
        else if (!isOpen && prevIsOpen)
        {
            OnDoorClosed?.Invoke(this);
        }

        prevIsOpen = isOpen;

        MoveDoors();
    }

    private void MoveDoors()
    {
        if (isOpen)
        {
            leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftClosedPos + leftOpenOffset, Time.deltaTime * doorSpeed);
            rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightClosedPos + rightOpenOffset, Time.deltaTime * doorSpeed);
        }
        else
        {
            leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftClosedPos, Time.deltaTime * doorSpeed);
            rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightClosedPos, Time.deltaTime * doorSpeed);
        }
    }

    public void Lock()
    {
        isLocked = true;
    }

    public void Unlock()
    {
        isLocked = false;
    }
}
