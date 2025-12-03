using UnityEngine;

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
    public Transform player;

    private bool isOpen = false;

    [Header("Wave Spawner Trigger")]
    public WaveSpawner waveSpawner;
    public bool triggerOnOpen = true;
    public bool triggerOnce = true;

    private bool prevIsOpen = false;
    private bool hasTriggered = false;

    void Start()
    {
        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;
        prevIsOpen = isOpen;
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(player.position, transform.position);

        if (dist < openDistance)
            isOpen = true;
        else
            isOpen = false;

        if (isOpen && !prevIsOpen)
        {
            if (triggerOnOpen && waveSpawner != null && (!triggerOnce || !hasTriggered))
            {
                waveSpawner.StartWaves();
                hasTriggered = true;
            }
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
}
