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
    public float openDistance = 4f;       // How close player must be
    public float doorSpeed = 4f;          // Slide speed
    public Transform player;              // Player transform

    private bool isOpen = false;

    void Start()
    {
        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;
    }

    void Update()
    {
        float dist = Vector3.Distance(player.position, transform.position);

        if (dist < openDistance)
            isOpen = true;
        else
            isOpen = false;

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
