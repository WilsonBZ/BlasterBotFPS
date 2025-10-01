using UnityEngine;


public class Billboard : MonoBehaviour
{
    public enum Axis { Up, Forward }

    [SerializeField] private Axis pivotAxis = Axis.Up;
    [SerializeField] private bool reverseFace = true;

    private Transform mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        Vector3 targetPos = transform.position + mainCamera.rotation * (reverseFace ? Vector3.forward : Vector3.back);
        Vector3 targetOrientation = mainCamera.rotation * Vector3.up;

        if (pivotAxis == Axis.Forward)
        {
            transform.LookAt(targetPos, targetOrientation);
        }
        else
        {
            transform.rotation = mainCamera.rotation;
        }
    }
}