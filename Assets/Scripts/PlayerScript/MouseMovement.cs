using UnityEngine;

public class MouseMovement : MonoBehaviour
{
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float sensitivity = 15f;
    [SerializeField] private float tiltAmount = 5f;
    [SerializeField] private float tiltSpeed = 10f;
    [SerializeField] private Camera mainCamera;

    [Header("Slide Effects")]
    [SerializeField] private float slideFOV = 100f;
    [SerializeField] private float fovTransitionSpeed = 8f;
    [SerializeField] private float edgeDistortionIntensity = 0.5f;
    [SerializeField] private Material edgeDistortionMaterial;

    private float baseFOV;
    private float targetFOV;
    private float xRotation = 0f;
    private float currentTilt = 0f;

    // Cached material intensity — avoids GetFloat every frame.
    private float cachedEdgeIntensity = 0f;
    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    private static readonly int OffsetXID   = Shader.PropertyToID("_OffsetX");
    private static readonly int OffsetYID   = Shader.PropertyToID("_OffsetY");

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        baseFOV   = mainCamera != null ? mainCamera.fieldOfView : 60f;
        targetFOV = baseFOV;

        if (cameraTransform == null)
            cameraTransform = mainCamera != null ? mainCamera.transform : transform;

        if (playerBody == null)
            playerBody = transform;

        if (edgeDistortionMaterial != null)
            edgeDistortionMaterial.SetFloat(IntensityID, 0f);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = locked;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation  = Mathf.Clamp(xRotation, -89f, 89f);

            float targetTilt = Input.GetKey(KeyCode.A) ? tiltAmount :
                               Input.GetKey(KeyCode.D) ? -tiltAmount : 0f;

            currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSpeed * Time.deltaTime);

            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, currentTilt);

            if (playerBody != null)
                playerBody.Rotate(Vector3.up * mouseX);
        }

        if (mainCamera != null)
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, fovTransitionSpeed * Time.deltaTime);

        UpdateEdgeEffects();
    }

    /// <summary>Sets FOV and edge distortion intensity when the player slides.</summary>
    public void SetSlideEffects(bool sliding, float slideSpeedNormalized)
    {
        targetFOV = sliding ? slideFOV : baseFOV;

        if (edgeDistortionMaterial != null)
        {
            cachedEdgeIntensity = sliding
                ? Mathf.Clamp01(edgeDistortionIntensity * slideSpeedNormalized * 1.5f)
                : 0f;
            edgeDistortionMaterial.SetFloat(IntensityID, cachedEdgeIntensity);
        }
    }

    private void UpdateEdgeEffects()
    {
        // Skip entirely when not sliding — avoids per-frame SetFloat calls.
        if (edgeDistortionMaterial == null || cachedEdgeIntensity <= 0.01f) return;

        edgeDistortionMaterial.SetFloat(OffsetXID, Mathf.Sin(Time.time * 10f) * 0.1f);
        edgeDistortionMaterial.SetFloat(OffsetYID, Mathf.Cos(Time.time *  8f) * 0.1f);
    }

    // OnRenderImage is NOT called by URP — removed to avoid a silent wasted blit
    // every frame. Hook edge distortion through a URP Renderer Feature instead if needed.
}