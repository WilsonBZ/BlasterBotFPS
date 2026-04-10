using System.Collections;
using UnityEngine;

public class ElevatorCylinderDoor : MonoBehaviour
{
    public Transform leftDoor;
    public Transform rightDoor;

    public float leftOpenAngleDelta = 70f;
    public float rightOpenAngleDelta = -70f;

    [Header("Animation")]
    [Tooltip("Lerp speed for the rotation animation.")]
    public float doorSpeed = 3f;

    // Closed-state Euler Y angles captured at Start.
    private float leftClosedY;
    private float rightClosedY;

    // True while the door is (or is animating to) open.
    private bool isOpen = false;
    private Coroutine animCoroutine;

    private void Start()
    {
        if (leftDoor  != null) leftClosedY  = leftDoor.localEulerAngles.y;
        if (rightDoor != null) rightClosedY = rightDoor.localEulerAngles.y;
    }

    /// <summary>Slides the door open. Safe to call multiple times.</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        RestartAnim();
    }

    /// <summary>Slides the door closed. Safe to call multiple times.</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        RestartAnim();
    }

    private void RestartAnim()
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateDoors());
    }

    private IEnumerator AnimateDoors()
    {
        float leftTargetY  = isOpen ? leftClosedY  + leftOpenAngleDelta  : leftClosedY;
        float rightTargetY = isOpen ? rightClosedY + rightOpenAngleDelta : rightClosedY;

        bool done = false;
        while (!done)
        {
            done = true;

            if (leftDoor != null)
            {
                float currentY   = leftDoor.localEulerAngles.y;
                float newY       = Mathf.LerpAngle(currentY, leftTargetY, doorSpeed * Time.deltaTime);
                leftDoor.localEulerAngles = new Vector3(leftDoor.localEulerAngles.x, newY, leftDoor.localEulerAngles.z);
                if (Mathf.Abs(Mathf.DeltaAngle(newY, leftTargetY)) > 0.5f) done = false;
            }

            if (rightDoor != null)
            {
                float currentY   = rightDoor.localEulerAngles.y;
                float newY       = Mathf.LerpAngle(currentY, rightTargetY, doorSpeed * Time.deltaTime);
                rightDoor.localEulerAngles = new Vector3(rightDoor.localEulerAngles.x, newY, rightDoor.localEulerAngles.z);
                if (Mathf.Abs(Mathf.DeltaAngle(newY, rightTargetY)) > 0.5f) done = false;
            }

            yield return null;
        }

        // Snap to exact target angle.
        if (leftDoor  != null) leftDoor.localEulerAngles  = new Vector3(leftDoor.localEulerAngles.x,  leftTargetY,  leftDoor.localEulerAngles.z);
        if (rightDoor != null) rightDoor.localEulerAngles = new Vector3(rightDoor.localEulerAngles.x, rightTargetY, rightDoor.localEulerAngles.z);

        animCoroutine = null;
    }
}
