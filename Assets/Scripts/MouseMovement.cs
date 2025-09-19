using UnityEngine;

public class MouseMovement : MonoBehaviour
{
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float sensitivity = 100f;
    [SerializeField] private float tiltAmount = 5f;
    [SerializeField] private float tiltSpeed = 10f;
    [SerializeField] private Camera mainCamera;

    [Header("Slide Effects")]
    [SerializeField] private float slideFOV = 100f;
    [SerializeField] private float fovTransitionSpeed = 8f;
    [SerializeField] private float edgeDistortionIntensity = 0.5f;
    [SerializeField] private Material edgeDistortionMaterial;

    //Fov stuff
    private float baseFOV;
    private float targetFOV;

    private float xRotation = 0f;
    private float currentTilt = 0f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        baseFOV = mainCamera.fieldOfView;
        targetFOV = baseFOV;

        if (edgeDistortionMaterial)
        {
            edgeDistortionMaterial.SetFloat("_Intensity", 0f);
        }
    }

    private void Update()
    {
        // Toggle cursor lock/unlock with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

            //Handle rotation, and limit
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, currentTilt);

            playerBody.Rotate(Vector3.up * mouseX);

            //Handle tilting
            float targetTilt = 0f;
            if (Input.GetKey(KeyCode.A)) targetTilt = tiltAmount;
            if (Input.GetKey(KeyCode.D)) targetTilt = -tiltAmount;

            currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSpeed * Time.deltaTime);
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, currentTilt);
        }

        mainCamera.fieldOfView = Mathf.Lerp(
            mainCamera.fieldOfView,
            targetFOV,
            fovTransitionSpeed * Time.deltaTime
        );

        UpdateEdgeEffects();
    }

    public void SetSlideEffects(bool sliding, float slideSpeedNormalized)
    {
        targetFOV = sliding ? slideFOV : baseFOV;

        if (edgeDistortionMaterial)
        {
            float targetIntensity = sliding ?
                Mathf.Clamp01(edgeDistortionIntensity * slideSpeedNormalized * 1.5f) : 0f;

            edgeDistortionMaterial.SetFloat("_Intensity", targetIntensity);
        }
    }

    private void UpdateEdgeEffects()
    {
        if (edgeDistortionMaterial && edgeDistortionMaterial.GetFloat("_Intensity") > 0.01f)
        {
            edgeDistortionMaterial.SetFloat("_OffsetX", Mathf.Sin(Time.time * 10f) * 0.1f);
            edgeDistortionMaterial.SetFloat("_OffsetY", Mathf.Cos(Time.time * 8f) * 0.1f);
        }
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (edgeDistortionMaterial && edgeDistortionMaterial.GetFloat("_Intensity") > 0.01f)
        {
            Graphics.Blit(src, dest, edgeDistortionMaterial);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}